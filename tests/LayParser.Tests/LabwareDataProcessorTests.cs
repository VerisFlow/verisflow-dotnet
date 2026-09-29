using System;
using System.Collections.Generic;
using System.IO;
using VerisFlow.LayParser.Core;
using Xunit;

namespace VerisFlow.LayParser.Core.Tests
{
    /// <summary>
    /// Labware definitions written as "key value" text (separator format). Geometry with packed definitions is
    /// covered by <see cref="LabwareGeometryTests"/>.
    /// </summary>
    public class LabwareDataProcessorTests : IDisposable
    {
        private readonly string _tempDirectory;

        public LabwareDataProcessorTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void Process_WithRackAndPropertiesFile_CalculatesFieldsCorrectly()
        {
            string rckPath = Path.Combine(_tempDirectory, "test_rack.rck");
            string rckContent = @"
Dim.Dx 127.76
Dim.Dy 85.48
Cntr.1.base -12.50
IX.Index 1
Rows 8
Columns 12
";
            File.WriteAllText(rckPath, rckContent);

            var rawData = new List<LabwareInfo>
            {
                new LabwareInfo
                {
                    Index = 1,
                    Id = "Rack1",
                    FilePath = rckPath,
                    Template = "default",
                    ZTrans = 10.5,
                    TForm3 = new TFormVector { X = 100.0, Y = 200.0, Z = 300.0 }
                }
            };

            var processed = LabwareDataProcessor.Process(rawData);

            Assert.Single(processed);
            var item = processed[0];
            Assert.Equal("Rack1", item.Id);
            Assert.Equal(100.0, item.FinalX);
            Assert.Equal(200.0, item.FinalY);
            Assert.Equal(10.5, item.FinalZ);
            Assert.Equal(LabwareType.RackCarrier, item.LabwareType);
            Assert.Equal("", item.Template);
            Assert.Equal("", item.ParentId);
            Assert.Equal(127.76, item.Dx);
            Assert.Equal(85.48, item.Dy);
            Assert.Equal(8, item.Row);
            Assert.Equal(12, item.Column);
            Assert.True(item.AlphaIndex);
            Assert.True(item.TipRack);

            // "Dim.Dx" must not be read as the column pitch "Dx".
            Assert.Equal(0, item.PitchX);
            Assert.Equal(PositionReference.FirstPosition, item.ReferenceKind);
            Assert.Equal(96, item.Positions.Count);
            Assert.Contains(item.GeometryNotes, note => note.Contains("UseBndry"));
        }

        [Fact]
        public void Process_WithoutContainerFile_KeepsZTransAndReportsIncompleteZ()
        {
            string rckPath = Path.Combine(_tempDirectory, "no_container.rck");
            File.WriteAllText(rckPath, "Dim.Dx 127\nDim.Dy 86\nCntr.1.base 1\nRows 8\nColumns 12\n");

            var rawData = new List<LabwareInfo>
            {
                new LabwareInfo
                {
                    Index = 1,
                    Id = "Plate",
                    FilePath = rckPath,
                    Template = "Carrier",
                    ZTrans = 186.15,
                    ZTransValue = 1,
                    HasZTransValue = true
                }
            };

            var item = LabwareDataProcessor.Process(rawData)[0];

            Assert.Equal(186.15, item.FinalZ);
            Assert.True(item.IsZCalculationIncomplete);
            Assert.Contains("ZTransValue is 1", item.ValidationWarning);
            Assert.Null(item.ContainerBaseMM);
        }

        [Theory]
        [InlineData(".tml", "Site_1", LabwareType.Carrier, true)]
        [InlineData(".tml", "", LabwareType.Carrier, false)]
        [InlineData(".rck", "", LabwareType.Rack, false)]
        [InlineData(".ctr", "", LabwareType.Container, false)]
        public void Process_DeterminesTypeAndLoadableCorrectly(string extension, string siteId, LabwareType expectedType, bool expectedLoadable)
        {
            string labwareFile = Path.Combine(_tempDirectory, $"labware{extension}");
            File.WriteAllText(labwareFile, "");

            var rawData = new List<LabwareInfo>
            {
                new LabwareInfo
                {
                    Index = 1,
                    Id = "Labware1",
                    FilePath = labwareFile,
                    SiteId = siteId,
                    Template = "CustomTemplate"
                }
            };

            var processed = LabwareDataProcessor.Process(rawData);

            Assert.Single(processed);
            Assert.Equal(expectedType, processed[0].LabwareType);
            Assert.Equal(expectedLoadable, processed[0].Loadable);
        }

        [Fact]
        public void Process_FallbackToHoleCntWhenRowsAndColsMissing()
        {
            // Verify fallback logic when standard dimension keys are omitted.
            string rckPath = Path.Combine(_tempDirectory, "hole_cnt.rck");
            File.WriteAllText(rckPath, "HoleCnt 24");

            var rawData = new List<LabwareInfo>
            {
                new LabwareInfo
                {
                    Index = 1,
                    FilePath = rckPath
                }
            };

            var processed = LabwareDataProcessor.Process(rawData);

            Assert.Single(processed);
            Assert.Equal(24, processed[0].Row);
            Assert.Equal(1, processed[0].Column);
        }

        [Fact]
        public void Process_WhenDefinitionMissing_AddsGeometryNote()
        {
            var rawData = new List<LabwareInfo>
            {
                new LabwareInfo
                {
                    Index = 1,
                    Id = "Ghost",
                    FilePath = Path.Combine(_tempDirectory, "missing.rck"),
                    Template = "Carrier"
                }
            };

            var item = LabwareDataProcessor.Process(rawData)[0];

            Assert.Empty(item.Positions);
            Assert.Contains(item.GeometryNotes, note => note.Contains("could not be read"));
        }
    }
}