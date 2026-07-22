using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using TraceLogic.Core.Interfaces;
using TraceLogic.Core.Models;

namespace TraceLogic.Core.Exporting
{
    /// <summary>
    /// Handles exporting data to various file formats.
    /// This version is simplified to only support CSV export.
    /// </summary>
    public class CsvDataExporter : ITraceDataExporter
    {
        /// <summary>
        /// Exports a list of liquid transfer events to a CSV file.
        /// </summary>
        /// <param name="data">The collection of finalized liquid transfer events to be exported.</param>
        /// <param name="columns">The structural schema dictating which properties are exported and their associated header names.</param>
        /// <param name="filePath">The absolute or relative system path where the CSV output file will be written.</param>
        public void Export(IEnumerable<LiquidTransferEvent> data, List<ExportColumnInfo> columns, string filePath)
        {
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            Export(data, columns, fileStream);
        }

        /// <summary>
        /// Exports a list of liquid transfer events directly to an open stream.
        /// </summary>
        /// <param name="data">The collection of finalized liquid transfer events to be exported.</param>
        /// <param name="columns">The structural schema dictating which properties are exported and their associated header names.</param>
        /// <param name="outputStream">The stream where the CSV data will be written.</param>
        public void Export(IEnumerable<LiquidTransferEvent> data, List<ExportColumnInfo> columns, Stream outputStream)
        {
            // Use 4096 (4 KB) buffer size for optimal I/O throughput and netstandard2.0 compatibility
            using var writer = new StreamWriter(outputStream, new UTF8Encoding(false), bufferSize: 4096, leaveOpen: true);

            writer.WriteLine(string.Join(",", columns.Select(c => c.Header)));

            foreach (var transfer in data)
            {
                var line = string.Join(",", columns.Select(c =>
                {
                    var value = GetPropertyValue(transfer, c.PropertyName);
                    var stringValue = value switch
                    {
                        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                        _ => value?.ToString() ?? string.Empty
                    };
                    return stringValue;
                }));
                writer.WriteLine(line);
            }
        }

        /// <summary>
        /// Gets a property's value from an object using reflection.
        /// </summary>
        /// <param name="obj">The underlying target object.</param>
        /// <param name="propertyName">The string name of the target property to extract.</param>
        /// <returns>The underlying object value, or null if the property cannot be found or the object is null.</returns>
        private static object? GetPropertyValue(object obj, string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName) || obj == null)
            {
                return null;
            }
            return obj.GetType().GetProperty(propertyName)?.GetValue(obj, null);
        }
    }

    /// <summary>
    /// A simple DTO to carry column information from UI to the exporter.
    /// </summary>
    public class ExportColumnInfo
    {
        /// <summary>
        /// Gets or sets the display text intended for the column header in the exported file.
        /// </summary>
        /// <value>The header string.</value>
        public required string Header { get; set; }

        /// <summary>
        /// Gets or sets the exact string match of the underlying property name to extract data from.
        /// </summary>
        /// <value>The reflection property name.</value>
        public required string PropertyName { get; set; }
    }
}