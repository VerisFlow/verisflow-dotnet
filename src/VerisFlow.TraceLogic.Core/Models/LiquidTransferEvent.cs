namespace TraceLogic.Core.Models
{
    /// <summary>
    /// Represents a single, complete liquid transfer from a source to a target.
    /// This is a high-level model generated from aggregating multiple PipettingSteps.
    /// Properties are nullable to allow for state tracking during parsing.
    /// </summary>
    public class LiquidTransferEvent
    {
        /// <summary>
        /// Gets or sets the exact date and time the transfer was finalized (dispense completion).
        /// </summary>
        /// <value>The local timestamp of the event.</value>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the physical channel identifier executing the transfer.
        /// </summary>
        /// <value>The integer representation of the channel number.</value>
        public int ChannelId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the labware from which liquid was aspirated.
        /// </summary>
        /// <value>The source labware identifier, or null if unassigned.</value>
        public string? SourceLabware { get; set; }

        /// <summary>
        /// Gets or sets the specific position on the source labware.
        /// </summary>
        /// <value>The source position identifier, or null if unassigned.</value>
        public string? SourcePositionId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the labware to which liquid was dispensed.
        /// </summary>
        /// <value>The target labware identifier, or null if unassigned.</value>
        public string? TargetLabware { get; set; }

        /// <summary>
        /// Gets or sets the specific position on the target labware.
        /// </summary>
        /// <value>The target position identifier, or null if unassigned.</value>
        public string? TargetPositionId { get; set; }

        /// <summary>
        /// Gets or sets the absolute volume of liquid transferred.
        /// </summary>
        /// <value>The volume in microliters (uL).</value>
        public double Volume { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the tip rack labware used for the transfer.
        /// </summary>
        /// <value>The tip rack identifier, or null if unassigned.</value>
        public string? TipLabwareId { get; set; }

        /// <summary>
        /// Gets or sets the specific position on the tip rack labware.
        /// </summary>
        /// <value>The tip position index.</value>
        public int TipPositionId { get; set; }
    }
}