using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DanaProcessing
{
    /// <summary>
    /// Latitude/longitude helpers for maps and globes: placing a point on a sphere,
    /// distances and circles on the Earth's surface, flat (equirectangular) maps, and
    /// evenly spread points. Angles are in degrees, distances in km.
    ///
    /// Sphere convention: x points east (towards longitude 90°), y north (up), and
    /// longitude 0 faces -z -- so a camera on the -z axis sees Africa and Europe.
    /// </summary>
    public static class Geo
    {
        /// <summary>The Earth's mean radius, km.</summary>
        public const float EarthRadiusKm = 6371f;

        const float Deg = MathF.PI / 180f;

        /// <summary>A latitude/longitude as a point on a sphere of this radius (centered on the origin).</summary>
        public static PVector ToSphere(float lat, float lon, float radius = 1)
        {
            float la = lat * Deg, lo = lon * Deg, c = MathF.Cos(la);
            return new PVector(c * MathF.Sin(lo) * radius, MathF.Sin(la) * radius, -c * MathF.Cos(lo) * radius);
        }

        /// <summary>The latitude/longitude of a point (any distance from the center), the inverse of ToSphere().</summary>
        public static (float Lat, float Lon) FromSphere(PVector p)
        {
            float r = MathF.Sqrt(p.X * p.X + p.Y * p.Y + p.Z * p.Z);
            if (r == 0) return (0, 0);
            return (MathF.Asin(Math.Clamp(p.Y / r, -1, 1)) / Deg, MathF.Atan2(p.X, -p.Z) / Deg);
        }

        /// <summary>Great-circle distance between two places, km (the haversine formula).</summary>
        public static float DistanceKm(float lat1, float lon1, float lat2, float lon2)
        {
            double dLat = (lat2 - lat1) * Deg, dLon = (lon2 - lon1) * Deg;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Deg) * Math.Cos(lat2 * Deg) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return (float)(2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(a))));
        }

        /// <summary>Where you end up going <paramref name="km"/> from (lat, lon) along a compass bearing (0 = north, 90 = east).</summary>
        public static (float Lat, float Lon) Destination(float lat, float lon, float bearingDeg, float km)
        {
            double la = lat * Deg, lo = lon * Deg, br = bearingDeg * Deg, d = km / EarthRadiusKm;
            double lat2 = Math.Asin(Math.Sin(la) * Math.Cos(d) + Math.Cos(la) * Math.Sin(d) * Math.Cos(br));
            double lon2 = lo + Math.Atan2(Math.Sin(br) * Math.Sin(d) * Math.Cos(la), Math.Cos(d) - Math.Sin(la) * Math.Sin(lat2));
            double lonDeg = (lon2 / Deg + 540) % 360 - 180;
            return ((float)(lat2 / Deg), (float)lonDeg);
        }

        /// <summary>The ring of places <paramref name="km"/> away from (lat, lon): segments + 1 points, the last repeating the first. Draw it with ToSphere() or Equirectangular().</summary>
        public static (float Lat, float Lon)[] Circle(float lat, float lon, float km, int segments = 64)
        {
            var points = new (float, float)[segments + 1];
            for (int k = 0; k <= segments; k++)
                points[k] = Destination(lat, lon, k * 360f / segments, km);
            return points;
        }

        /// <summary>A flat world map w x h: longitude -180..180 left to right, latitude 90..-90 top to bottom.</summary>
        public static (float X, float Y) Equirectangular(float lat, float lon, float w, float h) =>
            ((lon + 180) / 360 * w, (90 - lat) / 180 * h);

        /// <summary>The inverse of Equirectangular(): a point on a w x h flat map back to latitude/longitude.</summary>
        public static (float Lat, float Lon) FromEquirectangular(float x, float y, float w, float h) =>
            (90 - y / h * 180, x / w * 360 - 180);

        /// <summary>
        /// <paramref name="count"/> points spread evenly over a unit sphere (a Fibonacci spiral):
        /// the classic dotted globe. Each is a ToSphere() direction; FromSphere() gives its lat/lon.
        /// </summary>
        public static PVector[] FibonacciSphere(int count)
        {
            var points = new PVector[count];
            for (int k = 0; k < count; k++)
            {
                float y = 1 - 2 * (k + 0.5f) / count, r = MathF.Sqrt(1 - y * y), a = k * 2.39996323f;
                points[k] = new PVector(MathF.Cos(a) * r, y, MathF.Sin(a) * r);
            }
            return points;
        }
    }

    /// <summary>One closed outline (or open line) of a GeoJSON geometry, as parallel latitude/longitude arrays.</summary>
    public sealed class GeoRing
    {
        internal GeoRing(float[] lat, float[] lon, bool isHole)
        {
            Lat = lat; Lon = lon; IsHole = isHole;
        }

        public float[] Lat { get; }
        public float[] Lon { get; }

        /// <summary>True for a hole inside a polygon (a lake, an enclave).</summary>
        public bool IsHole { get; }

        public int Count => Lat.Length;
    }

    /// <summary>
    /// One thing in a GeoJSON file: a country, a city, an earthquake, a route... Its
    /// geometry is in <see cref="Points"/> (points), <see cref="Rings"/> (lines and polygon
    /// outlines), and its data in Text("key") / Number("key").
    /// </summary>
    public sealed class GeoFeature
    {
        private readonly Dictionary<string, JsonElement> _properties = new(StringComparer.Ordinal);

        /// <summary>"Point", "Polygon", "MultiPolygon", "LineString"... (the first one, for a GeometryCollection).</summary>
        public string GeometryType { get; internal set; } = "";

        /// <summary>The feature's id, if it has one.</summary>
        public string Id { get; internal set; }

        /// <summary>Points of Point/MultiPoint geometries: latitude, longitude and the optional third value (altitude, or depth for earthquakes).</summary>
        public List<(float Lat, float Lon, float Alt)> Points { get; } = new();

        /// <summary>Outlines of polygons (holes marked) and lines.</summary>
        public List<GeoRing> Rings { get; } = new();

        /// <summary>Its first point (for point features).</summary>
        public float Lat => Points.Count > 0 ? Points[0].Lat : 0;
        public float Lon => Points.Count > 0 ? Points[0].Lon : 0;
        public float Alt => Points.Count > 0 ? Points[0].Alt : 0;

        public float MinLat { get; internal set; } = float.MaxValue;
        public float MaxLat { get; internal set; } = float.MinValue;
        public float MinLon { get; internal set; } = float.MaxValue;
        public float MaxLon { get; internal set; } = float.MinValue;

        /// <summary>A display name from the usual properties: name, NAME, ADMIN, title.</summary>
        public string Name => Text("name") ?? Text("NAME") ?? Text("ADMIN") ?? Text("title") ?? Id ?? "";

        /// <summary>The property names this feature has.</summary>
        public IEnumerable<string> Keys => _properties.Keys;

        public bool Has(string key) => _properties.ContainsKey(key);

        /// <summary>A property as text (numbers and booleans too); null when it's missing or null.</summary>
        public string Text(string key)
        {
            if (!_properties.TryGetValue(key, out var v)) return null;
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => v.GetRawText(),
            };
        }

        /// <summary>A property as a number; <paramref name="fallback"/> when it's missing, null or not a number.</summary>
        public double Number(string key, double fallback = double.NaN)
        {
            if (!_properties.TryGetValue(key, out var v)) return fallback;
            if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
            if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
            return fallback;
        }

        /// <summary>
        /// Whether (lat, lon) is inside this feature's polygons (holes excluded) -- e.g. which
        /// country a place is in. Even-odd ray casting; lines and points contain nothing.
        /// </summary>
        public bool Contains(float lat, float lon)
        {
            if (lat < MinLat || lat > MaxLat || lon < MinLon || lon > MaxLon) return false;
            if (GeometryType is not ("Polygon" or "MultiPolygon" or "GeometryCollection")) return false;
            bool inside = false;
            foreach (var ring in Rings)
            {
                var la = ring.Lat; var lo = ring.Lon;
                for (int i = 0, j = la.Length - 1; i < la.Length; j = i++)
                    if ((la[i] > lat) != (la[j] > lat) && lon < (lo[j] - lo[i]) * (lat - la[i]) / (la[j] - la[i]) + lo[i])
                        inside = !inside;
            }
            return inside;
        }

        internal void SetProperty(string key, JsonElement value) => _properties[key] = value.Clone();

        internal void Extend(float lat, float lon)
        {
            MinLat = Math.Min(MinLat, lat); MaxLat = Math.Max(MaxLat, lat);
            MinLon = Math.Min(MinLon, lon); MaxLon = Math.Max(MaxLon, lon);
        }
    }

    /// <summary>
    /// A GeoJSON file read into features (countries, earthquakes, routes...). Get one with
    /// LoadGeoJSON(pathOrUrl) or, without waiting, RequestGeoJSON(url).
    /// <code>
    /// var world = LoadGeoJSON("https://.../countries.geojson");
    /// var country = world.FeatureAt(23.1f, -82.4f);   // Cuba
    /// </code>
    /// </summary>
    public sealed class GeoMap
    {
        public List<GeoFeature> Features { get; } = new();

        public int Count => Features.Count;

        /// <summary>The first feature whose polygons contain (lat, lon) -- which country a place is in -- or null.</summary>
        public GeoFeature FeatureAt(float lat, float lon)
        {
            foreach (var f in Features)
                if (f.Contains(lat, lon)) return f;
            return null;
        }

        /// <summary>Reads GeoJSON text: a FeatureCollection, a single Feature, or a bare geometry.</summary>
        public static GeoMap Parse(string json)
        {
            var map = new GeoMap();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string type = root.TryGetProperty("type", out var t) ? t.GetString() : "";
            if (type == "FeatureCollection")
            {
                foreach (var f in root.GetProperty("features").EnumerateArray())
                    map.Features.Add(ReadFeature(f));
            }
            else if (type == "Feature")
                map.Features.Add(ReadFeature(root));
            else
            {
                var feature = new GeoFeature();
                ReadGeometry(feature, root);
                map.Features.Add(feature);
            }
            return map;
        }

        private static GeoFeature ReadFeature(JsonElement f)
        {
            var feature = new GeoFeature();
            if (f.TryGetProperty("id", out var id) && id.ValueKind != JsonValueKind.Null)
                feature.Id = id.ValueKind == JsonValueKind.String ? id.GetString() : id.GetRawText();
            if (f.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
                foreach (var p in props.EnumerateObject())
                    feature.SetProperty(p.Name, p.Value);
            if (f.TryGetProperty("geometry", out var g) && g.ValueKind == JsonValueKind.Object)
                ReadGeometry(feature, g);
            return feature;
        }

        private static void ReadGeometry(GeoFeature feature, JsonElement g)
        {
            string type = g.GetProperty("type").GetString() ?? "";
            if (feature.GeometryType == "") feature.GeometryType = type;
            if (type == "GeometryCollection")
            {
                foreach (var child in g.GetProperty("geometries").EnumerateArray())
                    ReadGeometry(feature, child);
                return;
            }
            var c = g.GetProperty("coordinates");
            switch (type)
            {
                case "Point": AddPoint(feature, c); break;
                case "MultiPoint": foreach (var p in c.EnumerateArray()) AddPoint(feature, p); break;
                case "LineString": AddRing(feature, c, false); break;
                case "MultiLineString": foreach (var l in c.EnumerateArray()) AddRing(feature, l, false); break;
                case "Polygon": AddPolygon(feature, c); break;
                case "MultiPolygon": foreach (var poly in c.EnumerateArray()) AddPolygon(feature, poly); break;
            }
        }

        private static void AddPoint(GeoFeature feature, JsonElement p)
        {
            float lon = (float)p[0].GetDouble(), lat = (float)p[1].GetDouble();
            float alt = p.GetArrayLength() > 2 && p[2].ValueKind == JsonValueKind.Number ? (float)p[2].GetDouble() : 0;
            feature.Points.Add((lat, lon, alt));
            feature.Extend(lat, lon);
        }

        private static void AddPolygon(GeoFeature feature, JsonElement polygon)
        {
            bool first = true;
            foreach (var ring in polygon.EnumerateArray())
            {
                AddRing(feature, ring, !first);
                first = false;
            }
        }

        private static void AddRing(GeoFeature feature, JsonElement ring, bool hole)
        {
            int n = ring.GetArrayLength();
            var lat = new float[n];
            var lon = new float[n];
            int k = 0;
            foreach (var p in ring.EnumerateArray())
            {
                lon[k] = (float)p[0].GetDouble();
                lat[k] = (float)p[1].GetDouble();
                if (!hole) feature.Extend(lat[k], lon[k]);
                k++;
            }
            feature.Rings.Add(new GeoRing(lat, lon, hole));
        }
    }
}
