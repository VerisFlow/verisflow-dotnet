using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace VerisFlow.LayParser.Core
{
    public static class DeckLayoutParser
    {
        public const string HamiltonLabwareBasePath = @"C:\Program Files (x86)\HAMILTON\LabWare\";

        private static readonly Regex DeckHeaderPattern = new Regex(@"\bDECKLAY,([A-Za-z0-9_]+)", RegexOptions.CultureInvariant);

        /// <summary>
        /// Reads deck layout data from a .lay file, extracting instrument and all labware instances in a single pass.
        /// </summary>
        /// <param name="deckLayoutFilePath">The full path to the .lay file.</param>
        /// <returns>A DeckData object containing the instrument name and labware list. Empty if the file cannot be read.</returns>
        public static DeckData GetDeckData(string deckLayoutFilePath)
        {
            string content;
            try
            {
                content = HxCfgText.ReadAllText(deckLayoutFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading deck layout file: {ex.Message}");
                return new DeckData();
            }

            return ParseDeckContent(content);
        }

        /// <summary>
        /// Parses the content of a .lay file (read with <see cref="HxCfgText.ReadAllText"/>).
        /// </summary>
        public static DeckData ParseDeckContent(string content)
        {
            var deckData = new DeckData();
            if (string.IsNullOrEmpty(content))
            {
                return deckData;
            }

            if (HxCfgText.TryGetString(content, "Instrument", out var instrument) && instrument.Trim().Length > 0)
            {
                deckData.Instrument = instrument.Trim();
            }
            else
            {
                var headerMatch = DeckHeaderPattern.Match(content);
                if (headerMatch.Success)
                {
                    deckData.Instrument = headerMatch.Groups[1].Value;
                }
            }

            if (!HxCfgText.TryGetInt32(content, "Labware.Cnt", out var labwareCount))
            {
                return deckData;
            }

            for (var i = 1; i <= labwareCount; i++)
            {
                var prefix = "Labware." + i.ToString(CultureInfo.InvariantCulture) + ".";

                var labware = new LabwareInfo
                {
                    Index = i,
                    FilePath = ResolveLabwarePath(HxCfgText.GetString(content, prefix + "File")),
                    Id = HxCfgText.GetString(content, prefix + "Id").Trim(),
                    SiteId = HxCfgText.GetString(content, prefix + "SiteId").Trim(),
                    Template = HxCfgText.GetString(content, prefix + "Template").Trim(),
                    StackId = HxCfgText.GetString(content, prefix + "StackID").Trim(),
                    ZTrans = ReadRounded(content, prefix + "ZTrans"),
                    Angle = ReadRounded(content, prefix + "Angle"),
                    TForm1 = ReadTFormVector(content, prefix, 1),
                    TForm2 = ReadTFormVector(content, prefix, 2),
                    TForm3 = ReadTFormVector(content, prefix, 3)
                };

                if (HxCfgText.TryGetDouble(content, prefix + "ZTransValue", out var zTransValue))
                {
                    labware.ZTransValue = HxCfgText.Round3(zTransValue);
                    labware.HasZTransValue = true;
                }

                deckData.Labware.Add(labware);
            }

            return deckData;
        }

        /// <summary>
        /// Parses a deck layout file to extract detailed labware information based on specific rules.
        /// </summary>
        /// <param name="deckLayoutFilePath">The full path to the .lay file.</param>
        /// <returns>A list of LabwareInfo objects.</returns>
        public static List<LabwareInfo> GetLabwareInfo(string deckLayoutFilePath)
        {
            return GetDeckData(deckLayoutFilePath).Labware;
        }

        /// <summary>
        /// Resolves a labware file path from a layout or rack file: relative paths are relative to the Hamilton labware folder.
        /// </summary>
        internal static string ResolveLabwarePath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return string.Empty;
            }

            var path = rawPath.Trim();

            try
            {
                return Path.IsPathRooted(path) ? path : Path.Combine(HamiltonLabwareBasePath, path);
            }
            catch (ArgumentException)
            {
                // Invalid path characters; report the path as written.
                return path;
            }
        }

        private static double ReadRounded(string content, string key)
        {
            return HxCfgText.TryGetDouble(content, key, out var value) ? HxCfgText.Round3(value) : 0.0;
        }

        private static TFormVector ReadTFormVector(string content, string labwarePrefix, int tformIndex)
        {
            var prefix = labwarePrefix + "TForm." + tformIndex.ToString(CultureInfo.InvariantCulture) + ".";

            return new TFormVector
            {
                X = ReadRounded(content, prefix + "X"),
                Y = ReadRounded(content, prefix + "Y"),
                Z = ReadRounded(content, prefix + "Z")
            };
        }
    }
}