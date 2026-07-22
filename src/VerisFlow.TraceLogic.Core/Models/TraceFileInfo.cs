namespace TraceLogic.Core.Models
{
    /// <summary>
    /// Represents the metadata and file system information for a located trace log file.
    /// </summary>
    public class TraceFileInfo
    {
        /// <summary>
        /// Gets or sets the name of the trace file, including its extension.
        /// </summary>
        /// <value>The file name string.</value>
        public required string FileName { get; set; }

        /// <summary>
        /// Gets or sets the absolute file system path pointing to the trace file.
        /// </summary>
        /// <value>The full directory and file name string.</value>
        public required string FullPath { get; set; }

        /// <summary>
        /// Gets or sets the physical size of the trace file on disk in kilobytes.
        /// </summary>
        /// <value>The file size represented as a double-precision floating-point number.</value>
        public double SizeKB { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the file was last modified by the operating system.
        /// </summary>
        /// <value>The local system date and time of the last write operation.</value>
        public DateTime LastModified { get; set; }
    }
}