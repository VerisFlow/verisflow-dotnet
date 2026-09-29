#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace VerisFlow.LayParser.Core.Tests
{
    /// <summary>
    /// Geometry of packed labware definitions, checked against values of the DeckDemo layout.
    /// </summary>
    public sealed class LabwareGeometryTests : IDisposable
    {
        private const int Precision = 3;

        private readonly LabwareFixture _fixture = new LabwareFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        private List<ProcessedLabwareInfo> ProcessDemoDeck()
        {
            var layout = new HxCfgFileBuilder("DECKLAY,ML_STAR")
                .Add("Instrument", "ML_STAR")
                .Add("Labware.Cnt", 4);

            LabwareFixture.AddLabware(layout, 1, "PLT_CAR_Plate_1", _fixture.PlateCarrier, "6T-30", "default", 752.5, 63, 100, 2);
            LabwareFixture.AddLabware(layout, 2, "Plate_1", _fixture.PlateRack, "1", "PLT_CAR_Plate_1", 770.5, 530, 186.15, 0);
            LabwareFixture.AddLabware(layout, 3, "RGT_CAR_1", _fixture.ReagentCarrier, "1T-53", "default", 1270, 63, 100, 2);
            LabwareFixture.AddLabware(layout, 4, "Trough5", _fixture.TroughRack, "5", "RGT_CAR_1", 1281.25, 148.75, 163.2, 1);

            var deck = DeckLayoutParser.ParseDeckContent(layout.Build());
            return LabwareDataProcessor.Process(deck.Labware);
        }

        private static LabwarePosition PositionOf(ProcessedLabwareInfo labware, string name)
        {
            var position = labware.GetPosition(name);
            Assert.NotNull(position);
            return position;
        }

        [Fact]
        public void GridRack_OriginIsDerivedFromBoundaryAndFirstPosition()
        {
            var plate = ProcessDemoDeck().Single(l => l.Id == "Plate_1");

            Assert.Equal(PositionReference.FirstPosition, plate.ReferenceKind);
            Assert.Equal(756.5, plate.OriginX, Precision);
            Assert.Equal(455.5, plate.OriginY, Precision);
            Assert.Equal(127, plate.Dx, Precision);
            Assert.Equal(86, plate.Dy, Precision);
            Assert.Equal("PLT_CAR_Plate_1", plate.ParentId);
            Assert.Empty(plate.GeometryNotes);
        }

        [Fact]
        public void GridRack_PitchIsNotConfusedWithOutlineDimensions()
        {
            var plate = ProcessDemoDeck().Single(l => l.Id == "Plate_1");

            Assert.Equal(9, plate.PitchX, Precision);
            Assert.Equal(9, plate.PitchY, Precision);
        }

        [Fact]
        public void GridRack_PositionsRunDownColumnsFromBackLeft()
        {
            var plate = ProcessDemoDeck().Single(l => l.Id == "Plate_1");

            Assert.Equal(96, plate.Positions.Count);

            var a1 = PositionOf(plate, "A1");
            Assert.Equal(1, a1.Index);
            Assert.Equal(770.5, a1.X, Precision);
            Assert.Equal(530, a1.Y, Precision);

            var h1 = PositionOf(plate, "H1");
            Assert.Equal(8, h1.Index);
            Assert.Equal(467, h1.Y, Precision);

            var a12 = PositionOf(plate, "A12");
            Assert.Equal(89, a12.Index);
            Assert.Equal(869.5, a12.X, Precision);
        }

        [Fact]
        public void GetPosition_ReturnsNullForUnknownPosition()
        {
            var plate = ProcessDemoDeck().Single(l => l.Id == "Plate_1");

            Assert.Null(plate.GetPosition("Z99"));
            Assert.Null(plate.GetPosition(" "));
        }

        [Fact]
        public void ZTransValue0_AddsContainerBaseOnly()
        {
            var plate = ProcessDemoDeck().Single(l => l.Id == "Plate_1");

            Assert.Equal(186.65, plate.FinalZ, Precision);
            Assert.Equal(0.0, plate.ZTransValue);
            Assert.Equal(0.5, plate.ContainerBaseMM);
            Assert.False(plate.IsZCalculationIncomplete);
        }

        [Fact]
        public void Carrier_UsesZTransAndTrackFromSiteId()
        {
            var carrier = ProcessDemoDeck().Single(l => l.Id == "PLT_CAR_Plate_1");

            Assert.Equal(LabwareType.Carrier, carrier.LabwareType);
            Assert.Equal(PositionReference.Origin, carrier.ReferenceKind);
            Assert.Equal(752.5, carrier.OriginX, Precision);
            Assert.Equal(100, carrier.FinalZ, Precision);
            Assert.Equal(30, carrier.Track);
            Assert.Equal(6, carrier.TrackWidth);
            Assert.Equal(carrier.OriginX, DeckGeometry.GetMlStarTrackX(carrier.Track!.Value), Precision);
            Assert.False(carrier.IsZCalculationIncomplete);
            Assert.Empty(carrier.GeometryNotes);
        }

        [Fact]
        public void ExplicitRack_OriginMatchesCarrierSite()
        {
            var labware = ProcessDemoDeck();
            var trough = labware.Single(l => l.Id == "Trough5");
            var carrier = labware.Single(l => l.Id == "RGT_CAR_1");
            var site = carrier.Sites.Single(s => s.Id == trough.SiteId);

            Assert.True(trough.HasExplicitPositions);
            Assert.Equal(1271.25, trough.OriginX, Precision);
            Assert.Equal(69.5, trough.OriginY, Precision);
            Assert.Equal(site.AbsoluteX, trough.OriginX, Precision);
            Assert.Equal(site.AbsoluteY, trough.OriginY, Precision);
            Assert.Equal(163.2, site.AbsoluteZ, Precision);
            Assert.Equal(53, carrier.Track);
        }

        [Fact]
        public void ExplicitRack_PositionsKeepTheirUnevenSpacing()
        {
            var trough = ProcessDemoDeck().Single(l => l.Id == "Trough5");

            Assert.Equal(8, trough.Positions.Count);
            Assert.Equal(148.75, PositionOf(trough, "1").Y, Precision);
            Assert.Equal(107.25, PositionOf(trough, "5").Y, Precision);
            Assert.Equal(80.25, PositionOf(trough, "8").Y, Precision);
            Assert.Equal(1281.25, PositionOf(trough, "8").X, Precision);
        }

        [Fact]
        public void ZTransValue1_AddsContainerOffsetAndBase()
        {
            var trough = ProcessDemoDeck().Single(l => l.Id == "Trough5");

            Assert.Equal(165.2, trough.FinalZ, Precision);
            Assert.Equal(0, trough.ContainerBaseOffset, Precision);
        }

        [Fact]
        public void UnknownZTransValue_KeepsZTransAndAddsNote()
        {
            var layout = new HxCfgFileBuilder("DECKLAY,ML_STAR").Add("Labware.Cnt", 1);
            LabwareFixture.AddLabware(layout, 1, "Carrier", _fixture.PlateCarrier, "6T-1", "default", 100, 63, 100, 3);

            var carrier = LabwareDataProcessor.Process(DeckLayoutParser.ParseDeckContent(layout.Build()).Labware).Single();

            Assert.Equal(100, carrier.FinalZ, Precision);
            Assert.Contains(carrier.GeometryNotes, note => note.Contains("ZTransValue"));
        }

        [Fact]
        public void StackId_IsReported()
        {
            var layout = new HxCfgFileBuilder("DECKLAY,ML_STAR").Add("Labware.Cnt", 1);
            LabwareFixture.AddLabware(layout, 1, "Stack4_0001", _fixture.PlateRack, "4", "PLT_CAR", 84.25, 189.5, 173, 1, stackId: "Stack4");

            var plate = LabwareDataProcessor.Process(DeckLayoutParser.ParseDeckContent(layout.Build()).Labware).Single();

            Assert.Equal("Stack4", plate.StackId);
        }
    }
}