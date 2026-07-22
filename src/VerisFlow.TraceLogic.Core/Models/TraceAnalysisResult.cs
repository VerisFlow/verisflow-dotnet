namespace TraceLogic.Core.Models
{
    /// <summary>
    /// A container for all the data parsed from a .trc file.
    /// This is the final object returned by the parser.
    /// </summary>
    public class TraceAnalysisResult
    {
        /// <summary>
        /// Gets or sets the original name of the parsed trace file.
        /// </summary>
        /// <value>The file name string.</value>
        public required string FileName { get; set; }

        /// <summary>
        /// Gets or sets the comprehensive chronological list of all raw trace entries.
        /// </summary>
        /// <value>A list containing low-level trace log data.</value>
        public List<TraceEntry> AllEntries { get; set; } = new List<TraceEntry>();

        /// <summary>
        /// Gets or sets the collection of logical operational steps aggregated from the raw entries.
        /// </summary>
        /// <value>A list of distinct pipetting operations.</value>
        public List<PipettingStep> PipettingSteps { get; set; } = new List<PipettingStep>();

        // NEW: Add a list to hold the high-level liquid transfer events.
        /// <summary>
        /// Gets or sets the final synthesized collection of end-to-end liquid transfers.
        /// </summary>
        /// <value>A list of complete transfer events mapped across channels.</value>
        public List<LiquidTransferEvent> LiquidTransfers { get; set; } = new List<LiquidTransferEvent>();

        /// <summary>
        /// Gets or sets any structural anomalies or critical failures encountered during the parsing execution.
        /// </summary>
        /// <value>A list of error message strings.</value>
        public List<string> Errors { get; set; } = new List<string>();
    }
}