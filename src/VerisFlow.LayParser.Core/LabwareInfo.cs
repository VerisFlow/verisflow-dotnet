using System;
using System.Text;
using System.Collections.Generic;

namespace VerisFlow.LayParser.Core
{
    /// <summary>
    /// Represents the parsed deck layout data including the instrument and its labware items.
    /// </summary>
    public class DeckData
    {
        public string Instrument { get; set; } = string.Empty;
        public List<LabwareInfo> Labware { get; set; } = new List<LabwareInfo>();
    }

    /// <summary>
    /// Represents a 3D vector for TForm data.
    /// </summary>
    public class TFormVector
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public override string ToString()
        {
            return FormattableString.Invariant($"X={X:F3}, Y={Y:F3}, Z={Z:F3}");
        }
    }

    /// <summary>
    /// Represents detailed information about a single piece of labware, as written in the layout file.
    /// </summary>
    public class LabwareInfo
    {
        public int Index { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string SiteId { get; set; } = string.Empty;
        public TFormVector TForm1 { get; set; } = new TFormVector();
        public TFormVector TForm2 { get; set; } = new TFormVector();
        public TFormVector TForm3 { get; set; } = new TFormVector();

        /// <summary>
        /// Selects how ZTrans is interpreted: 0 and 1 for racks (see <see cref="LabwareDataProcessor"/>),
        /// 2 on carriers (ZTrans is used as is). 0 when the layout has no value; see <see cref="HasZTransValue"/>.
        /// </summary>
        public double ZTransValue { get; set; }

        /// <summary>True when the layout contains a ZTransValue for this labware.</summary>
        public bool HasZTransValue { get; set; }

        public double ZTrans { get; set; }
        public string Template { get; set; } = string.Empty;

        /// <summary>Rotation angle from the layout (Labware.N.Angle). 0 for unrotated labware.</summary>
        public double Angle { get; set; }

        /// <summary>Stack the labware belongs to (Labware.N.StackID); empty when not stacked.</summary>
        public string StackId { get; set; } = string.Empty;

        public string GetTFormAsMarkdown()
        {
            var sb = new StringBuilder();
            sb.Append(FormattableString.Invariant($"**1:** {TForm1}<br>"));
            sb.Append(FormattableString.Invariant($"**2:** {TForm2}<br>"));
            sb.Append(FormattableString.Invariant($"**3:** {TForm3}"));
            return sb.ToString();
        }
    }

    /// <summary>
    /// A position defined explicitly in a rack file ("N.ID", "N.X", "N.Y"), relative to the rack's boundary point.
    /// </summary>
    public class RackPositionDefinition
    {
        /// <summary>1-based position number (N).</summary>
        public int Index { get; set; }

        /// <summary>Position identifier (N.ID); usually equal to <see cref="Index"/>.</summary>
        public string Id { get; set; } = string.Empty;

        public double X { get; set; }
        public double Y { get; set; }
    }

    /// <summary>
    /// A site of a carrier template (.tml): where labware can be placed on the carrier.
    /// </summary>
    public class CarrierSite
    {
        /// <summary>1-based index of the site definition (Site.N).</summary>
        public int Index { get; set; }

        /// <summary>
        /// Site identifier as used by labware SiteIds (Site.N.Id). Not necessarily equal to <see cref="Index"/>.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Offset of the site's front-left corner from the carrier origin.</summary>
        public double X { get; set; }

        /// <summary>Offset of the site's front-left corner from the carrier origin.</summary>
        public double Y { get; set; }

        /// <summary>Height of the site above the carrier base.</summary>
        public double Z { get; set; }

        public double Dx { get; set; }
        public double Dy { get; set; }
        public int StackSize { get; set; } = 1;
        public string LabwareFile { get; set; } = string.Empty;

        /// <summary>Deck coordinate of the site's front-left corner. Set on processed labware only.</summary>
        public double AbsoluteX { get; set; }

        /// <summary>Deck coordinate of the site's front-left corner. Set on processed labware only.</summary>
        public double AbsoluteY { get; set; }

        /// <summary>Deck height of the site surface. Set on processed labware only.</summary>
        public double AbsoluteZ { get; set; }
    }

    /// <summary>
    /// A data class to store properties extracted from a labware file.
    /// </summary>
    public class LabwareProperties
    {
        public double DimDx { get; set; }
        public double DimDy { get; set; }
        public double DimDz { get; set; }
        public int Rows { get; set; }
        public int Columns { get; set; }
        public int HoleCount { get; set; }
        public int IxIndex { get; set; }
        public double CntrBase { get; set; }
        public string CntrFile { get; set; } = string.Empty;

        /// <summary>Offset from the rack's front-left corner to its position reference point (BndryX).</summary>
        public double BoundaryX { get; set; }

        /// <summary>Offset from the rack's front-left corner to its position reference point (BndryY).</summary>
        public double BoundaryY { get; set; }

        /// <summary>UseBndry = 1.</summary>
        public bool UseBoundary { get; set; }

        /// <summary>Distance between columns (Dx).</summary>
        public double PitchX { get; set; }

        /// <summary>Distance between rows (Dy).</summary>
        public double PitchY { get; set; }

        public double HoleDx { get; set; }
        public double HoleDy { get; set; }
        public int HoleShape { get; set; }
        public int Stagger { get; set; }
        public int DataType { get; set; }

        /// <summary>Explicitly defined positions; empty for regular grids.</summary>
        public List<RackPositionDefinition> ExplicitPositions { get; set; } = new List<RackPositionDefinition>();

        /// <summary>Sites of a carrier template; empty for other labware.</summary>
        public List<CarrierSite> Sites { get; set; } = new List<CarrierSite>();
    }

    /// <summary>
    /// Represents a specific volumetric segment within a container.
    /// </summary>
    public class ContainerSegment
    {
        public int Index { get; set; }
        public double Dx { get; set; }
        public double Dy { get; set; }
        public double Dz { get; set; }
        public string EqnOfVol { get; set; } = string.Empty;
        public double Max { get; set; }
        public double Min { get; set; }
        public int Shape { get; set; }
    }

    /// <summary>
    /// A data class to store properties extracted from a container file.
    /// </summary>
    public class ContainerProperties
    {
        public double DimDx { get; set; }
        public double DimDy { get; set; }
        public double BaseMM { get; set; }
        public int SegmentsCount { get; set; }
        public List<ContainerSegment> Segments { get; set; } = new List<ContainerSegment>();
    }
}