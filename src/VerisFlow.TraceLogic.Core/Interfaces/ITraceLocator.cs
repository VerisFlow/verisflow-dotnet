using System;
using System.Collections.Generic;
using TraceLogic.Core.Enums;
using TraceLogic.Core.Models;

namespace TraceLogic.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for locating trace log files.
    /// </summary>
    public interface ITraceLocator
    {
        /// <summary>
        /// Scans the directory for .trc files and applies the specified time-based filters.
        /// </summary>
        List<TraceFileInfo> FindFiles(
            TimeFilterType filterType = TimeFilterType.All,
            string? targetDirectory = null,
            DateTime? startTime = null,
            DateTime? endTime = null);
    }
}