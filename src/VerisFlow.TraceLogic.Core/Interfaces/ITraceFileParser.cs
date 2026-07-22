using System.Collections.Generic;
using System.Threading;
using TraceLogic.Core.Models;

namespace TraceLogic.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for parsing trace log files.
    /// </summary>
    public interface ITraceFileParser
    {
        /// <summary>
        /// Parses the entire .trc file synchronously from the given path.
        /// </summary>
        TraceAnalysisResult Parse(string filePath);

        /// <summary>
        /// Asynchronously reads and parses the log file into a stream of low-level entries.
        /// </summary>
        IAsyncEnumerable<TraceEntry> ParseLinesAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously aggregates individual trace entries into logical pipetting steps.
        /// </summary>
        IAsyncEnumerable<PipettingStep> AggregatePipettingStepsAsync(IAsyncEnumerable<TraceEntry> entriesStream, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously processes pipetting steps to generate a chronological stream of liquid transfers.
        /// </summary>
        IAsyncEnumerable<LiquidTransferEvent> CreateLiquidTransferEventsAsync(IAsyncEnumerable<PipettingStep> stepsStream, CancellationToken cancellationToken = default);
    }
}