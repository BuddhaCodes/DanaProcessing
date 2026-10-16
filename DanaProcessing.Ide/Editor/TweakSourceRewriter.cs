using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DanaProcessing.Ide.Compilation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DanaProcessing.Ide.Editor
{
    /// <summary>
    /// "Apply tweaks to code": writes the values you tuned with the [Tweak]
    /// sliders back into the sketch's source, as the initializers of those same
    /// fields. Pure text-in, edits-out (no editor, no running sketch) so it's
    /// testable on its own; MainWindow applies the edits to the editor's
    /// document as a single undoable step.
    ///
    /// Only touches what really changed: fields whose slider was never moved
    /// are skipped (so `MathF.PI / 2` survives as long as nobody moved that
    /// slider), and an initializer whose value already equals the running
    /// value is left exactly as written (`1.5708f` isn't rewritten to `1.571f`). Everything else in the file (comments, spacing, the
    /// attribute itself) is untouched, since only the initializer's own span
    /// is replaced.
    /// </summary>
    public static class TweakSourceRewriter
    {
        /// <summary>Replace <see cref="Length"/> characters at <see cref="Start"/> with <see cref="Text"/>.</summary>
        public sealed record Edit(int Start, int Length, string Text);

        /// <summary>The edits that bring <paramref name="source"/> in line with <paramref name="values"/>, sorted from the END of the file backwards so they can be applied one after another without shifting each other's offsets.</summary>
        public static IReadOnlyList<Edit> ComputeEdits(string source, IReadOnlyList<TweakValue> values)
        {
            var byName = new Dictionary<string, TweakValue>();
            foreach (var v in values)
                byName[v.FieldName] = v;

            var tree = CSharpSyntaxTree.ParseText(source, SketchCompiler.SharedParseOptions);
            var edits = new List<Edit>();

            foreach (var field in tree.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                if (!HasTweakAttribute(field))
                    continue;

                foreach (var variable in field.Declaration.Variables)
                {
                    if (!byName.TryGetValue(variable.Identifier.ValueText, out var value) || !value.Touched)
                        continue;

                    if (variable.Initializer is { } initializer)
                    {
                        if (AlreadyEqual(initializer.Value, value))
                            continue;
                        edits.Add(new Edit(initializer.Value.SpanStart, initializer.Value.Span.Length, value.CSharpLiteral));
                    }
                    else
                    {
                        // `[Tweak] float speed;` -- give it an initializer.
                        edits.Add(new Edit(variable.Identifier.Span.End, 0, " = " + value.CSharpLiteral));
                    }
                }
            }

            edits.Sort((a, b) => b.Start.CompareTo(a.Start));
            return edits;
        }

        /// <summary>Convenience for tests and callers that just want the new text.</summary>
        public static string Apply(string source, IReadOnlyList<TweakValue> values)
        {
            var sb = new StringBuilder(source);
            foreach (var edit in ComputeEdits(source, values))
            {
                sb.Remove(edit.Start, edit.Length);
                sb.Insert(edit.Start, edit.Text);
            }
            return sb.ToString();
        }

        private static bool HasTweakAttribute(FieldDeclarationSyntax field) =>
            field.AttributeLists
                .SelectMany(list => list.Attributes)
                .Any(attribute =>
                {
                    string name = attribute.Name.ToString();
                    int dot = name.LastIndexOf('.');
                    if (dot >= 0)
                        name = name.Substring(dot + 1);
                    return name == "Tweak" || name == "TweakAttribute";
                });

        /// <summary>
        /// True when the initializer as written already produces the running
        /// value. Numbers and bools are compared by value (in the field's own
        /// precision, so `1.5708f` matches the float it compiled to); a Color is
        /// compared by its `new Color(r, g, b[, a])` arguments; anything else
        /// (an expression we can't evaluate here) only counts as equal if its
        /// text is literally the same as the new literal.
        /// </summary>
        private static bool AlreadyEqual(ExpressionSyntax expression, TweakValue value)
        {
            switch (value.Value)
            {
                case float f:
                    return TryEvaluateNumber(expression, out double nf) && (float)nf == f;
                case double d:
                    return TryEvaluateNumber(expression, out double nd) && nd == d;
                case int i:
                    return TryEvaluateNumber(expression, out double ni) && ni == i;
                case bool b:
                    return expression.IsKind(b ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression);
                case Color c:
                    return TryEvaluateColor(expression, out var rgba)
                        && rgba.R == c.R && rgba.G == c.G && rgba.B == c.B && rgba.A == c.A;
                default:
                    return Normalize(expression.ToString()) == Normalize(value.CSharpLiteral);
            }
        }

        private static bool TryEvaluateNumber(ExpressionSyntax expression, out double number)
        {
            number = 0;
            switch (expression)
            {
                case ParenthesizedExpressionSyntax paren:
                    return TryEvaluateNumber(paren.Expression, out number);
                case PrefixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.UnaryMinusExpression):
                    if (!TryEvaluateNumber(unary.Operand, out number))
                        return false;
                    number = -number;
                    return true;
                case PrefixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.UnaryPlusExpression):
                    return TryEvaluateNumber(unary.Operand, out number);
                case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.NumericLiteralExpression):
                    switch (literal.Token.Value)
                    {
                        case int v: number = v; return true;
                        case uint v: number = v; return true;
                        case long v: number = v; return true;
                        case ulong v: number = v; return true;
                        case float v: number = v; return true;
                        case double v: number = v; return true;
                        case decimal v: number = (double)v; return true;
                        default: return false;
                    }
                default:
                    return false;
            }
        }

        private static bool TryEvaluateColor(ExpressionSyntax expression, out (int R, int G, int B, int A) rgba)
        {
            rgba = default;
            if (expression is not BaseObjectCreationExpressionSyntax creation || creation.ArgumentList is null)
                return false;
            if (creation is ObjectCreationExpressionSyntax named && !named.Type.ToString().EndsWith("Color", StringComparison.Ordinal))
                return false;

            var args = creation.ArgumentList.Arguments;
            if (args.Count != 3 && args.Count != 4)
                return false;

            var parts = new int[4] { 0, 0, 0, 255 };
            for (int i = 0; i < args.Count; i++)
            {
                if (!TryEvaluateNumber(args[i].Expression, out double n))
                    return false;
                parts[i] = (int)n;
            }
            rgba = (parts[0], parts[1], parts[2], parts[3]);
            return true;
        }

        private static string Normalize(string code) =>
            new string(code.Where(ch => !char.IsWhiteSpace(ch)).ToArray());
    }
}
