using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Options;
using TraceLogic.Core.Enums;
using TraceLogic.Core.Models;
using TraceLogic.Core.Interfaces;
using TraceLogic.Core.Options;

namespace TraceLogic.Core.IO
{
    /// <summary>
    /// A dedicated infrastructure service responsible for locating, filtering, 
    /// and retrieving Hamilton Venus trace log files (.trc) from the file system.
    /// </summary>
    public class TraceLocator : ITraceLocator
    {
        private readonly string _systemDefaultDirectory;

        /// <summary>
        /// Initializes a new instance of the TraceLocator class.
        /// </summary>
        /// <param name="options">The configured locator options injected by the framework.</param>
        public TraceLocator(IOptions<TraceLocatorOptions> options)
        {
            _systemDefaultDirectory = options.Value.DefaultLogDirectory;
        }

        /// <summary>
        /// Scans the directory for .trc files and applies the specified time-based filters.
        /// </summary>
        /// <param name="filterType">The predefined time window (e.g., Today, ThisWeek). Defaults to All.</param>
        /// <param name="targetDirectory">The path provided by the AI via MCP. If null, uses the configured default directory.</param>
        /// <param name="startTime">The lower bound for a Custom filter.</param>
        /// <param name="endTime">The upper bound for a Custom filter.</param>
        /// <returns>A list of strongly-typed file information models, ordered by newest first.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown if the target directory does not exist on the disk.</exception>
        public List<TraceFileInfo> FindFiles(
            TimeFilterType filterType = TimeFilterType.All,
            string? targetDirectory = null,
            DateTime? startTime = null,
            DateTime? endTime = null)
        {
            string? searchPath = string.IsNullOrWhiteSpace(targetDirectory)
                ? _systemDefaultDirectory
                : targetDirectory;

            if (!Directory.Exists(searchPath))
            {
                throw new DirectoryNotFoundException($"The specified Hamilton log directory does not exist: {searchPath}");
            }

            var directoryInfo = new DirectoryInfo(searchPath);

            var allFiles = directoryInfo.GetFiles("*.trc")
                                        .OrderByDescending(f => f.LastWriteTime)
                                        .ToList();

            DateTime now = DateTime.Now;

            IEnumerable<FileInfo> filteredFiles = filterType switch
            {
                TimeFilterType.Latest => allFiles.Take(1),
                TimeFilterType.Today => allFiles.Where(f => f.LastWriteTime.Date == now.Date),
                TimeFilterType.ThisWeek => allFiles.Where(f =>
                    GetIso8601WeekOfYear(f.LastWriteTime) == GetIso8601WeekOfYear(now) &&
                    f.LastWriteTime.Year == now.Year),
                TimeFilterType.ThisMonth => allFiles.Where(f =>
                    f.LastWriteTime.Month == now.Month &&
                    f.LastWriteTime.Year == now.Year),
                TimeFilterType.Custom => ApplyCustomTimeFilter(allFiles, startTime, endTime),
                _ => allFiles
            };

            return filteredFiles.Select(f => new TraceFileInfo
            {
                FileName = f.Name,
                FullPath = f.FullName,
                SizeKB = Math.Round(f.Length / 1024.0, 2),
                LastModified = f.LastWriteTime
            }).ToList();
        }

        /// <summary>
        /// Applies a precise start and/or end time boundary to the file list.
        /// </summary>
        /// <param name="files">The full, unfiltered collection of log files.</param>
        /// <param name="start">The optional starting timestamp to enforce as the lower boundary.</param>
        /// <param name="end">The optional ending timestamp to enforce as the upper boundary.</param>
        /// <returns>An enumerable collection of files that fall within the bounded time span.</returns>
        private static IEnumerable<FileInfo> ApplyCustomTimeFilter(List<FileInfo> files, DateTime? start, DateTime? end)
        {
            var query = files.AsEnumerable();

            if (start.HasValue)
            {
                query = query.Where(f => f.LastWriteTime >= start.Value);
            }

            if (end.HasValue)
            {
                query = query.Where(f => f.LastWriteTime <= end.Value);
            }

            return query;
        }

        /// <summary>
        /// Calculates the ISO 8601 week number of a given date.
        /// This is required because standard .NET week calculation can vary wildly 
        /// depending on regional system settings (e.g., Sunday vs Monday start).
        /// </summary>
        /// <param name="time">The date and time structure to evaluate.</param>
        /// <returns>The calculated integer week number adhering to the ISO 8601 specification.</returns>
        private static int GetIso8601WeekOfYear(DateTime time)
        {
            var day = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(time);
            if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
            {
                time = time.AddDays(3);
            }

            return System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                time,
                System.Globalization.CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Monday);
        }
    }
}