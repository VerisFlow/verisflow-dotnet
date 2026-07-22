using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using TraceLogic.Core.Enums;
using TraceLogic.Core.Models;
using TraceLogic.Core.Parsing;
using Xunit;

namespace TraceLogic.Core.Tests.Parsing
{
    /// <summary>
    /// Contains unit tests for verifying the log file parsing and data stream aggregation logic in <see cref="TraceFileParser"/>.
    /// </summary>
    public class TraceFileParserTests
    {
        private readonly TraceFileParser _parser;
        private readonly Mock<ILogger<TraceFileParser>> _loggerMock;

        /// <summary>
        /// Initializes a new instance of the <see cref="TraceFileParserTests"/> class and sets up required test dependencies.
        /// </summary>
        public TraceFileParserTests()
        {
            _loggerMock = new Mock<ILogger<TraceFileParser>>();
            _parser = new TraceFileParser(_loggerMock.Object);
        }

        /// <summary>
        /// Verifies that <see cref="TraceFileParser.ParseLinesAsync"/> accurately parses a single raw log line into a structured <see cref="TraceEntry"/>.
        /// </summary>
        [Fact]
        public async Task ParseLinesAsync_ValidLogLine_ShouldParseTraceEntryCorrectly()
        {
            // Arrange
            string logLine = "2026-07-22 10:30:00> Head : Aspirate - Start; channel 1: Plate1, A1, 50.0 uL";
            string tempFilePath = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFilePath, logLine, Encoding.UTF8);

            try
            {
                // Act
                var entries = new List<TraceEntry>();
                await foreach (var entry in _parser.ParseLinesAsync(tempFilePath))
                {
                    entries.Add(entry);
                }

                // Assert
                Assert.Single(entries);
                var parsedEntry = entries[0];
                Assert.Equal(new DateTime(2026, 7, 22, 10, 30, 0), parsedEntry.Timestamp);
                Assert.Equal("Head", parsedEntry.Source);
                Assert.Equal("Aspirate", parsedEntry.Command);
                Assert.Equal(EntryStatus.Start, parsedEntry.Status);
                Assert.Equal("channel 1: Plate1, A1, 50.0 uL", parsedEntry.Details);
            }
            finally
            {
                // Clean up temporary test file from disk
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        /// <summary>
        /// Verifies that <see cref="TraceFileParser.CreateLiquidTransferEventsAsync"/> correctly state-tracks and combines tip pickup, aspirate, and dispense steps into a complete <see cref="LiquidTransferEvent"/>.
        /// </summary>
        [Fact]
        public async Task CreateLiquidTransferEventsAsync_CompleteSequence_ShouldGenerateTransferEvent()
        {
            // Arrange
            // Simulating a sequence of aggregated pipetting steps
            var steps = new List<PipettingStep>
            {
                new PipettingStep
                {
                    ActionType = PipettingActionType.PickupTip,
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now.AddSeconds(1),
                    ChannelActions = new List<ChannelAction>
                    {
                        new ChannelAction { ChannelNumber = 1, LabwareId = "TipRack1", PositionId = "1", Volume = 0 }
                    }
                },
                new PipettingStep
                {
                    ActionType = PipettingActionType.Aspirate,
                    StartTime = DateTime.Now.AddSeconds(2),
                    EndTime = DateTime.Now.AddSeconds(3),
                    ChannelActions = new List<ChannelAction>
                    {
                        new ChannelAction { ChannelNumber = 1, LabwareId = "SourcePlate", PositionId = "A1", Volume = 100.0 }
                    }
                },
                new PipettingStep
                {
                    ActionType = PipettingActionType.Dispense,
                    StartTime = DateTime.Now.AddSeconds(4),
                    EndTime = DateTime.Now.AddSeconds(5),
                    ChannelActions = new List<ChannelAction>
                    {
                        new ChannelAction { ChannelNumber = 1, LabwareId = "TargetPlate", PositionId = "B1", Volume = 100.0 }
                    }
                }
            };

            // Act
            var transfers = new List<LiquidTransferEvent>();
            await foreach (var transfer in _parser.CreateLiquidTransferEventsAsync(ToAsyncEnumerable(steps)))
            {
                transfers.Add(transfer);
            }

            // Assert
            Assert.Single(transfers);
            var eventData = transfers[0];
            Assert.Equal(1, eventData.ChannelId);
            Assert.Equal("SourcePlate", eventData.SourceLabware);
            Assert.Equal("A1", eventData.SourcePositionId);
            Assert.Equal("TargetPlate", eventData.TargetLabware);
            Assert.Equal("B1", eventData.TargetPositionId);
            Assert.Equal(100.0, eventData.Volume);
            Assert.Equal("TipRack1", eventData.TipLabwareId);
            Assert.Equal(1, eventData.TipPositionId);
        }

        /// <summary>
        /// Helper method to convert an in-memory synchronous collection to an asynchronous stream for testing.
        /// </summary>
        /// <typeparam name="T">The type of elements in the collection.</typeparam>
        /// <param name="items">The source collection of items.</param>
        /// <returns>An asynchronous enumerable stream of items.</returns>
        private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                yield return item;
            }
            await Task.CompletedTask;
        }
    }
}