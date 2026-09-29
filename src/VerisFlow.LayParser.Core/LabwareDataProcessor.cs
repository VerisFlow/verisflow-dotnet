using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace VerisFlow.LayParser.Core
{
    /// <summary>
    /// A service class responsible for processing the raw labware data
    /// into its final, calculated form.
    /// </summary>
    public static class LabwareDataProcessor
    {
        private const double TipRackContainerBaseThreshold = -10;
        private const int MaxExplicitPositions = 10000;
        private const int MaxSegments = 100;
        private const int MaxSites = 1000;

        /// <summary>
        /// Processes a list of raw LabwareInfo objects to calculate final coordinates and geometry.
        /// </summary>
        /// <remarks>
        /// FinalX/FinalY are TForm.3 (see <see cref="ProcessedLabwareInfo.ReferenceKind"/>). FinalZ starts at ZTrans:
        /// ZTransValue 0 adds the container BaseMM; ZTransValue 1 adds Cntr.1.base and the container BaseMM;
        /// ZTransValue 2 (carriers) uses ZTrans as is. Each labware or container file is read once per call.
        /// </remarks>
        /// <param name="rawData">The list of raw LabwareInfo objects parsed from the file.</param>
        /// <returns>A list of ProcessedLabwareInfo objects with the final calculated data.</returns>
        public static List<ProcessedLabwareInfo> Process(List<LabwareInfo> rawData)
        {
#if NET6_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(rawData);
#else
            if (rawData == null)
            {
                throw new ArgumentNullException(nameof(rawData));
            }
#endif

            var files = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            return rawData.Select(raw => ProcessLabware(raw, files)).ToList();
        }

        private static ProcessedLabwareInfo ProcessLabware(LabwareInfo raw, Dictionary<string, string?> files)
        {
            var notes = new List<string>();

            var finalX = raw.TForm3.X;
            var finalY = raw.TForm3.Y;
            var finalZ = raw.ZTrans;

            var labwareType = GetLabwareType(raw);
            var isLoadable = labwareType == LabwareType.Carrier && !string.IsNullOrEmpty(raw.SiteId);

            var definition = ReadFileCached(raw.FilePath, files);
            var properties = definition != null ? ReadLabwareProperties(definition) : new LabwareProperties();

            if (definition == null)
            {
                Console.WriteLine($"File not found: {raw.FilePath}");
                notes.Add(FormattableString.Invariant(
                    $"The labware definition '{raw.FilePath}' could not be read; dimensions, positions, and container data are unavailable."));
            }

            var isTipRack = properties.CntrBase < TipRackContainerBaseThreshold;
            var alphaIndex = properties.IxIndex == 1;
            var row = properties.Rows;
            var column = (row > 0 && properties.Columns == 0) ? 1 : properties.Columns;
            var parentId = IsDefault(raw.Template) ? string.Empty : raw.Template;

            ContainerProperties? containerProperties = null;
            if (!string.IsNullOrEmpty(properties.CntrFile))
            {
                var containerDefinition = ReadFileCached(properties.CntrFile, files);
                if (containerDefinition != null)
                {
                    containerProperties = ReadContainerProperties(containerDefinition);
                }
                else
                {
                    Console.WriteLine($"Container file not found: {properties.CntrFile}");
                }
            }

            // --- Z: unchanged algorithm ---
            var zCalculationIncomplete = false;
            var validationWarning = string.Empty;

            if (raw.ZTransValue == 0)
            {
                if (containerProperties != null)
                {
                    finalZ += containerProperties.BaseMM;
                }
                else
                {
                    zCalculationIncomplete = true;
                    validationWarning = FormattableString.Invariant(
                        $"Critical physics warning: ZTransValue is 0 for Labware '{raw.Id}', but the container file is missing or unreadable. BaseMM could not be applied. FinalZ is likely incorrect and unsafe for physical execution.");
                    Console.WriteLine(validationWarning);
                }
            }
            else if (raw.ZTransValue == 1)
            {
                if (containerProperties != null)
                {
                    finalZ += properties.CntrBase;
                    finalZ += containerProperties.BaseMM;
                }
                else
                {
                    zCalculationIncomplete = true;
                    validationWarning = FormattableString.Invariant(
                        $"Critical physics warning: ZTransValue is 1 for Labware '{raw.Id}', but the container file is missing or unreadable. Cntr.1.base and BaseMM could not be applied. FinalZ is likely incorrect and unsafe for physical execution.");
                    Console.WriteLine(validationWarning);
                }
            }
            else if (raw.ZTransValue != 2)
            {
                // Value 2 is used by carriers: ZTrans is their height as is. Anything else is unknown.
                notes.Add(FormattableString.Invariant(
                    $"ZTransValue {raw.ZTransValue} is not a known value (0, 1, or 2); FinalZ is ZTrans without further offsets."));
            }

            // --- Outline and positions ---
            var isRack = labwareType == LabwareType.Rack || labwareType == LabwareType.RackCarrier;
            var reference = isRack ? PositionReference.FirstPosition : PositionReference.Origin;
            var originX = finalX;
            var originY = finalY;
            var positions = new List<LabwarePosition>();

            if (isRack && definition != null)
            {
                var boundaryX = properties.UseBoundary ? properties.BoundaryX : 0;
                var boundaryY = properties.UseBoundary ? properties.BoundaryY : 0;

                if (!properties.UseBoundary)
                {
                    notes.Add("UseBndry is 0: the first position is assumed to lie at the rack's boundary origin (BndryX/BndryY treated as 0). This rule is not verified.");
                }

                if (properties.Stagger != 0)
                {
                    notes.Add("The rack uses a staggered position layout, which is not supported; positions are computed as a regular grid.");
                }

                if (properties.ExplicitPositions.Count > 0)
                {
                    var first = properties.ExplicitPositions[0];
                    originX = finalX - boundaryX - first.X;
                    originY = finalY - boundaryY - first.Y;

                    foreach (var definitionPosition in properties.ExplicitPositions)
                    {
                        positions.Add(new LabwarePosition
                        {
                            Index = definitionPosition.Index,
                            Name = definitionPosition.Id.Length > 0
                                ? definitionPosition.Id
                                : definitionPosition.Index.ToString(CultureInfo.InvariantCulture),
                            Row = definitionPosition.Index,
                            Column = 1,
                            X = HxCfgText.Round3(finalX + definitionPosition.X - first.X),
                            Y = HxCfgText.Round3(finalY + definitionPosition.Y - first.Y)
                        });
                    }
                }
                else
                {
                    // The grid extends from the front-left position at the boundary point; position 1 (A1) is at the back-left.
                    originX = finalX - boundaryX;
                    originY = finalY - boundaryY - Math.Max(0, row - 1) * properties.PitchY;

                    if (row > 0 && column > 0)
                    {
                        for (var c = 1; c <= column; c++)
                        {
                            for (var r = 1; r <= row; r++)
                            {
                                var index = (c - 1) * row + r;
                                positions.Add(new LabwarePosition
                                {
                                    Index = index,
                                    Name = alphaIndex
                                        ? RowName(r) + c.ToString(CultureInfo.InvariantCulture)
                                        : index.ToString(CultureInfo.InvariantCulture),
                                    Row = r,
                                    Column = c,
                                    X = HxCfgText.Round3(finalX + (c - 1) * properties.PitchX),
                                    Y = HxCfgText.Round3(finalY - (r - 1) * properties.PitchY)
                                });
                            }
                        }
                    }
                }
            }

            if (IsRotated(raw))
            {
                notes.Add("The labware is rotated (Angle or TForm.1/TForm.2 is not the identity). Rotation is not applied; the outline and positions are unrotated.");
            }

            originX = HxCfgText.Round3(originX);
            originY = HxCfgText.Round3(originY);
            finalZ = HxCfgText.Round3(finalZ);

            // --- Carrier sites and track ---
            var sites = properties.Sites.Select(site => new CarrierSite
            {
                Index = site.Index,
                Id = site.Id,
                X = site.X,
                Y = site.Y,
                Z = site.Z,
                Dx = site.Dx,
                Dy = site.Dy,
                StackSize = site.StackSize,
                LabwareFile = site.LabwareFile,
                AbsoluteX = HxCfgText.Round3(originX + site.X),
                AbsoluteY = HxCfgText.Round3(originY + site.Y),
                AbsoluteZ = HxCfgText.Round3(finalZ + site.Z)
            }).ToList();

            int? track = null;
            int? trackWidth = null;
            if (parentId.Length == 0 && DeckGeometry.TryParseTrackSite(raw.SiteId, out var width, out var firstTrack))
            {
                track = firstTrack;
                trackWidth = width;
            }

            return new ProcessedLabwareInfo
            {
                Index = raw.Index,
                Id = raw.Id,
                FilePath = raw.FilePath,
                FinalX = finalX,
                FinalY = finalY,
                FinalZ = finalZ,
                Template = parentId,
                LabwareType = labwareType,
                Loadable = isLoadable,
                Dx = properties.DimDx,
                Dy = properties.DimDy,
                Column = column,
                Row = row,
                AlphaIndex = alphaIndex,
                TipRack = isTipRack,
                ContainerProperties = containerProperties,
                IsZCalculationIncomplete = zCalculationIncomplete,
                ValidationWarning = validationWarning,

                SiteId = raw.SiteId,
                ParentId = parentId,
                StackId = raw.StackId,
                Angle = raw.Angle,
                Dz = properties.DimDz,
                ZTrans = raw.ZTrans,
                ZTransValue = raw.HasZTransValue ? raw.ZTransValue : (double?)null,
                ContainerBaseOffset = properties.CntrBase,
                ContainerBaseMM = containerProperties?.BaseMM,
                ReferenceKind = reference,
                OriginX = originX,
                OriginY = originY,
                BoundaryX = properties.BoundaryX,
                BoundaryY = properties.BoundaryY,
                UseBoundary = properties.UseBoundary,
                PitchX = properties.PitchX,
                PitchY = properties.PitchY,
                HoleDx = properties.HoleDx,
                HoleDy = properties.HoleDy,
                HoleShape = properties.HoleShape,
                HasExplicitPositions = properties.ExplicitPositions.Count > 0,
                Positions = positions,
                Sites = sites,
                Track = track,
                TrackWidth = trackWidth,
                GeometryNotes = notes
            };
        }

        private static LabwareType GetLabwareType(LabwareInfo raw)
        {
            string extension;
            try
            {
                extension = Path.GetExtension(raw.FilePath)?.ToLowerInvariant() ?? string.Empty;
            }
            catch (ArgumentException)
            {
                extension = string.Empty;
            }

            var labwareType = LabwareType.Unknown;
            switch (extension)
            {
                case ".tml":
                    labwareType = LabwareType.Carrier;
                    break;
                case ".rck":
                    labwareType = LabwareType.Rack;
                    break;
                case ".ctr":
                    labwareType = LabwareType.Container;
                    break;
            }

            if (labwareType == LabwareType.Rack && IsDefault(raw.Template))
            {
                labwareType = LabwareType.RackCarrier;
            }

            return labwareType;
        }

        private static bool IsDefault(string template)
        {
            return string.Equals(template, "default", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// TForm.1/TForm.2 of unrotated labware are (1,0,0)/(0,1,0). All-zero vectors mean the layout had no TForm entries.
        /// </summary>
        private static bool IsRotated(LabwareInfo raw)
        {
            if (raw.Angle != 0)
            {
                return true;
            }

            return !IsVectorOrMissing(raw.TForm1, 1, 0, 0) || !IsVectorOrMissing(raw.TForm2, 0, 1, 0);
        }

        private static bool IsVectorOrMissing(TFormVector vector, double x, double y, double z)
        {
            var missing = vector.X == 0 && vector.Y == 0 && vector.Z == 0;
            return missing || (vector.X == x && vector.Y == y && vector.Z == z);
        }

        /// <summary>
        /// "A".."Z", then "AA", "AB", ... for 1-based row numbers.
        /// </summary>
        private static string RowName(int row)
        {
            var name = new StringBuilder();
            while (row > 0)
            {
                row--;
                name.Insert(0, (char)('A' + row % 26));
                row /= 26;
            }

            return name.ToString();
        }

        /// <summary>
        /// Reads a file once per processing run. Returns null (also cached) when it is missing or unreadable.
        /// </summary>
        private static string? ReadFileCached(string path, Dictionary<string, string?> files)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (files.TryGetValue(path, out var cached))
            {
                return cached;
            }

            string? content = null;
            try
            {
                if (File.Exists(path))
                {
                    content = HxCfgText.ReadAllText(path);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not read {path}: {ex.Message}");
            }

            files[path] = content;
            return content;
        }

        /// <summary>
        /// Extracts key properties from the content of a labware file (.rck, .tml).
        /// Missing values keep their defaults (0 / empty).
        /// </summary>
        private static LabwareProperties ReadLabwareProperties(string content)
        {
            var properties = new LabwareProperties
            {
                DimDx = HxCfgText.GetDouble(content, "Dim.Dx"),
                DimDy = HxCfgText.GetDouble(content, "Dim.Dy"),
                DimDz = HxCfgText.GetDouble(content, "Dim.Dz"),
                CntrBase = HxCfgText.GetDouble(content, "Cntr.1.base"),
                IxIndex = HxCfgText.GetInt32(content, "IX.Index"),
                HoleCount = HxCfgText.GetInt32(content, "HoleCnt"),
                BoundaryX = HxCfgText.GetDouble(content, "BndryX"),
                BoundaryY = HxCfgText.GetDouble(content, "BndryY"),
                UseBoundary = HxCfgText.GetInt32(content, "UseBndry") == 1,
                PitchX = HxCfgText.GetDouble(content, "Dx"),
                PitchY = HxCfgText.GetDouble(content, "Dy"),
                HoleDx = HxCfgText.GetDouble(content, "Hole.X"),
                HoleDy = HxCfgText.GetDouble(content, "Hole.Y"),
                HoleShape = HxCfgText.GetInt32(content, "Hole.Shape"),
                Stagger = HxCfgText.GetInt32(content, "Stagger"),
                DataType = HxCfgText.GetInt32(content, "DataType")
            };

            var rowsFound = HxCfgText.TryGetInt32(content, "Rows", out var rows);
            var columnsFound = HxCfgText.TryGetInt32(content, "Columns", out var columns);

            if (rowsFound)
            {
                properties.Rows = rows;
            }

            if (columnsFound)
            {
                properties.Columns = columns;
            }

            // Racks without Rows and Columns (e.g. troughs) declare their positions with HoleCnt.
            if (!rowsFound && !columnsFound && properties.HoleCount > 0)
            {
                properties.Rows = properties.HoleCount;
            }

            if (HxCfgText.TryGetString(content, "Cntr.1.file", out var containerFile) && containerFile.Trim().Length > 0)
            {
                properties.CntrFile = DeckLayoutParser.ResolveLabwarePath(containerFile);
            }

            properties.ExplicitPositions = ReadExplicitPositions(content, properties.HoleCount);
            properties.Sites = ReadSites(content);

            return properties;
        }

        /// <summary>
        /// Reads "N.ID", "N.X", "N.Y" positions, stopping at the first missing position.
        /// </summary>
        private static List<RackPositionDefinition> ReadExplicitPositions(string content, int holeCount)
        {
            var positions = new List<RackPositionDefinition>();
            var limit = holeCount > 0 ? Math.Min(holeCount, MaxExplicitPositions) : MaxExplicitPositions;

            for (var n = 1; n <= limit; n++)
            {
                var prefix = n.ToString(CultureInfo.InvariantCulture) + ".";

                if (!HxCfgText.TryGetDouble(content, prefix + "X", out var x) || !HxCfgText.TryGetDouble(content, prefix + "Y", out var y))
                {
                    break;
                }

                positions.Add(new RackPositionDefinition
                {
                    Index = n,
                    Id = HxCfgText.GetString(content, prefix + "ID").Trim(),
                    X = x,
                    Y = y
                });
            }

            return positions;
        }

        /// <summary>
        /// Reads the sites of a carrier template (Site.Cnt, Site.N.*).
        /// </summary>
        private static List<CarrierSite> ReadSites(string content)
        {
            var sites = new List<CarrierSite>();

            if (!HxCfgText.TryGetInt32(content, "Site.Cnt", out var count) || count <= 0)
            {
                return sites;
            }

            for (var n = 1; n <= Math.Min(count, MaxSites); n++)
            {
                var prefix = "Site." + n.ToString(CultureInfo.InvariantCulture) + ".";

                sites.Add(new CarrierSite
                {
                    Index = n,
                    Id = HxCfgText.GetString(content, prefix + "Id").Trim(),
                    X = HxCfgText.GetDouble(content, prefix + "X"),
                    Y = HxCfgText.GetDouble(content, prefix + "Y"),
                    Z = HxCfgText.GetDouble(content, prefix + "Z"),
                    Dx = HxCfgText.GetDouble(content, prefix + "Dx"),
                    Dy = HxCfgText.GetDouble(content, prefix + "Dy"),
                    StackSize = HxCfgText.TryGetInt32(content, prefix + "StackSize", out var stackSize) ? stackSize : 1,
                    LabwareFile = HxCfgText.GetString(content, prefix + "LabwareFile").Trim()
                });
            }

            return sites;
        }

        /// <summary>
        /// Extracts key properties from the content of a container file (.ctr).
        /// </summary>
        private static ContainerProperties ReadContainerProperties(string content)
        {
            var properties = new ContainerProperties
            {
                DimDx = HxCfgText.GetDouble(content, "Dim.Dx"),
                DimDy = HxCfgText.GetDouble(content, "Dim.Dy"),
                BaseMM = HxCfgText.GetDouble(content, "BaseMM")
            };

            if (!HxCfgText.TryGetInt32(content, "Segments", out var segmentsCount) || segmentsCount <= 0)
            {
                return properties;
            }

            properties.SegmentsCount = segmentsCount;

            for (var i = 1; i <= Math.Min(segmentsCount, MaxSegments); i++)
            {
                var prefix = i.ToString(CultureInfo.InvariantCulture) + ".";

                properties.Segments.Add(new ContainerSegment
                {
                    Index = i,
                    Dx = HxCfgText.GetDouble(content, prefix + "DX"),
                    Dy = HxCfgText.GetDouble(content, prefix + "DY"),
                    Dz = HxCfgText.GetDouble(content, prefix + "DZ"),
                    Max = HxCfgText.GetDouble(content, prefix + "Max"),
                    Min = HxCfgText.GetDouble(content, prefix + "Min"),
                    Shape = HxCfgText.GetInt32(content, prefix + "Shape"),
                    EqnOfVol = HxCfgText.GetString(content, prefix + "EqnOfVol").Trim()
                });
            }

            return properties;
        }
    }
}