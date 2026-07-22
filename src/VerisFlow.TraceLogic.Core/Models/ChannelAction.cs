using System.Globalization;

namespace TraceLogic.Core.Models
{
    /// <summary>
    /// Represents a detailed action performed by a single pipetting channel.
    /// </summary>
    public class ChannelAction
    {
        /// <summary>
        /// Gets or sets the physical channel identifier on the pipetting head.
        /// </summary>
        /// <value>The integer representation of the channel number.</value>
        public required int ChannelNumber { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier or name of the labware involved in the action.
        /// </summary>
        /// <value>The string identifier of the labware.</value>
        public required string LabwareId { get; set; }

        /// <summary>
        /// Gets or sets the specific position or well index on the target labware.
        /// </summary>
        /// <value>The string identifier of the position.</value>
        public required string PositionId { get; set; }

        /// <summary>
        /// Gets or sets the volume of liquid aspirated or dispensed during the action.
        /// </summary>
        /// <value>The volume in microliters (uL).</value>
        public required double Volume { get; set; }

        /// <summary>
        /// Returns a formatted string that represents the current channel action.
        /// </summary>
        /// <returns>A string containing the channel number, labware, position, and volume.</returns>
        public override string ToString()
        {
#if NETSTANDARD2_0
            return string.Format(CultureInfo.InvariantCulture, "Ch: {0}, Labware: {1}, Pos: {2}, Vol: {3}uL", ChannelNumber, LabwareId, PositionId, Volume);
#else
            return string.Create(CultureInfo.InvariantCulture, $"Ch: {ChannelNumber}, Labware: {LabwareId}, Pos: {PositionId}, Vol: {Volume}uL");
#endif
        }
    }
}