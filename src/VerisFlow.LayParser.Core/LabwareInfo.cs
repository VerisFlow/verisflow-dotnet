using System.Text;
using System.Collections.Generic;

namespace VerisFlow.LayParser.Core
{
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
    /// Represents detailed information about a single piece of labware.
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
        public double ZTransValue { get; set; }
        public double ZTrans { get; set; }
        public string Template { get; set; } = string.Empty;

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
    /// A data class to store properties extracted from a labware file.
    /// </summary>
    public class LabwareProperties
    {
        public double DimDx { get; set; }
        public double DimDy { get; set; }
        public int Rows { get; set; }
        public int Columns { get; set; }
        public int IxIndex { get; set; }
        public double CntrBase { get; set; }
        public string CntrFile { get; set; } = string.Empty;
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