namespace TraceLogic.Core.Enums
{
    /// <summary>
    /// Defines the predefined time windows used for filtering and retrieving trace log files.
    /// </summary>
    public enum TimeFilterType
    {
        /// <summary>
        /// Applies no time filtering, returning all available log files.
        /// </summary>
        All,

        /// <summary>
        /// Retrieves only the single most recently modified log file.
        /// </summary>
        Latest,

        /// <summary>
        /// Retrieves log files modified during the current local calendar day.
        /// </summary>
        Today,

        /// <summary>
        /// Retrieves log files modified during the current ISO 8601 calendar week.
        /// </summary>
        ThisWeek,

        /// <summary>
        /// Retrieves log files modified during the current local calendar month.
        /// </summary>
        ThisMonth,

        /// <summary>
        /// Applies a specific, user-defined start and end date boundary for retrieval.
        /// </summary>
        Custom
    }
}