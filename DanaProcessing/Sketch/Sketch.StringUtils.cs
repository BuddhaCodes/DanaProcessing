using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DanaProcessing
{
    /// <summary>
    /// String utility functions — https://processing.org/reference/join_.html
    /// and siblings (match/matchAll/nf family/split/splitTokens/trim).
    /// </summary>
    public abstract partial class Sketch
    {
        /// <summary>Joins a string array into one string with separator between each element, like Processing's join().</summary>
        public string Join(string[] array, string separator) => string.Join(separator, array);

        /// <summary>Splits a string on every occurrence of a plain-text delimiter, like Processing's split(). Unlike SplitTokens(), consecutive delimiters produce empty entries and the delimiter is matched literally, not as a set of characters.</summary>
        public string[] Split(string value, char delimiter) => value.Split(delimiter);

        /// <summary>Splits a string on every occurrence of a (possibly multi-character) delimiter, like Processing's split(value, delim).</summary>
        public string[] Split(string value, string delimiter) => value.Split(new[] { delimiter }, StringSplitOptions.None);

        /// <summary>Splits a string on runs of whitespace, like Processing's one-argument splitTokens(value).</summary>
        public string[] SplitTokens(string value) => value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        /// <summary>Splits a string on runs of any character in `delimiters` (each character in the string is its own delimiter, not a multi-char sequence), like Processing's splitTokens(value, delim). Empty tokens between adjacent delimiters are dropped, matching Processing.</summary>
        public string[] SplitTokens(string value, string delimiters) => value.Split(delimiters.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

        /// <summary>Trims leading/trailing whitespace from a string, like Processing's trim(string).</summary>
        public string Trim(string value) => value.Trim();

        /// <summary>Trims each string in an array, like Processing's trim(string[]).</summary>
        public string[] Trim(string[] values)
        {
            var result = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                result[i] = values[i]?.Trim() ?? "";
            return result;
        }

        /// <summary>
        /// Matches a string against a regular expression and returns the
        /// groups of the first match (index 0 is the whole match, like
        /// Processing's match()), or null if there's no match.
        /// </summary>
        public string[]? Match(string value, string regexp)
        {
            var m = Regex.Match(value, regexp);
            if (!m.Success)
                return null;
            var groups = new string[m.Groups.Count];
            for (int i = 0; i < m.Groups.Count; i++)
                groups[i] = m.Groups[i].Success ? m.Groups[i].Value : null!;
            return groups;
        }

        /// <summary>
        /// Finds every (non-overlapping) match of a regular expression in a
        /// string, like Processing's matchAll() — each row is one match's
        /// groups, same layout as Match() returns for a single match. Null
        /// if there are no matches at all.
        /// </summary>
        public string[][]? MatchAll(string value, string regexp)
        {
            var matches = Regex.Matches(value, regexp);
            if (matches.Count == 0)
                return null;
            var result = new string[matches.Count][];
            for (int i = 0; i < matches.Count; i++)
            {
                var m = matches[i];
                var groups = new string[m.Groups.Count];
                for (int j = 0; j < m.Groups.Count; j++)
                    groups[j] = m.Groups[j].Success ? m.Groups[j].Value : null!;
                result[i] = groups;
            }
            return result;
        }

        // =====================================================================
        // nf() / nfc() / nfp() / nfs() — https://processing.org/reference/nf_.html
        // and siblings. Number-to-string formatting with a fixed digit count.
        // =====================================================================

        /// <summary>Formats an int with at least `digits` digits, zero-padded on the left, like Processing's nf(num, digits).</summary>
        public string Nf(int num, int digits) => (num < 0 ? "-" : "") + Math.Abs(num).ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0');

        /// <summary>Formats a float with `left` digits before the decimal point and `right` digits after, both zero-padded, like Processing's nf(num, left, right).</summary>
        public string Nf(float num, int left, int right)
        {
            string sign = num < 0 ? "-" : "";
            string formatted = Math.Abs(num).ToString("F" + Math.Max(0, right), CultureInfo.InvariantCulture);
            var parts = formatted.Split('.');
            string intPart = parts[0].PadLeft(left, '0');
            return right > 0 ? $"{sign}{intPart}.{parts[1]}" : $"{sign}{intPart}";
        }

        /// <summary>Formats an int with thousands separators (commas), like Processing's nfc(num).</summary>
        public string Nfc(int num) => num.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>Formats a float with thousands separators and `right` decimal digits, like Processing's nfc(num, right).</summary>
        public string Nfc(float num, int right) => num.ToString("N" + Math.Max(0, right), CultureInfo.InvariantCulture);

        /// <summary>Formats a number with a leading '+' or '-' sign always shown, like Processing's nfp(num, digits).</summary>
        public string Nfp(int num, int digits) => (num >= 0 ? "+" : "") + Nf(num, digits);

        /// <summary>Float overload of Nfp — always shows a leading sign, like Processing's nfp(num, left, right).</summary>
        public string Nfp(float num, int left, int right) => (num >= 0 ? "+" : "") + Nf(num, left, right);

        /// <summary>Formats a number padding *spaces* instead of zeros on the left (keeps the sign directly against the digits), like Processing's nfs(num, digits).</summary>
        public string Nfs(int num, int digits)
        {
            string sign = num < 0 ? "-" : "";
            string digitsOnly = Math.Abs(num).ToString(CultureInfo.InvariantCulture);
            return (sign + digitsOnly).PadLeft(digits + sign.Length, ' ');
        }

        /// <summary>Float overload of Nfs — space-padded instead of zero-padded, like Processing's nfs(num, left, right).</summary>
        public string Nfs(float num, int left, int right)
        {
            bool negative = num < 0;
            string formatted = Math.Abs(num).ToString("F" + Math.Max(0, right), CultureInfo.InvariantCulture);
            var parts = formatted.Split('.');
            string intPart = (negative ? "-" : "") + parts[0];
            intPart = intPart.PadLeft(left + (negative ? 1 : 0), ' ');
            return right > 0 ? $"{intPart}.{parts[1]}" : intPart;
        }
    }
}
