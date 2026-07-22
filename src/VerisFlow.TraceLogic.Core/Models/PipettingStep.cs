using TraceLogic.Core.Enums;

namespace TraceLogic.Core.Models
{
    /// <summary>
    /// Represents a higher-level, aggregated pipetting operation (e.g., a full aspirate or dispense).
    /// </summary>
    public class PipettingStep
    {
        /// <summary>
        /// Gets or sets the specific category of pipetting operation performed.
        /// </summary>
        /// <value>An enumeration value representing the action type.</value>
        public PipettingActionType ActionType { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the operation initiated.
        /// </summary>
        /// <value>The start date and time.</value>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the operation concluded.
        /// </summary>
        /// <value>The end date and time.</value>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets the calculated execution time of the pipetting step.
        /// </summary>
        /// <value>A timespan representing the difference between EndTime and StartTime.</value>
        public TimeSpan Duration => EndTime - StartTime;

        /// <summary>
        /// Gets or sets the collection of individual channel actions associated with this step.
        /// </summary>
        /// <value>A list of detailed channel actions.</value>
        public List<ChannelAction> ChannelActions { get; set; } = new List<ChannelAction>();

        /// <summary>
        /// Gets or sets the originating line number in the log file where this step began.
        /// </summary>
        /// <value>The integer line number.</value>
        public int StartLineNumber { get; set; }
    }
}