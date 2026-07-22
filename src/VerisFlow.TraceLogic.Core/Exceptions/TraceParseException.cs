using System;

namespace TraceLogic.Core.Exceptions
{
    /// <summary>
    /// Represents errors that occur during the parsing of a trace log file.
    /// </summary>
    public class TraceParseException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the TraceParseException class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public TraceParseException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the TraceParseException class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public TraceParseException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}