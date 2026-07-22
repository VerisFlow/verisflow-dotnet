using TraceLogic.Core.Enums;

namespace TraceLogic.Core.Models
{
    /// <summary>
    /// Represents a single parsed line from the .trc log file.
    /// This is the lowest-level data object.
    /// </summary>
    public class TraceEntry
    {
        /// <summary>
        /// Gets or sets the physical line number corresponding to the original log file.
        /// </summary>
        /// <value>The zero-indexed or one-indexed line number integer.</value>
        public int LineNumber { get; set; }

        /// <summary>
        /// Gets or sets the extracted execution timestamp of the entry.
        /// </summary>
        /// <value>The precise date and time the log line was generated.</value>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the operational origin throwing the log entry.
        /// </summary>
        /// <value>The name or identifier of the source subsystem.</value>
        public required string Source { get; set; }

        /// <summary>
        /// Gets or sets the core action or instruction logged.
        /// </summary>
        /// <value>The command string.</value>
        public required string Command { get; set; }

        /// <summary>
        /// Gets or sets the execution lifecycle state of the command.
        /// </summary>
        /// <value>The mapped entry status enumeration.</value>
        public EntryStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the payload or granular context appended to the log instruction.
        /// </summary>
        /// <value>A string containing operational details and variables.</value>
        public required string Details { get; set; }

        /// <summary>
        /// Gets or sets the unmodified text string as extracted from the log file.
        /// </summary>
        /// <value>The raw text trace line.</value>
        public required string RawLine { get; set; }
    }
}