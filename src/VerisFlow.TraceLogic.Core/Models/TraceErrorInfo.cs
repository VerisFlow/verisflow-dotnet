using System;

namespace TraceLogic.Core.Models
{
    /// <summary>
    /// Represents an error event or diagnostic error message extracted from a trace log file.
    /// </summary>
    public class TraceErrorInfo
    {
        /// <summary>
        /// Gets or sets the originating line number in the log file where the error occurred.
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the error was logged.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the subsystem or source component reporting the error.
        /// </summary>
        public required string Source { get; set; }

        /// <summary>
        /// Gets or sets the command or operation being executed when the error occurred.
        /// </summary>
        public required string Command { get; set; }

        /// <summary>
        /// Gets or sets the detailed description or message associated with the error.
        /// </summary>
        public required string Message { get; set; }

        /// <summary>
        /// Gets or sets the specific error code identifier, if present.
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets the method or script source file location and line number, if present.
        /// </summary>
        public string? FileLocation { get; set; }
    }
}
