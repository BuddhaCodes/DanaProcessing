using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace DanaProcessing
{
    /// <summary>How <see cref="PlotterSvg.Optimize"/> prepares an SVG for a pen plotter.</summary>
    public sealed record PlotterOptions
    {
        /// <summary>Filled shapes become outlines (a pen can't fill); stroke color = their fill color.</summary>
        public bool StrokesOnly { get; init; } = true;

        /// <summary>Drops shapes that cover the whole canvas with a fill and no stroke (Background()).</summary>
        public bool DropBackground { get; init; } = true;

        /// <summary>Drops text (a plotter would draw the glyph outlines one by one; keep it if you want that).</summary>
        public bool DropText { get; init; }
    }

    /// <summary>What <see cref="PlotterSvg.Optimize"/> did.</summary>
    public sealed record PlotterResult(string Svg, int Elements, int Pens, double TravelBefore, double TravelAfter);

    /// <summary>
    /// Makes a sketch's SVG kind to a pen plotter (AxiDraw, a Cricut, an old
    /// HP...): one group per stroke color -- one pen each, in Inkscape layer
    /// style -- and, inside each group, the strokes reordered so the pen
    /// travels as little as possible in the air between them (greedy nearest
    /// neighbour, flipping open polylines when their other end is closer).
    /// Backgrounds go away and fills become outlines.
    /// </summary>
    public static class PlotterSvg
    {
        private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";
        private static readonly XNamespace Inkscape = "http://www.inkscape.org/namespaces/inkscape";

        private sealed class Stroke
        {
            public required XElement Element { get; init; }
            public required (double X, double Y) Start { get; set; }
            public required (double X, double Y) End { get; set; }
            public bool Reversible { get; init; }
            public string Pen { get; set; } = "black";
        }

        public static PlotterResult Optimize(string svg, PlotterOptions? options = null)
        {
            options ??= new PlotterOptions();
            var doc = XDocument.Parse(svg);
            var root = doc.Root ?? throw new ArgumentException("Empty SVG.", nameof(svg));
            double width = Number(root.Attribute("width")?.Value) ?? 0;
            double height = Number(root.Attribute("height")?.Value) ?? 0;

            var strokes = new List<Stroke>();
            var keep = new List<XElement>(); // things that aren't strokes (defs, clip paths...) stay as they are
            var leaves = Flatten(root, Identity, new Dictionary<XName, string>()).ToList();
            root.RemoveNodes();
            foreach (var element in leaves)
            {
                string name = element.Name.LocalName;
                if (name is "defs" or "style")
                {
                    keep.Add(element);
                    continue;
                }
                if (name == "text")
                {
                    if (!options.DropText)
                        keep.Add(element);
                    continue;
                }
                var stroke = ToStroke(element, width, height, options);
                if (stroke != null)
                    strokes.Add(stroke);
            }

            double before = Travel(strokes);

            // One pen per color, each group ordered for the shortest travel.
            var ordered = new List<(string Pen, List<Stroke> Strokes)>();
            foreach (var group in strokes.GroupBy(s => s.Pen))
                ordered.Add((group.Key, NearestNeighbour(group.ToList())));
            double after = Travel(ordered.SelectMany(g => g.Strokes).ToList());

            root.SetAttributeValue(XNamespace.Xmlns + "inkscape", Inkscape.NamespaceName); // Set, not Add: optimizing twice is fine
            if (root.Attribute("viewBox") == null && width > 0 && height > 0)
                root.SetAttributeValue("viewBox", $"0 0 {Fmt(width)} {Fmt(height)}");
            foreach (var element in keep)
                root.Add(element);
            int pen = 0;
            foreach (var (color, list) in ordered)
            {
                pen++;
                var layer = new XElement(Svg + "g",
                    new XAttribute("id", $"pen-{pen}"),
                    new XAttribute(Inkscape + "groupmode", "layer"),
                    new XAttribute(Inkscape + "label", $"{pen} - {color}"));
                foreach (var s in list)
                    layer.Add(s.Element);
                root.Add(layer);
            }

            var builder = new StringBuilder();
            using (var writer = new Utf8StringWriter(builder))
                doc.Save(writer);
            return new PlotterResult(builder.ToString(), strokes.Count, ordered.Count, before, after);
        }

        private static readonly double[] Identity = { 1, 0, 0, 1, 0, 0 };

        // Presentation attributes a <g> passes down to children that don't set their own.
        private static readonly string[] Inherited = { "fill", "stroke", "stroke-width", "stroke-linecap", "stroke-linejoin", "stroke-miterlimit", "opacity" };

        /// <summary>
        /// Every drawable element with its groups dissolved: each one carries
        /// its full transform and the style it inherited. (Skia wraps whatever
        /// was drawn under a clip or a Save() in &lt;g&gt;; a plotter just wants
        /// a flat list of strokes. Clipping is dropped -- a pen can't clip.)
        /// </summary>
        private static IEnumerable<XElement> Flatten(XElement parent, double[] matrix, Dictionary<XName, string> style)
        {
            foreach (var child in parent.Elements())
            {
                string name = child.Name.LocalName;
                if (name == "clipPath")
                    continue; // clipping is dropped (see above), so its shapes would only be drawn
                if (name is "defs" or "style")
                {
                    yield return new XElement(child);
                    continue;
                }
                var m = Multiply(matrix, Matrix(child.Attribute("transform")?.Value));
                if (name == "g")
                {
                    var inner = new Dictionary<XName, string>(style);
                    foreach (var attr in Inherited)
                        if (child.Attribute(attr) is { } a)
                            inner[attr] = a.Value;
                    foreach (var leaf in Flatten(child, m, inner))
                        yield return leaf;
                    continue;
                }
                var copy = new XElement(child);
                copy.Attribute("clip-path")?.Remove();
                foreach (var (attr, value) in style)
                    if (copy.Attribute(attr) == null)
                        copy.SetAttributeValue(attr, value);
                if (m.SequenceEqual(Identity))
                    copy.Attribute("transform")?.Remove();
                else
                    copy.SetAttributeValue("transform", $"matrix({string.Join(" ", m.Select(Fmt))})");
                yield return copy;
            }
        }

        /// <summary>SVG matrices [a b c d e f]: parent then child.</summary>
        private static double[] Multiply(double[] p, double[] c) => new[]
        {
            p[0] * c[0] + p[2] * c[1],
            p[1] * c[0] + p[3] * c[1],
            p[0] * c[2] + p[2] * c[3],
            p[1] * c[2] + p[3] * c[3],
            p[0] * c[4] + p[2] * c[5] + p[4],
            p[1] * c[4] + p[3] * c[5] + p[5],
        };

        private sealed class Utf8StringWriter : StringWriter
        {
            public Utf8StringWriter(StringBuilder sb) : base(sb, CultureInfo.InvariantCulture) { }
            public override Encoding Encoding => Encoding.UTF8;
        }

        private static Stroke? ToStroke(XElement e, double width, double height, PlotterOptions options)
        {
            string? fill = e.Attribute("fill")?.Value;
            string? stroke = e.Attribute("stroke")?.Value;
            // SVG's default fill is black when the attribute is missing.
            bool hasFill = fill != "none" && !(fill == null && e.Name.LocalName is "line" or "polyline");
            bool hasStroke = stroke != null && stroke != "none";
            var matrix = Matrix(e.Attribute("transform")?.Value);

            if (options.DropBackground && hasFill && !hasStroke && CoversCanvas(e, matrix, width, height))
                return null;

            if (options.StrokesOnly && hasFill)
            {
                if (!hasStroke)
                {
                    e.SetAttributeValue("stroke", fill ?? "black");
                    e.SetAttributeValue("stroke-width", "1");
                    stroke = fill ?? "black";
                }
                e.SetAttributeValue("fill", "none");
            }

            var ends = Endpoints(e);
            if (ends == null)
                return null;
            var (start, end, reversible) = ends.Value;
            return new Stroke
            {
                Element = e,
                Start = Apply(matrix, start),
                End = Apply(matrix, end),
                Reversible = reversible,
                Pen = (stroke ?? fill ?? "black").Trim().ToLowerInvariant(),
            };
        }

        private static bool CoversCanvas(XElement e, double[] m, double width, double height)
        {
            if (e.Name.LocalName != "rect" || width <= 0 || height <= 0)
                return false;
            double x = Number(e.Attribute("x")?.Value) ?? 0, y = Number(e.Attribute("y")?.Value) ?? 0;
            double w = Number(e.Attribute("width")?.Value) ?? 0, h = Number(e.Attribute("height")?.Value) ?? 0;
            var a = Apply(m, (x, y));
            var b = Apply(m, (x + w, y + h));
            return Math.Min(a.X, b.X) <= 0.5 && Math.Min(a.Y, b.Y) <= 0.5
                && Math.Max(a.X, b.X) >= width - 0.5 && Math.Max(a.Y, b.Y) >= height - 0.5;
        }

        /// <summary>Where the pen goes down and comes up for an element (local coordinates), and whether it can be drawn backwards.</summary>
        private static ((double X, double Y) Start, (double X, double Y) End, bool Reversible)? Endpoints(XElement e)
        {
            double N(string attr) => Number(e.Attribute(attr)?.Value) ?? 0;
            switch (e.Name.LocalName)
            {
                case "line":
                    return ((N("x1"), N("y1")), (N("x2"), N("y2")), true);
                case "rect":
                    return ((N("x"), N("y")), (N("x"), N("y")), false);
                case "circle":
                case "ellipse":
                {
                    double rx = e.Attribute("r") != null ? N("r") : N("rx");
                    var p = (N("cx") + rx, N("cy"));
                    return (p, p, false);
                }
                case "polyline":
                case "polygon":
                {
                    var pts = Points(e.Attribute("points")?.Value);
                    if (pts.Count == 0)
                        return null;
                    bool closed = e.Name.LocalName == "polygon";
                    return (pts[0], closed ? pts[0] : pts[^1], !closed);
                }
                case "path":
                    return PathEnds(e.Attribute("d")?.Value);
                case "g":
                {
                    var first = e.Elements().Select(Endpoints).FirstOrDefault(x => x != null);
                    var last = e.Elements().Select(Endpoints).LastOrDefault(x => x != null);
                    return first == null || last == null ? null : (first.Value.Start, last.Value.End, false);
                }
                default:
                    return null;
            }
        }

        /// <summary>First and last point of a path; reversible when it's a single open M/L polyline (then <see cref="Reverse"/> can flip it).</summary>
        private static ((double X, double Y), (double X, double Y), bool)? PathEnds(string? d)
        {
            var tokens = Tokenize(d);
            if (tokens.Count == 0)
                return null;
            (double X, double Y) cur = (0, 0), subStart = (0, 0), start = (0, 0);
            bool haveStart = false, onlyLines = true;
            int moves = 0;
            char cmd = 'M';
            int i = 0;
            double Next()
            {
                if (i < tokens.Count && tokens[i].Num is double v)
                {
                    i++;
                    return v;
                }
                return 0;
            }
            while (i < tokens.Count)
            {
                if (tokens[i].Cmd is char c)
                {
                    cmd = c;
                    i++;
                    if (c is 'Z' or 'z')
                    {
                        cur = subStart;
                        onlyLines = false;
                        continue;
                    }
                }
                bool rel = char.IsLower(cmd);
                (double X, double Y) Rel(double x, double y) => rel ? (cur.X + x, cur.Y + y) : (x, y);
                switch (char.ToUpperInvariant(cmd))
                {
                    case 'M':
                        cur = Rel(Next(), Next());
                        subStart = cur;
                        moves++;
                        if (!haveStart) { start = cur; haveStart = true; }
                        cmd = rel ? 'l' : 'L'; // extra pairs after M are line-tos
                        break;
                    case 'L': cur = Rel(Next(), Next()); break;
                    case 'H': { double x = Next(); cur = (rel ? cur.X + x : x, cur.Y); break; }
                    case 'V': { double y = Next(); cur = (cur.X, rel ? cur.Y + y : y); break; }
                    case 'C': Next(); Next(); Next(); Next(); cur = Rel(Next(), Next()); onlyLines = false; break;
                    case 'S':
                    case 'Q': Next(); Next(); cur = Rel(Next(), Next()); onlyLines = false; break;
                    case 'T': cur = Rel(Next(), Next()); onlyLines = false; break;
                    case 'A': Next(); Next(); Next(); Next(); Next(); cur = Rel(Next(), Next()); onlyLines = false; break;
                    default: i++; break; // unknown: skip a token rather than loop forever
                }
            }
            return haveStart ? (start, cur, onlyLines && moves == 1) : null;
        }

        private readonly record struct Token(char? Cmd, double? Num);

        private static List<Token> Tokenize(string? d)
        {
            var tokens = new List<Token>();
            if (string.IsNullOrEmpty(d))
                return tokens;
            int i = 0;
            while (i < d.Length)
            {
                char c = d[i];
                if (char.IsLetter(c) && c is not ('e' or 'E'))
                {
                    tokens.Add(new Token(c, null));
                    i++;
                }
                else if (char.IsDigit(c) || c is '-' or '+' or '.')
                {
                    int startIndex = i;
                    i++;
                    bool dot = c == '.';
                    while (i < d.Length)
                    {
                        char n = d[i];
                        if (char.IsDigit(n)) { i++; continue; }
                        if (n == '.' && !dot) { dot = true; i++; continue; }
                        if (n is 'e' or 'E') { i++; if (i < d.Length && d[i] is '-' or '+') i++; continue; }
                        break;
                    }
                    if (double.TryParse(d.AsSpan(startIndex, i - startIndex), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        tokens.Add(new Token(null, v));
                }
                else
                {
                    i++;
                }
            }
            return tokens;
        }

        private static List<(double X, double Y)> Points(string? text)
        {
            var nums = Tokenize(text).Where(t => t.Num != null).Select(t => t.Num!.Value).ToList();
            var pts = new List<(double, double)>();
            for (int i = 0; i + 1 < nums.Count; i += 2)
                pts.Add((nums[i], nums[i + 1]));
            return pts;
        }

        /// <summary>Draws a reversible stroke from its other end.</summary>
        private static void Reverse(Stroke s)
        {
            var e = s.Element;
            switch (e.Name.LocalName)
            {
                case "line":
                    var (x1, y1, x2, y2) = (e.Attribute("x1")?.Value, e.Attribute("y1")?.Value, e.Attribute("x2")?.Value, e.Attribute("y2")?.Value);
                    e.SetAttributeValue("x1", x2); e.SetAttributeValue("y1", y2);
                    e.SetAttributeValue("x2", x1); e.SetAttributeValue("y2", y1);
                    break;
                case "polyline":
                    var pts = Points(e.Attribute("points")?.Value);
                    pts.Reverse();
                    e.SetAttributeValue("points", string.Join(" ", pts.Select(p => $"{Fmt(p.X)},{Fmt(p.Y)}")));
                    break;
                case "path":
                {
                    // Only single open M/L polylines are marked reversible: rebuild as absolute M/L.
                    var points = new List<(double X, double Y)>();
                    var tokens = Tokenize(e.Attribute("d")?.Value);
                    (double X, double Y) cur = (0, 0);
                    char cmd = 'M';
                    for (int i = 0; i < tokens.Count;)
                    {
                        if (tokens[i].Cmd is char c) { cmd = c; i++; continue; }
                        bool rel = char.IsLower(cmd);
                        switch (char.ToUpperInvariant(cmd))
                        {
                            case 'H': cur = (rel ? cur.X + tokens[i].Num!.Value : tokens[i].Num!.Value, cur.Y); i++; break;
                            case 'V': cur = (cur.X, rel ? cur.Y + tokens[i].Num!.Value : tokens[i].Num!.Value); i++; break;
                            default:
                                if (i + 1 >= tokens.Count || tokens[i + 1].Num == null) { i++; continue; }
                                double x = tokens[i].Num!.Value, y = tokens[i + 1].Num!.Value;
                                cur = rel ? (cur.X + x, cur.Y + y) : (x, y);
                                i += 2;
                                break;
                        }
                        points.Add(cur);
                    }
                    points.Reverse();
                    e.SetAttributeValue("d", string.Join("", points.Select((p, k) => $"{(k == 0 ? 'M' : 'L')}{Fmt(p.X)} {Fmt(p.Y)}")));
                    break;
                }
            }
            (s.Start, s.End) = (s.End, s.Start);
        }

        private static List<Stroke> NearestNeighbour(List<Stroke> strokes)
        {
            var result = new List<Stroke>(strokes.Count);
            var left = new List<Stroke>(strokes);
            (double X, double Y) pen = (0, 0);
            while (left.Count > 0)
            {
                int best = 0;
                bool flip = false;
                double bestDist = double.MaxValue;
                for (int i = 0; i < left.Count; i++)
                {
                    double d = Dist(pen, left[i].Start);
                    if (d < bestDist) { bestDist = d; best = i; flip = false; }
                    if (left[i].Reversible)
                    {
                        double r = Dist(pen, left[i].End);
                        if (r < bestDist) { bestDist = r; best = i; flip = true; }
                    }
                }
                var chosen = left[best];
                left.RemoveAt(best);
                if (flip)
                    Reverse(chosen);
                result.Add(chosen);
                pen = chosen.End;
            }
            return result;
        }

        /// <summary>Total pen-up distance, starting from the origin.</summary>
        private static double Travel(IReadOnlyList<Stroke> strokes)
        {
            double total = 0;
            (double X, double Y) pen = (0, 0);
            foreach (var s in strokes)
            {
                total += Dist(pen, s.Start);
                pen = s.End;
            }
            return total;
        }

        private static double Dist((double X, double Y) a, (double X, double Y) b) =>
            Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        /// <summary>An SVG transform as [a b c d e f] (only what Skia writes: matrix(), translate(), scale()).</summary>
        private static double[] Matrix(string? transform)
        {
            var m = new double[] { 1, 0, 0, 1, 0, 0 };
            if (string.IsNullOrWhiteSpace(transform))
                return m;
            var nums = Tokenize(transform.Replace("matrix", " ").Replace("translate", " ").Replace("scale", " "))
                .Where(t => t.Num != null).Select(t => t.Num!.Value).ToList();
            if (transform.Contains("matrix") && nums.Count >= 6)
                return nums.Take(6).ToArray();
            if (transform.Contains("translate") && nums.Count >= 1)
                return new double[] { 1, 0, 0, 1, nums[0], nums.Count > 1 ? nums[1] : 0 };
            if (transform.Contains("scale") && nums.Count >= 1)
                return new double[] { nums[0], 0, 0, nums.Count > 1 ? nums[1] : nums[0], 0, 0 };
            return m;
        }

        private static (double X, double Y) Apply(double[] m, (double X, double Y) p) =>
            (m[0] * p.X + m[2] * p.Y + m[4], m[1] * p.X + m[3] * p.Y + m[5]);

        private static double? Number(string? text) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

        private static string Fmt(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
