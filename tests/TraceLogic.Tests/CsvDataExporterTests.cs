using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TraceLogic.Core.Exporting;
using TraceLogic.Core.Models;
using Xunit;

namespace TraceLogic.Core.Tests.Exporting
{
    /// <summary>
    /// Contains unit tests for validating CSV export formatting and stream output functionality in <see cref="CsvDataExporter"/>.
    /// </summary>
    public class CsvDataExporterTests
    {
        /// <summary>
        /// Defines common line separator characters across platforms to avoid repetitive heap allocations.
        /// </summary>
        private static readonly string[] LineSeparators = ["\r\n", "\n"];

        /// <summary>
        /// Verifies that <see cref="CsvDataExporter.Export(IEnumerable{LiquidTransferEvent}, List{ExportColumnInfo}, Stream)"/> correctly formats model properties into CSV headers and rows.
        /// </summary>
        [Fact]
        public void Export_ValidDataAndColumns_ShouldWriteFormattedCsvToStream()
        {
            // Arrange
            var exporter = new CsvDataExporter();
            using var memoryStream = new MemoryStream();

            var transfers = new List<LiquidTransferEvent>
            {
                new LiquidTransferEvent
                {
                    ChannelId = 1,
                    SourceLabware = "Plate_Src",
                    SourcePositionId = "A1",
                    TargetLabware = "Plate_Dst",
                    TargetPositionId = "A2",
                    Volume = 25.5
                }
            };

            var columns = new List<ExportColumnInfo>
            {
                new ExportColumnInfo { Header = "Channel", PropertyName = nameof(LiquidTransferEvent.ChannelId) },
                new ExportColumnInfo { Header = "Source", PropertyName = nameof(LiquidTransferEvent.SourceLabware) },
                new ExportColumnInfo { Header = "Source Well", PropertyName = nameof(LiquidTransferEvent.SourcePositionId) },
                new ExportColumnInfo { Header = "Volume (uL)", PropertyName = nameof(LiquidTransferEvent.Volume) }
            };

            // Act
            exporter.Export(transfers, columns, memoryStream);

            // Assert
            memoryStream.Position = 0;
            using var reader = new StreamReader(memoryStream, Encoding.UTF8);
            string content = reader.ReadToEnd();

            string[] lines = content.Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(2, lines.Length);
            Assert.Equal("Channel,Source,Source Well,Volume (uL)", lines[0]);
            Assert.Equal("1,Plate_Src,A1,25.5", lines[1]);
        }
    }
}