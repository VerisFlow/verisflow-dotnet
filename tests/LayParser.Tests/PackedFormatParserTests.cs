#nullable enable

using System.Linq;
using Xunit;

namespace VerisFlow.LayParser.Core.Tests
{
    /// <summary>
    /// Layout parsing of packed files, in which every key and value has a one-character length prefix.
    /// </summary>
    public sealed class PackedFormatParserTests
    {
        private static readonly int[] ExpectedGripSequenceIndexes = { 1, 2 };

        [Fact]
        public void LongIdentifiers_AreReadExactly()
        {
            // Values of 33+ characters have a printable length prefix ('!' for 33), which earlier versions read as part of the value.
            var id = "Tip300F_Rack_With_A_Long_Identifier_0001";
            var template = "PLT_CAR_L5AC_A00_With_Long_Name_01";

            var layout = new HxCfgFileBuilder("DECKLAY,ML_STAR").Add("Labware.Cnt", 1);
            LabwareFixture.AddLabware(layout, 1, id, "ML_STAR\\stf_l.rck", "1", template, 500.4, 529.8, 214.95, 1);

            var labware = DeckLayoutParser.ParseDeckContent(layout.Build()).Labware.Single();

            Assert.Equal(id, labware.Id);
            Assert.Equal(template, labware.Template);
            Assert.Equal(@"C:\Program Files (x86)\HAMILTON\LabWare\ML_STAR\stf_l.rck", labware.FilePath);
        }

        [Fact]
        public void Coordinates_AreRoundedNotFloored()
        {
            // 0.29 * 1000 is 289.99999999999994 in binary floating point; flooring gave 0.289.
            var layout = new HxCfgFileBuilder("DECKLAY,ML_STAR").Add("Labware.Cnt", 1);
            LabwareFixture.AddLabware(layout, 1, "Plate", "plate.rck", "1", "Carrier", 0.29, 186.65, 186.15, 0);

            var labware = DeckLayoutParser.ParseDeckContent(layout.Build()).Labware.Single();

            Assert.Equal(0.29, labware.TForm3.X);
            Assert.Equal(186.65, labware.TForm3.Y);
        }

        [Fact]
        public void MissingZTransValue_IsDistinguishedFromZero()
        {
            var layout = new HxCfgFileBuilder("DECKLAY,ML_STAR").Add("Labware.Cnt", 2);
            LabwareFixture.AddLabware(layout, 1, "WithValue", "a.rck", "1", "Carrier", 0, 0, 100, 0);
            LabwareFixture.AddLabware(layout, 2, "WithoutValue", "b.rck", "1", "Carrier", 0, 0, 100, null);

            var labware = DeckLayoutParser.ParseDeckContent(layout.Build()).Labware;

            Assert.True(labware[0].HasZTransValue);
            Assert.False(labware[1].HasZTransValue);
        }

        [Fact]
        public void Instrument_IsReadFromLayout()
        {
            var layout = new HxCfgFileBuilder("DECKLAY,ML_STAR").Add("Instrument", "ML_STAR").Add("Labware.Cnt", 0);

            Assert.Equal("ML_STAR", DeckLayoutParser.ParseDeckContent(layout.Build()).Instrument);
        }

        [Fact]
        public void HxCfgText_DoesNotMatchKeysInsideLongerKeys()
        {
            var content = new HxCfgFileBuilder("RECTRACK,default").Add("Dim.Dx", 127).Add("Site.1.Dx", 20).Build();

            Assert.False(HxCfgText.TryGetDouble(content, "Dx", out _));
            Assert.Equal(127, HxCfgText.GetDouble(content, "Dim.Dx"));
        }

        [Fact]
        public void Sequences_AreGroupedByLabwareWithLongIds()
        {
            var longId = "COREGripTool_OnWaste_1000ul_0001_Extra";

            var content = new HxCfgFileBuilder("DECKLAY,ML_STAR")
                .Add("Seq.1.Cnt", 3)
                .Add("Seq.1.Item.1.ObjId", longId).Add("Seq.1.Item.1.PosId", "1")
                .Add("Seq.1.Item.2.ObjId", longId).Add("Seq.1.Item.2.PosId", "2")
                .Add("Seq.1.Item.3.ObjId", "Plate_1").Add("Seq.1.Item.3.PosId", "A1")
                .Add("Seq.1.Name", "seqMixed")
                .Add("Seq.1.ReadOnly", 0)
                .Add("Seq.Cnt", 1)
                .Build();

            var sequence = DeckSequenceParser.ParseSequenceContent(content).Single();

            Assert.Equal("seqMixed", sequence.Name);
            Assert.Equal(3, sequence.TotalCount);
            Assert.False(sequence.ReadOnly);
            Assert.Equal(2, sequence.Matrices.Count);

            var grip = sequence.Matrices.Single(m => m.ObjId == longId);
            Assert.Equal(2, grip.TotalWells);
            Assert.Equal(ExpectedGripSequenceIndexes, grip.Positions.Select(p => p.SequenceIndex));

            var plate = sequence.Matrices.Single(m => m.ObjId == "Plate_1");
            Assert.Equal(1, plate.Positions.Single().RowIndex);
            Assert.Equal(1, plate.Positions.Single().ColumnIndex);
        }
    }
}