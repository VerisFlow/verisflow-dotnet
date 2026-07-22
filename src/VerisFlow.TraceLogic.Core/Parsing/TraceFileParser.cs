using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TraceLogic.Core.Enums;
using TraceLogic.Core.Models;
using TraceLogic.Core.Interfaces;
using TraceLogic.Core.Exceptions;

namespace TraceLogic.Core.Parsing
{
    /// <summary>
    /// Main class responsible for parsing Hamilton Venus .trc files.
    /// </summary>
    /// <remarks>
    /// This parser operates sequentially, transforming unstructured log lines into low-level entries, 
    /// coupling related operations into logical steps, and finally leveraging an internal state machine 
    /// to build comprehensive liquid transfer events.
    /// </remarks>
    public partial class TraceFileParser : ITraceFileParser
    {
        private readonly ILogger<TraceFileParser> _logger;

#if NET8_0_OR_GREATER
        [GeneratedRegex(@"^(?<timestamp>[\d\- :]+)> (?<source>.+?) : (?<command>.+?) - (?<status>\w+); ?(?<details>.*)$")]
        private static partial Regex GetLineRegex();

        [GeneratedRegex(@"channel (?<channel>\d+): (?<labware>[^,]+), (?<position>[^,]+), (?<volume>[\d\.]+) uL")]
        private static partial Regex GetPipettingDetailsRegex();

        [GeneratedRegex(@"channel (?<channel>\d+): (?<labware>[^,]+), (?<position>[^,>]+)")]
        private static partial Regex GetTipActionDetailsRegex();
#else
        private static readonly Regex LineRegexCompiled = new Regex(
            @"^(?<timestamp>[\d\- :]+)> (?<source>.+?) : (?<command>.+?) - (?<status>\w+); ?(?<details>.*)$",
            RegexOptions.Compiled);
        private static Regex GetLineRegex() => LineRegexCompiled;

        private static readonly Regex PipettingDetailsRegexCompiled = new Regex(
            @"channel (?<channel>\d+): (?<labware>[^,]+), (?<position>[^,]+), (?<volume>[\d\.]+) uL",
            RegexOptions.Compiled);
        private static Regex GetPipettingDetailsRegex() => PipettingDetailsRegexCompiled;

        private static readonly Regex TipActionDetailsRegexCompiled = new Regex(
            @"channel (?<channel>\d+): (?<labware>[^,]+), (?<position>[^,>]+)",
            RegexOptions.Compiled);
        private static Regex GetTipActionDetailsRegex() => TipActionDetailsRegexCompiled;
#endif

        /// <summary>
        /// Initializes a new instance of the TraceFileParser class.
        /// </summary>
        /// <param name="logger">The logger used to capture parsing events and errors.</param>
        public TraceFileParser(ILogger<TraceFileParser> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Parses the entire .trc file from the given path.
        /// </summary>
        /// <param name="filePath">The full path to the .trc file.</param>
        /// <returns>A TraceAnalysisResult object containing all parsed data.</returns>
        /// <exception cref="System.ArgumentException">Thrown when the provided file path is null, empty, or contains invalid characters.</exception>
        public TraceAnalysisResult Parse(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            }

            var result = new TraceAnalysisResult { FileName = Path.GetFileName(filePath) };

            try
            {
                Task.Run(async () =>
                {
                    var entries = new List<TraceEntry>();
                    await foreach (var entry in ParseLinesAsync(filePath))
                    {
                        entries.Add(entry);
                    }
                    result.AllEntries = entries;

                    var steps = new List<PipettingStep>();
                    await foreach (var step in AggregatePipettingStepsAsync(GetAsyncEnumerable(entries)))
                    {
                        steps.Add(step);
                    }
                    result.PipettingSteps = steps;

                    var transfers = new List<LiquidTransferEvent>();
                    await foreach (var transfer in CreateLiquidTransferEventsAsync(GetAsyncEnumerable(steps)))
                    {
                        transfers.Add(transfer);
                    }
                    result.LiquidTransfers = transfers;

                }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
#pragma warning disable CA1848
                _logger.LogError(ex, "An unexpected error occurred during parsing of file {FilePath}", filePath);
#pragma warning restore CA1848
                result.Errors.Add($"An unexpected error occurred during parsing: {ex.Message}");
                throw new TraceParseException($"Failed to parse trace file {filePath}", ex);
            }

            return result;
        }

        private static async IAsyncEnumerable<T> GetAsyncEnumerable<T>(IEnumerable<T> source)
        {
            foreach (var item in source)
            {
                yield return item;
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// Step 1: Reads the file asynchronously and parses each line into a TraceEntry object.
        /// </summary>
        public async IAsyncEnumerable<TraceEntry> ParseLinesAsync(string filePath, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            int lineNumber = 1;

            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous);
            using var reader = new StreamReader(fileStream, Encoding.UTF8, true);

            string? line;
#if NETSTANDARD2_0
            while ((line = await reader.ReadLineAsync()) != null)
#else
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
#endif
            {
                cancellationToken.ThrowIfCancellationRequested();
                var match = GetLineRegex().Match(line);

                if (match.Success)
                {
                    yield return new TraceEntry
                    {
                        LineNumber = lineNumber,
#if NET8_0_OR_GREATER
                        Timestamp = DateTime.ParseExact(match.Groups["timestamp"].ValueSpan, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
#else
                        Timestamp = DateTime.ParseExact(match.Groups["timestamp"].Value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
#endif
                        Source = match.Groups["source"].Value.Trim(),
                        Command = match.Groups["command"].Value.Trim(),
                        Status = Enum.TryParse<EntryStatus>(match.Groups["status"].Value, true, out var status) ? status : EntryStatus.Unknown,
                        Details = match.Groups["details"].Value.Trim(),
                        RawLine = line
                    };
                }
                lineNumber++;
            }
        }

        /// <summary>
        /// Step 2: Aggregates individual TraceEntry objects into logical PipettingSteps.
        /// </summary>
        /// <remarks>
        /// This method scans for 'Start' entries of specific pipetting commands and searches forward 
        /// to match them with their corresponding 'Complete' entries to compute operational durations and specific payload details.
        /// </remarks>
        public async IAsyncEnumerable<PipettingStep> AggregatePipettingStepsAsync(IAsyncEnumerable<TraceEntry> entriesStream, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var pendingCommands = new Dictionary<string, TraceEntry>();

            await foreach (var currentEntry in entriesStream.WithCancellation(cancellationToken))
            {
                if (!IsPipettingCommand(currentEntry.Command))
                {
                    continue;
                }

                if (currentEntry.Status == EntryStatus.Start)
                {
                    pendingCommands[currentEntry.Command] = currentEntry;
                }
                else if (currentEntry.Status == EntryStatus.Complete && pendingCommands.TryGetValue(currentEntry.Command, out var startEntry))
                {
                    var actionType = GetPipettingActionType(currentEntry.Command);
                    var step = new PipettingStep
                    {
                        ActionType = actionType,
                        StartTime = startEntry.Timestamp,
                        EndTime = currentEntry.Timestamp,
                        StartLineNumber = startEntry.LineNumber,
                        ChannelActions = ParseChannelDetails(currentEntry.Details, actionType)
                    };

                    pendingCommands.Remove(currentEntry.Command);
                    yield return step;
                }
            }
        }

        /// <summary>
        /// Step 3: Processes a list of pipetting steps to generate a chronological list of liquid transfers.
        /// </summary>
        public async IAsyncEnumerable<LiquidTransferEvent> CreateLiquidTransferEventsAsync(IAsyncEnumerable<PipettingStep> stepsStream, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var channelStates = new Dictionary<int, LiquidTransferEvent>();

            await foreach (var step in stepsStream.WithCancellation(cancellationToken))
            {
                foreach (var action in step.ChannelActions)
                {
                    if (!channelStates.TryGetValue(action.ChannelNumber, out var state))
                    {
                        state = new LiquidTransferEvent { ChannelId = action.ChannelNumber };
                        channelStates[action.ChannelNumber] = state;
                    }

                    switch (step.ActionType)
                    {
                        case PipettingActionType.PickupTip:
                            state.TipLabwareId = action.LabwareId;
                            state.TipPositionId = int.TryParse(action.PositionId, out var pos) ? pos : 0;
                            break;

                        case PipettingActionType.Aspirate:
                            state.SourceLabware = action.LabwareId;
                            state.SourcePositionId = action.PositionId;
                            state.Volume = action.Volume;
                            break;

                        case PipettingActionType.Dispense:
                            state.Timestamp = step.StartTime;
                            state.TargetLabware = action.LabwareId;
                            state.TargetPositionId = action.PositionId;

                            yield return new LiquidTransferEvent
                            {
                                Timestamp = state.Timestamp,
                                ChannelId = state.ChannelId,
                                SourceLabware = state.SourceLabware,
                                SourcePositionId = state.SourcePositionId,
                                TargetLabware = state.TargetLabware,
                                TargetPositionId = state.TargetPositionId,
                                Volume = state.Volume,
                                TipLabwareId = state.TipLabwareId,
                                TipPositionId = state.TipPositionId
                            };

                            state.SourceLabware = null;
                            state.SourcePositionId = null;
                            state.Volume = 0;
                            break;

                        case PipettingActionType.EjectTip:
                            state.TipLabwareId = null;
                            state.TipPositionId = 0;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Helper to parse the details string from a "complete" line based on the action type.
        /// </summary>
        /// <param name="details">The raw detailed string extracted from the log line.</param>
        /// <param name="actionType">The specific action type being parsed.</param>
        /// <returns>A list of detailed channel actions identified in the string.</returns>
        private static List<ChannelAction> ParseChannelDetails(string details, PipettingActionType actionType)
        {
            var actions = new List<ChannelAction>();
            bool isVolumeAction = actionType == PipettingActionType.Aspirate || actionType == PipettingActionType.Dispense;
            var regex = isVolumeAction ? GetPipettingDetailsRegex() : GetTipActionDetailsRegex();

            var matches = regex.Matches(details);
            foreach (Match match in matches)
            {
                actions.Add(new ChannelAction
                {
                    ChannelNumber = int.Parse(match.Groups["channel"].Value, CultureInfo.InvariantCulture),
                    LabwareId = match.Groups["labware"].Value.Trim(),
                    PositionId = match.Groups["position"].Value.Trim(),
                    Volume = isVolumeAction ? double.Parse(match.Groups["volume"].Value, CultureInfo.InvariantCulture) : 0
                });
            }
            return actions;
        }

        /// <summary>
        /// Checks if a command string corresponds to a known pipetting action.
        /// </summary>
        /// <param name="command">The command string to evaluate.</param>
        /// <returns>True if the command string contains a recognized pipetting action; otherwise, false.</returns>
        private static bool IsPipettingCommand(string command)
        {
            return command.Contains("Aspirate") || command.Contains("Dispense") || command.Contains("Tip Pick Up") || command.Contains("Tip Eject");
        }

        /// <summary>
        /// Maps a command string to a PipettingActionType enum.
        /// </summary>
        /// <param name="command">The command string containing the action name.</param>
        /// <returns>The corresponding PipettingActionType, or Unknown if not matched.</returns>
        private static PipettingActionType GetPipettingActionType(string command)
        {
            if (command.Contains("Aspirate")) return PipettingActionType.Aspirate;
            if (command.Contains("Dispense")) return PipettingActionType.Dispense;
            if (command.Contains("Tip Pick Up")) return PipettingActionType.PickupTip;
            if (command.Contains("Tip Eject")) return PipettingActionType.EjectTip;
            if (command.Contains("Initialize")) return PipettingActionType.Initialize;
            return PipettingActionType.Unknown;
        }
    }
}