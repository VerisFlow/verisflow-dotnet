using System;
using System.Collections.Generic;
using System.Globalization;

namespace VerisFlow.LayParser.Core
{
    /// <summary>
    /// Enum to define the type of labware.
    /// </summary>
    public enum LabwareType
    {
        Carrier,
        RackCarrier,
        Rack,
        Container,
        Unknown
    }

    /// <summary>
    /// What <see cref="ProcessedLabwareInfo.FinalX"/> and <see cref="ProcessedLabwareInfo.FinalY"/> refer to.
    /// </summary>
    public enum PositionReference
    {
        /// <summary>The front-left corner of the labware outline (carriers and other non-rack labware).</summary>
        Origin,

        /// <summary>The center of position 1, e.g. A1 at the back-left of a plate (racks and rack carriers).</summary>
        FirstPosition
    }

    /// <summary>
    /// A position (well, tip, tube, slot) of a rack with its center on the deck.
    /// </summary>
    public class LabwarePosition
    {
        /// <summary>
        /// 1-based position number. For grids, numbering runs down each column from A1 (1 = A1, 2 = B1, ...);
        /// this order is assumed, not read from the rack file.
        /// </summary>
        public int Index { get; set; }

        /// <summary>"A1".."H12" for alphanumerically indexed racks; the position number otherwise.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>1-based row, counted from the back. For explicit positions, equal to <see cref="Index"/>.</summary>
        public int Row { get; set; }

        /// <summary>1-based column, counted from the left. 1 for explicit positions.</summary>
        public int Column { get; set; }

        public double X { get; set; }
        public double Y { get; set; }

        public override string ToString()
        {
            return FormattableString.Invariant($"{Name} ({X:F3}, {Y:F3})");
        }
    }

    /// <summary>
    /// Represents the final, processed data for a single piece of labware,
    /// including the calculated final coordinates and type information.
    /// </summary>
    public class ProcessedLabwareInfo
    {
        public int Index { get; set; }
        public string Id { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Reference point on the deck; see <see cref="ReferenceKind"/> for what it refers to.</summary>
        public double FinalX { get; set; }

        /// <summary>Reference point on the deck; see <see cref="ReferenceKind"/> for what it refers to.</summary>
        public double FinalY { get; set; }

        /// <summary>
        /// ZTransValue 0: ZTrans + container BaseMM. ZTransValue 1: ZTrans + Cntr.1.base + container BaseMM.
        /// ZTransValue 2 (carriers): ZTrans.
        /// </summary>
        public double FinalZ { get; set; }

        /// <summary>Parent carrier id; empty for carriers and rack carriers. Same value as <see cref="ParentId"/>.</summary>
        public string Template { get; set; } = string.Empty;

        public LabwareType LabwareType { get; set; }
        public bool Loadable { get; set; }

        /// <summary>Outline width (Dim.Dx).</summary>
        public double Dx { get; set; }

        /// <summary>Outline depth (Dim.Dy).</summary>
        public double Dy { get; set; }

        public int Column { get; set; }
        public int Row { get; set; }
        public bool AlphaIndex { get; set; }
        public bool TipRack { get; set; }
        public ContainerProperties? ContainerProperties { get; set; }
        public bool IsZCalculationIncomplete { get; set; }
        public string ValidationWarning { get; set; } = string.Empty;

        // ------------------------------------------------------------------ Added in 0.4.0

        /// <summary>SiteId from the layout; for carriers placed on tracks, "{width}T-{track}".</summary>
        public string SiteId { get; set; } = string.Empty;

        /// <summary>Id of the carrier this labware is placed on; empty for carriers and rack carriers.</summary>
        public string ParentId { get; set; } = string.Empty;

        /// <summary>Stack the labware belongs to; empty when not stacked.</summary>
        public string StackId { get; set; } = string.Empty;

        /// <summary>Rotation angle from the layout. Rotation is not applied to the outline or positions.</summary>
        public double Angle { get; set; }

        /// <summary>Outline height (Dim.Dz).</summary>
        public double Dz { get; set; }

        /// <summary>ZTrans from the layout.</summary>
        public double ZTrans { get; set; }

        /// <summary>ZTransValue from the layout; null when the layout has none.</summary>
        public double? ZTransValue { get; set; }

        /// <summary>Container base offset from the rack file (Cntr.1.base).</summary>
        public double ContainerBaseOffset { get; set; }

        /// <summary>BaseMM of the container file; null when there is no readable container file.</summary>
        public double? ContainerBaseMM { get; set; }

        /// <summary>What <see cref="FinalX"/> and <see cref="FinalY"/> refer to.</summary>
        public PositionReference ReferenceKind { get; set; }

        /// <summary>Front-left corner of the outline (<see cref="Dx"/> × <see cref="Dy"/>) on the deck.</summary>
        public double OriginX { get; set; }

        /// <summary>Front-left corner of the outline (<see cref="Dx"/> × <see cref="Dy"/>) on the deck.</summary>
        public double OriginY { get; set; }

        public double BoundaryX { get; set; }
        public double BoundaryY { get; set; }
        public bool UseBoundary { get; set; }

        /// <summary>Distance between columns of a grid rack.</summary>
        public double PitchX { get; set; }

        /// <summary>Distance between rows of a grid rack.</summary>
        public double PitchY { get; set; }

        public double HoleDx { get; set; }
        public double HoleDy { get; set; }
        public int HoleShape { get; set; }

        /// <summary>True when the rack defines its positions explicitly instead of as a grid.</summary>
        public bool HasExplicitPositions { get; set; }

        /// <summary>Position centers of racks; empty for carriers or when the rack file could not be read.</summary>
        public List<LabwarePosition> Positions { get; set; } = new List<LabwarePosition>();

        /// <summary>Sites of carriers with absolute coordinates; empty for other labware.</summary>
        public List<CarrierSite> Sites { get; set; } = new List<CarrierSite>();

        /// <summary>First track of a carrier placed on tracks (from SiteId); null otherwise.</summary>
        public int? Track { get; set; }

        /// <summary>Width in tracks of a carrier placed on tracks (from SiteId); null otherwise.</summary>
        public int? TrackWidth { get; set; }

        /// <summary>
        /// Assumptions and limitations that apply to this labware's geometry (e.g. unverified rules, unsupported
        /// rotation). Empty when the geometry follows verified rules only.
        /// </summary>
        public List<string> GeometryNotes { get; set; } = new List<string>();

        /// <summary>
        /// Finds a position by name ("A1") or number ("5"), case-insensitively. Returns null when there is none.
        /// </summary>
        public LabwarePosition? GetPosition(string nameOrNumber)
        {
            if (string.IsNullOrWhiteSpace(nameOrNumber))
            {
                return null;
            }

            var key = nameOrNumber.Trim();

            foreach (var position in Positions)
            {
                if (string.Equals(position.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    return position;
                }
            }

            if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                foreach (var position in Positions)
                {
                    if (position.Index == number)
                    {
                        return position;
                    }
                }
            }

            return null;
        }
    }
}