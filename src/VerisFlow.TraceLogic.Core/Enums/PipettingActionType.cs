namespace TraceLogic.Core.Enums
{
    /// <summary>
    /// Defines the specific type of a pipetting action.
    /// </summary>
    public enum PipettingActionType
    {
        /// <summary>
        /// Represents an unrecognized or unparsed pipetting action.
        /// </summary>
        Unknown,

        /// <summary>
        /// Represents the initialization sequence of the pipetting arm or channels.
        /// </summary>
        Initialize,

        /// <summary>
        /// Represents the action of drawing liquid into the tips.
        /// </summary>
        Aspirate,

        /// <summary>
        /// Represents the action of releasing liquid from the tips.
        /// </summary>
        Dispense,

        /// <summary>
        /// Represents the action of mounting new disposable tips onto the channels.
        /// </summary>
        PickupTip,

        /// <summary>
        /// Represents the action of discarding disposable tips from the channels.
        /// </summary>
        EjectTip
    }
}