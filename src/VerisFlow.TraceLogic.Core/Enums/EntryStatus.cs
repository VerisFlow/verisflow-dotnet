namespace TraceLogic.Core.Enums
{
    /// <summary>
    /// Represents the status of a trace log entry.
    /// </summary>
    public enum EntryStatus
    {
        /// <summary>
        /// Indicates an undefined or unparsed status.
        /// </summary>
        Unknown,

        /// <summary>
        /// Indicates the initiation of a command or action.
        /// </summary>
        Start,

        /// <summary>
        /// Indicates an ongoing operation that has not yet concluded.
        /// </summary>
        Progress,

        /// <summary>
        /// Indicates the successful conclusion of a command or action.
        /// </summary>
        Complete,

        /// <summary>
        /// Indicates that data or a log entry was successfully flushed to the file.
        /// </summary>
        Written
    }
}