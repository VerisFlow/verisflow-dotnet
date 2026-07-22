using System.Collections.Generic;
using System.IO;
using TraceLogic.Core.Models;
using TraceLogic.Core.Exporting; // Required for ExportColumnInfo

namespace TraceLogic.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for exporting parsed trace data.
    /// </summary>
    public interface ITraceDataExporter
    {
        /// <summary>
        /// Exports data to a specified physical file path.
        /// </summary>
        void Export(IEnumerable<LiquidTransferEvent> data, List<ExportColumnInfo> columns, string filePath);

        /// <summary>
        /// Exports data to a provided memory or network stream.
        /// </summary>
        void Export(IEnumerable<LiquidTransferEvent> data, List<ExportColumnInfo> columns, Stream outputStream);
    }
}