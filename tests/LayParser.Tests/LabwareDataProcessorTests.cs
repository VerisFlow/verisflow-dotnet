using System;
using System.Collections.Generic;
using System.IO;
using VerisFlow.LayParser.Core;
using Xunit;

namespace VerisFlow.LayParser.Core.Tests
{
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
            Assert.Equal(127.76, item.Dx);
            Assert.Equal(85.48, item.Dy);
            Assert.Equal(8, item.Row);
            Assert.Equal(12, item.Column);
            Assert.True(item.AlphaIndex);
            Assert.True(item.TipRack);
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
    }
}