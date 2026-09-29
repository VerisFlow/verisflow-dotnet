using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace VerisFlow.LayParser.Core
{
    /// <summary>
    /// Reads values from Hamilton configuration files (.lay, .rck, .tml, .ctr) in their packed form, in which every key
    /// and every value is preceded by a one-character length prefix, e.g. "\x0CLabware.1.Id\x07Plate_1".
    /// </summary>
    /// <remarks>
    /// A key is only accepted where it is preceded by its own length prefix, so "Dx" never matches inside "Dim.Dx" or
    /// "Site.1.Dx", and values are cut by their length prefix, so values of 33 or more characters (whose prefix is a
    /// printable character) are read exactly. When no prefixed occurrence exists, a separator-based match is tried for
    /// compatibility with earlier versions of this library.
    /// </remarks>
    public static class HxCfgText
    {
        private const string Separator = @"[\s\x00-\x1F\x7F]+";

        // ISO-8859-1 maps every byte to the character with the same code, so length prefixes survive decoding.
        private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

        /// <summary>
        /// Reads a Hamilton configuration file with a byte-preserving encoding.
        /// </summary>
        public static string ReadAllText(string path)
        {
            return File.ReadAllText(path, Latin1);
        }

        /// <summary>
        /// Finds the value of <paramref name="key"/>. Returns false when the key does not exist.
        /// </summary>
        public static bool TryGetString(string content, string key, out string value)
        {
            value = string.Empty;

            if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(key))
            {
                return false;
            }

            var prefix = (char)key.Length;
            var start = 0;

            while (start < content.Length)
            {
                var index = content.IndexOf(key, start, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }

                start = index + 1;

                if (index > 0 && content[index - 1] == prefix && TryReadValueAt(content, index + key.Length, out value))
                {
                    return true;
                }
            }

            return TryGetStringBySeparator(content, key, out value);
        }

        /// <summary>
        /// The value of <paramref name="key"/>, or an empty string when the key does not exist.
        /// </summary>
        public static string GetString(string content, string key)
        {
            return TryGetString(content, key, out var value) ? value : string.Empty;
        }

        public static bool TryGetDouble(string content, string key, out double value)
        {
            value = 0;
            return TryGetString(content, key, out var text)
                && double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// The numeric value of <paramref name="key"/>, or 0 when the key does not exist or is not a number.
        /// </summary>
        public static double GetDouble(string content, string key)
        {
            return TryGetDouble(content, key, out var value) ? value : 0.0;
        }

        public static bool TryGetInt32(string content, string key, out int value)
        {
            value = 0;

            if (!TryGetString(content, key, out var text))
            {
                return false;
            }

            if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            // Some integer fields are written with a decimal point.
            if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                && number >= int.MinValue && number <= int.MaxValue && Math.Abs(number - Math.Round(number)) < 1e-9)
            {
                value = (int)Math.Round(number);
                return true;
            }

            return false;
        }

        /// <summary>
        /// The integer value of <paramref name="key"/>, or 0 when the key does not exist or is not an integer.
        /// </summary>
        public static int GetInt32(string content, string key)
        {
            return TryGetInt32(content, key, out var value) ? value : 0;
        }

        /// <summary>
        /// Rounds a coordinate to 3 decimal places (0.001 mm), away from zero at the midpoint.
        /// </summary>
        public static double Round3(double value)
        {
            return Math.Round(value, 3, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Reads the value whose length prefix is at <paramref name="lengthPosition"/>.
        /// </summary>
        internal static bool TryReadValueAt(string content, int lengthPosition, out string value)
        {
            value = string.Empty;

            if (lengthPosition >= content.Length)
            {
                return false;
            }

            int length = content[lengthPosition];
            var valueStart = lengthPosition + 1;

            if (length < 0x80)
            {
                if (valueStart + length > content.Length)
                {
                    return false;
                }

                value = content.Substring(valueStart, length);
                return true;
            }

            // The encoding of prefixes for long values is not known; take the run of printable characters and use the
            // prefix only when it agrees with that run.
            var end = valueStart;
            while (end < content.Length && !char.IsControl(content[end]))
            {
                end++;
            }

            var run = end - valueStart;
            value = content.Substring(valueStart, length <= run ? length : run);
            return true;
        }

        private static bool TryGetStringBySeparator(string content, string key, out string value)
        {
            var match = Regex.Match(
                content,
                @"(?<![\w.])" + Regex.Escape(key) + Separator + @"([^\s\x00-\x1F\x7F]+)",
                RegexOptions.CultureInvariant);

            value = match.Success ? match.Groups[1].Value : string.Empty;
            return match.Success;
        }
    }
}