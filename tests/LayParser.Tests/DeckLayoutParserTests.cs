using System;
using System.IO;
using VerisFlow.LayParser.Core;
using Xunit;

namespace VerisFlow.LayParser.Core.Tests
{
    public class DeckLayoutParserTests : IDisposable
    {
        private readonly string _tempDirectory;

        public DeckLayoutParserTests()
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
        public void GetLabwareInfo_WithValidFile_ParsesAllFieldsCorrectly()
        {
            string layPath = Path.Combine(_tempDirectory, "sample.lay");
            string layContent = @"
Labware.Cnt 1
Labware.1.File ML_STAR_Deck.tml
Labware.1.Id Carrier1
Labware.1.SiteId Site_A
Labware.1.Template CustomTemplate
Labware.1.ZTrans 15.5008
Labware.1.ZTransValue 10.1001
Labware.1.TForm.1.X 1.1119
Labware.1.TForm.1.Y 2.2229
Labware.1.TForm.1.Z 3.3339
Labware.1.TForm.2.X 4.4449
Labware.1.TForm.2.Y 5.5559
Labware.1.TForm.2.Z 6.6669
Labware.1.TForm.3.X 100.5559
Labware.1.TForm.3.Y 200.6669
Labware.1.TForm.3.Z 300.7779
";
            File.WriteAllText(layPath, layContent);

            var result = DeckLayoutParser.GetLabwareInfo(layPath);

            Assert.Single(result);
            var labware = result[0];
            Assert.Equal(1, labware.Index);
            Assert.Equal("Carrier1", labware.Id);
            Assert.Equal("Site_A", labware.SiteId);
            Assert.Equal("CustomTemplate", labware.Template);
            Assert.Equal(15.5, labware.ZTrans);
            Assert.Equal(10.1, labware.ZTransValue);
            Assert.Equal(100.555, labware.TForm3.X);
            Assert.Equal(200.666, labware.TForm3.Y);
            Assert.Equal(300.777, labware.TForm3.Z);
        }

        [Fact]
        public void GetLabwareInfo_WhenFileDoesNotExist_ReturnsEmptyList()
        {
            string nonExistentPath = Path.Combine(_tempDirectory, "missing.lay");

            var result = DeckLayoutParser.GetLabwareInfo(nonExistentPath);

            Assert.Empty(result);
        }

        [Fact]
        public void GetLabwareInfo_WhenCountMissing_ReturnsEmptyList()
        {
            string layPath = Path.Combine(_tempDirectory, "invalid.lay");
            File.WriteAllText(layPath, "Labware.1.Id Test");

            var result = DeckLayoutParser.GetLabwareInfo(layPath);

            Assert.Empty(result);
        }
    }
}