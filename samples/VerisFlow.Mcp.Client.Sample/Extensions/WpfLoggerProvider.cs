using Microsoft.Extensions.Logging;
using System;

namespace VerisFlow.Mcp.Client.Sample;

/// <summary>
/// Implements an <see cref="ILoggerProvider"/> that routes log messages to a WPF UI output action.
/// </summary>
public class WpfLoggerProvider : ILoggerProvider
{
    /// <summary>
    /// The action delegate used to deliver formatted log strings to the UI.
    /// </summary>
    private readonly Action<string> _logAction;

    /// <summary>
    /// Initializes a new instance of the <see cref="WpfLoggerProvider"/> class with the specified log action.
    /// </summary>
    /// <param name="logAction">The delegate executed when a new log message is formatted.</param>
    public WpfLoggerProvider(Action<string> logAction)
    {
        _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));
    }

    /// <summary>
    /// Creates a new <see cref="ILogger"/> instance for the specified category name.
    /// </summary>
    /// <param name="categoryName">The category name for messages produced by the logger.</param>
    /// <returns>A new <see cref="ILogger"/> instance.</returns>
    public ILogger CreateLogger(string categoryName)
    {
        return new WpfLogger(_logAction);
    }

    /// <summary>
    /// Disposes the logger provider and suppresses finalization.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Custom <see cref="ILogger"/> implementation that delegates log message processing to the WPF log action.
    /// </summary>
    private class WpfLogger : ILogger
    {
        /// <summary>
        /// The action delegate used to deliver formatted log strings.
        /// </summary>
        private readonly Action<string> _logAction;

        /// <summary>
        /// Initializes a new instance of the <see cref="WpfLogger"/> class.
        /// </summary>
        /// <param name="logAction">The delegate executed to display formatted log output.</param>
        public WpfLogger(Action<string> logAction)
        {
            _logAction = logAction;
        }

        /// <summary>
        /// Begins a logical operation scope.
        /// </summary>
        /// <typeparam name="TState">The type of the state for which to begin scope.</typeparam>
        /// <param name="state">The identifier for the scope.</param>
        /// <returns>Always returns <c>null</c> as logging scopes are not tracked in this provider.</returns>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <summary>
        /// Determines whether the specified log level is enabled.
        /// </summary>
        /// <param name="logLevel">The log level to check.</param>
        /// <returns><c>true</c> if the log level is active; otherwise, <c>false</c>.</returns>
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        /// <summary>
        /// Formats and writes a log entry if the specified log level is enabled.
        /// </summary>
        /// <typeparam name="TState">The type of the object to be written.</typeparam>
        /// <param name="logLevel">Entry will be written on this level.</param>
        /// <param name="eventId">Id of the event.</param>
        /// <param name="state">The entry to be written.</param>
        /// <param name="exception">The exception related to this entry.</param>
        /// <param name="formatter">Function to create a <see cref="string"/> message of the state and exception.</param>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            try
            {
                var message = formatter(state, exception);
                if (!string.IsNullOrEmpty(message))
                {
                    _logAction(message);
                }
            }
            catch
            {
                // Ensure logger formatting failures do not disrupt caller execution flow
            }
        }
    }
}