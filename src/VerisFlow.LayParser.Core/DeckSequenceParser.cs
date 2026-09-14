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
        /// <summary>
        /// Parses a deck layout file to extract all defined sequences.
        /// </summary>
        /// <param name="deckLayoutFilePath">The full path to the .lay file.</param>
        /// <returns>A list of SequenceInfo objects containing grouped rack matrices.</returns>
        public static List<SequenceInfo> GetSequenceInfo(string deckLayoutFilePath)
        {
            var sequences = new List<SequenceInfo>();
            string content;
            try
            {
                content = File.ReadAllText(deckLayoutFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading deck layout file for sequences: {ex.Message}");
                return sequences;
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

            var totalCountMatch = Regex.Match(content, @"\bSeq\.Cnt[\s\x00-\x1F\x7F]+(\d+)");
            if (!totalCountMatch.Success || !int.TryParse(totalCountMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seqCount))
            {
                return sequences;
            }

            for (int i = 1; i <= seqCount; i++)
            {
                var seqInfo = new SequenceInfo { Index = i };

                seqInfo.Name = ExtractStringValue(content, i, "Name");

                var cntMatch = Regex.Match(content, $@"\bSeq\.{i}\.Cnt[\s\x00-\x1F\x7F]+(\d+)");
                if (cntMatch.Success && int.TryParse(cntMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
                {
                    seqInfo.TotalCount = count;
                }

                var roMatch = Regex.Match(content, $@"\bSeq\.{i}\.ReadOnly[\s\x00-\x1F\x7F]+(\d+)");
                if (roMatch.Success && int.TryParse(roMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int readOnlyVal))
                {
                    seqInfo.ReadOnly = readOnlyVal == 1;
                }

                seqInfo.Matrices = ExtractSequenceMatrices(content, i);

                sequences.Add(seqInfo);
            }

            return sequences;
        }

        /// <summary>
        /// Extracts a string property value for a given sequence index.
        /// </summary>
        private static string ExtractStringValue(string content, int seqIndex, string property)
        {
            var match = Regex.Match(content, $@"\bSeq\.{seqIndex}\.{property}[\s\x00-\x1F\x7F]+([^\s\x00-\x1F\x7F]+)");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        /// <summary>
        /// Extracts all item positions for a sequence, groups them by ObjId, and orders matrices descending.
        /// </summary>
        private static List<SequenceRackMatrix> ExtractSequenceMatrices(string content, int seqIndex)
        {
            string itemPattern = $@"\bSeq\.{seqIndex}\.Item\.(\d+)\.ObjId[\s\x00-\x1F\x7F]+([^\s\x00-\x1F\x7F]+)[\s\x00-\x1F\x7F]+Seq\.{seqIndex}\.Item\.\1\.PosId[\s\x00-\x1F\x7F]+([^\s\x00-\x1F\x7F]+)";
            var matches = Regex.Matches(content, itemPattern);

            var rawWells = new List<(int Index, string ObjId, string PosId)>();
            foreach (Match match in matches)
            {
                if (int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemIndex))
                {
                    rawWells.Add((itemIndex, match.Groups[2].Value, match.Groups[3].Value));
                }
            }

            // Group by target labware and sort groups descending by ObjId
            var matrices = rawWells
                .GroupBy(w => w.ObjId)
                .OrderByDescending(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var matrix = new SequenceRackMatrix
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
                    };
                    return matrix;
                })
                .ToList();

            return matrices;
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
                string alpha = alphaNumericMatch.Groups[1].Value.ToUpperInvariant();
                int row = 0;
                foreach (char ch in alpha)
                {
                    row = row * 26 + (ch - 'A' + 1);
                }

                int.TryParse(alphaNumericMatch.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int col);
                return (row, col);
            }

            // Fallback for purely numeric linear slot indexing
            if (int.TryParse(posId.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int numericPos))
            {
                return (numericPos, 1);
            }

            return (0, 0);
        }
    }
}