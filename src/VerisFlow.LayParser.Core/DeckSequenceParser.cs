using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace VerisFlow.LayParser.Core
{
    /// <summary>
    /// A parser class responsible for extracting and organizing sequence definitions
    /// and their associated rack matrix data from deck layout files.
    /// </summary>
    public static class DeckSequenceParser
    {
        private static readonly Regex ItemKeyPattern = new Regex(
            @"Seq\.(\d+)\.Item\.(\d+)\.(ObjId|PosId)",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Parses a deck layout file to extract all defined sequences.
        /// </summary>
        /// <param name="deckLayoutFilePath">The full path to the .lay file.</param>
        /// <returns>A list of SequenceInfo objects containing grouped rack matrices.</returns>
        public static List<SequenceInfo> GetSequenceInfo(string deckLayoutFilePath)
        {
            string content;
            try
            {
                content = HxCfgText.ReadAllText(deckLayoutFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading deck layout file for sequences: {ex.Message}");
                return new List<SequenceInfo>();
            }

            return ParseSequenceContent(content);
        }

        /// <summary>
        /// Parses raw layout text content to construct sequence domain models.
        /// </summary>
        /// <param name="content">The text content of the deck layout file.</param>
        /// <returns>A list of structured SequenceInfo objects.</returns>
        public static List<SequenceInfo> ParseSequenceContent(string content)
        {
            var sequences = new List<SequenceInfo>();

            if (string.IsNullOrEmpty(content) || !HxCfgText.TryGetInt32(content, "Seq.Cnt", out var seqCount))
            {
                return sequences;
            }

            // One pass over the content for all items of all sequences.
            var items = ReadItems(content);

            for (var i = 1; i <= seqCount; i++)
            {
                var prefix = "Seq." + i.ToString(CultureInfo.InvariantCulture) + ".";

                var seqInfo = new SequenceInfo
                {
                    Index = i,
                    Name = HxCfgText.GetString(content, prefix + "Name").Trim(),
                    TotalCount = HxCfgText.GetInt32(content, prefix + "Cnt"),
                    ReadOnly = HxCfgText.GetInt32(content, prefix + "ReadOnly") == 1
                };

                seqInfo.Matrices = items.TryGetValue(i, out var sequenceItems)
                    ? BuildMatrices(sequenceItems
                        .Where(item => item.Value.ObjId.Length > 0)
                        .Select(item => (item.Key, item.Value.ObjId, item.Value.PosId)))
                    : ExtractSequenceMatricesBySeparator(content, i);

                sequences.Add(seqInfo);
            }

            return sequences;
        }

        /// <summary>
        /// Reads all "Seq.S.Item.N.ObjId/PosId" values, keyed by sequence and item index.
        /// </summary>
        private static Dictionary<int, SortedDictionary<int, SequenceItem>> ReadItems(string content)
        {
            var result = new Dictionary<int, SortedDictionary<int, SequenceItem>>();

            foreach (Match match in ItemKeyPattern.Matches(content))
            {
                var key = match.Value;
                var start = match.Index;

                // Only keys preceded by their own length prefix are real keys.
                if (start == 0 || content[start - 1] != (char)key.Length)
                {
                    continue;
                }

                if (!HxCfgText.TryReadValueAt(content, start + key.Length, out var value)
                    || !int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seqIndex)
                    || !int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var itemIndex))
                {
                    continue;
                }

                if (!result.TryGetValue(seqIndex, out var sequenceItems))
                {
                    sequenceItems = new SortedDictionary<int, SequenceItem>();
                    result[seqIndex] = sequenceItems;
                }

                if (!sequenceItems.TryGetValue(itemIndex, out var item))
                {
                    item = new SequenceItem();
                    sequenceItems[itemIndex] = item;
                }

                if (match.Groups[3].Value == "ObjId")
                {
                    item.ObjId = value.Trim();
                }
                else
                {
                    item.PosId = value.Trim();
                }
            }

            return result;
        }

        /// <summary>
        /// Compatibility path for content without length prefixes (behavior of versions before 0.4.0).
        /// </summary>
        private static List<SequenceRackMatrix> ExtractSequenceMatricesBySeparator(string content, int seqIndex)
        {
            var itemPattern = $@"\bSeq\.{seqIndex}\.Item\.(\d+)\.ObjId[\s\x00-\x1F\x7F]+([^\s\x00-\x1F\x7F]+)[\s\x00-\x1F\x7F]+Seq\.{seqIndex}\.Item\.\1\.PosId[\s\x00-\x1F\x7F]+([^\s\x00-\x1F\x7F]+)";
            var rawWells = new List<(int Index, string ObjId, string PosId)>();

            foreach (Match match in Regex.Matches(content, itemPattern))
            {
                if (int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var itemIndex))
                {
                    rawWells.Add((itemIndex, match.Groups[2].Value, match.Groups[3].Value));
                }
            }

            return BuildMatrices(rawWells);
        }

        /// <summary>
        /// Groups item positions by ObjId, ordering matrices descending by ObjId and positions by item index.
        /// </summary>
        private static List<SequenceRackMatrix> BuildMatrices(IEnumerable<(int Index, string ObjId, string PosId)> rawWells)
        {
            return rawWells
                .GroupBy(w => w.ObjId)
                .OrderByDescending(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new SequenceRackMatrix
                {
                    ObjId = group.Key,
                    TotalWells = group.Count(),
                    Positions = group.OrderBy(p => p.Index).Select(p =>
                    {
                        var (row, col) = ParseRowAndColumn(p.PosId);
                        return new SequenceWellPosition
                        {
                            SequenceIndex = p.Index,
                            PosId = p.PosId,
                            RowIndex = row,
                            ColumnIndex = col
                        };
                    }).ToList()
                })
                .ToList();
        }

        /// <summary>
        /// Converts an alphanumeric or numeric position identifier into 1-based matrix coordinates.
        /// </summary>
        /// <param name="posId">The raw position string (e.g. "A1", "H12", or "5").</param>
        /// <returns>A tuple containing the calculated 1-based row and column index.</returns>
        private static (int Row, int Column) ParseRowAndColumn(string posId)
        {
            if (string.IsNullOrWhiteSpace(posId))
            {
                return (0, 0);
            }

            // Handle standard alphanumeric grid coordinate notation like "A1" or "H12"
            var alphaNumericMatch = Regex.Match(posId.Trim(), @"^([A-Za-z]+)(\d+)$");
            if (alphaNumericMatch.Success)
            {
                var alpha = alphaNumericMatch.Groups[1].Value.ToUpperInvariant();
                var row = 0;
                foreach (var ch in alpha)
                {
                    row = row * 26 + (ch - 'A' + 1);
                }

                int.TryParse(alphaNumericMatch.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var col);
                return (row, col);
            }

            // Fallback for purely numeric linear slot indexing
            if (int.TryParse(posId.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericPos))
            {
                return (numericPos, 1);
            }

            return (0, 0);
        }

        private sealed class SequenceItem
        {
            public string ObjId { get; set; } = string.Empty;
            public string PosId { get; set; } = string.Empty;
        }
    }
}