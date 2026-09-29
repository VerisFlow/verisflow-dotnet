#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace VerisFlow.LayParser.Core.Tests
{
    /// <summary>
    /// Writes Hamilton configuration files in packed form: every key and value is preceded by a length character.
    /// </summary>
    internal sealed class HxCfgFileBuilder
    {
        private readonly StringBuilder _text;

        public HxCfgFileBuilder(string header)
        {
            _text = new StringBuilder(header);
        }

        public HxCfgFileBuilder Add(string key, string value)
        {
            _text.Append((char)key.Length).Append(key).Append((char)value.Length).Append(value);
            return this;
        }

        public HxCfgFileBuilder Add(string key, double value)
        {
            return Add(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public string Build()
        {
            return _text.ToString();
        }

        public string Save(string path)
        {
            File.WriteAllText(path, Build(), Encoding.GetEncoding(28591));
            return path;
        }
    }

    /// <summary>
    /// Temporary folder with labware definitions modeled on the Hamilton files of the DeckDemo layout.
    /// </summary>
    internal sealed class LabwareFixture : IDisposable
    {
        public LabwareFixture()
        {
            Folder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(Folder);

            PlateContainer = new HxCfgFileBuilder("CONTAINER,default")
                .Add("BaseMM", 0.5)
                .Add("Dim.Dx", 6)
                .Add("Dim.Dy", 6)
                .Add("Segments", 0)
                .Save(PathOf("plate.ctr"));

            // Modeled on 96_PCR_L.rck: Dim.Dx precedes Dx, so Dx must not be confused with Dim.Dx.
            PlateRack = new HxCfgFileBuilder("RECTRACK,default")
                .Add("BndryX", 14)
                .Add("BndryY", 11.5)
                .Add("Cntr.1.base", 1)
                .Add("Cntr.1.file", PlateContainer)
                .Add("Columns", 12)
                .Add("Dim.Dx", 127)
                .Add("Dim.Dy", 86)
                .Add("Dim.Dz", 23.5)
                .Add("Dx", 9)
                .Add("Dy", 9)
                .Add("Hole.Shape", 0)
                .Add("Hole.X", 6)
                .Add("Hole.Y", 6)
                .Add("IX.Index", 1)
                .Add("Rows", 8)
                .Add("Stagger", 0)
                .Add("UseBndry", 1)
                .Save(PathOf("plate.rck"));

            PlateCarrier = new HxCfgFileBuilder("TEMPLATE,default")
                .Add("Dim.Dx", 135)
                .Add("Dim.Dy", 497)
                .Add("Dim.Dz", 93)
                .Add("UseBndry", 0)
                .Save(PathOf("plate_carrier.tml"));

            TroughContainer = new HxCfgFileBuilder("CONTAINER,default")
                .Add("BaseMM", 2)
                .Add("Segments", 0)
                .Save(PathOf("trough.ctr"));

            // Modeled on rgt_cont_60ml_BC_A00.rck: explicit, unevenly spaced positions.
            var trough = new HxCfgFileBuilder("RECTRACK,default");
            double[] offsets = { 0, -9, -18, -27, -41.5, -50.5, -59.5, -68.5 };
            for (var n = 1; n <= offsets.Length; n++)
            {
                var prefix = n.ToString(CultureInfo.InvariantCulture) + ".";
                trough.Add(prefix + "ID", n.ToString(CultureInfo.InvariantCulture)).Add(prefix + "X", 0).Add(prefix + "Y", offsets[n - 1]);
            }

            TroughRack = trough
                .Add("BndryX", 10)
                .Add("BndryY", 79.25)
                .Add("Cntr.1.base", 0)
                .Add("Cntr.1.file", TroughContainer)
                .Add("Dim.Dx", 20)
                .Add("Dim.Dy", 89.9)
                .Add("Dim.Dz", 65)
                .Add("HoleCnt", 8)
                .Add("IX.Index", 0)
                .Add("UseBndry", 1)
                .Save(PathOf("trough.rck"));

            // Modeled on RGT_CAR_5R60_A00.tml: the Site.N order differs from Site.N.Id.
            var reagentCarrier = new HxCfgFileBuilder("TEMPLATE,default")
                .Add("Dim.Dx", 22.5)
                .Add("Dim.Dy", 497)
                .Add("Dim.Dz", 93);

            (string Id, double Y)[] sites = { ("3", 198.5), ("4", 102.5), ("5", 6.5), ("1", 390.5), ("2", 294.5) };
            for (var n = 1; n <= sites.Length; n++)
            {
                var prefix = "Site." + n.ToString(CultureInfo.InvariantCulture) + ".";
                reagentCarrier
                    .Add(prefix + "Dx", 20)
                    .Add(prefix + "Dy", 89.9)
                    .Add(prefix + "Id", sites[n - 1].Id)
                    .Add(prefix + "LabwareFile", "ML_STAR\\Reagent\\rgt_cont_60ml_BC_A00.rck")
                    .Add(prefix + "StackSize", 1)
                    .Add(prefix + "X", 1.25)
                    .Add(prefix + "Y", sites[n - 1].Y)
                    .Add(prefix + "Z", 63.2);
            }

            ReagentCarrier = reagentCarrier.Add("Site.Cnt", sites.Length).Save(PathOf("reagent_carrier.tml"));
        }

        public string Folder { get; }
        public string PlateContainer { get; }
        public string PlateRack { get; }
        public string PlateCarrier { get; }
        public string TroughContainer { get; }
        public string TroughRack { get; }
        public string ReagentCarrier { get; }

        public string PathOf(string fileName)
        {
            return Path.Combine(Folder, fileName);
        }

        public static void AddLabware(
            HxCfgFileBuilder layout,
            int index,
            string id,
            string file,
            string siteId,
            string template,
            double x,
            double y,
            double zTrans,
            double? zTransValue,
            string? stackId = null)
        {
            var prefix = "Labware." + index.ToString(CultureInfo.InvariantCulture) + ".";

            layout
                .Add(prefix + "Angle", 0)
                .Add(prefix + "File", file)
                .Add(prefix + "Id", id)
                .Add(prefix + "SiteId", siteId)
                .Add(prefix + "TForm.1.X", 1).Add(prefix + "TForm.1.Y", 0).Add(prefix + "TForm.1.Z", 0)
                .Add(prefix + "TForm.2.X", 0).Add(prefix + "TForm.2.Y", 1).Add(prefix + "TForm.2.Z", 0)
                .Add(prefix + "TForm.3.X", x).Add(prefix + "TForm.3.Y", y).Add(prefix + "TForm.3.Z", 1)
                .Add(prefix + "Template", template)
                .Add(prefix + "ZTrans", zTrans);

            if (zTransValue.HasValue)
            {
                layout.Add(prefix + "ZTransValue", zTransValue.Value);
            }

            if (stackId != null)
            {
                layout.Add(prefix + "StackID", stackId);
            }
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Folder, recursive: true);
            }
            catch
            {
                // Best effort.
            }
        }
    }
}