namespace TraceLogic.Core.Options
{
    /// <summary>
    /// Provides configuration settings for locating trace log files.
    /// </summary>
    public class TraceLocatorOptions
    {
        /// <summary>
        /// Gets or sets the default directory path to search for log files.
        /// Defaults to the standard Hamilton Venus installation path.
        /// </summary>
        public string DefaultLogDirectory { get; set; } = @"C:\Program Files (x86)\HAMILTON\LogFiles";
    }
}