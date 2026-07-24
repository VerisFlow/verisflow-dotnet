using System.Collections.Generic;

namespace VerisFlow.Mcp.Server.Sample;

public static class McpToolRegistry
{
    /// <summary>
    /// Constructs the MCP JSON schema definitions for all supported tools exposed to Cloud AI clients.
    /// </summary>
    public static object GetToolsDefinition()
    {
        return new
        {
            tools = new object[]
            {
                new
                {
                    name = "list_traces",
                    description = "Search for files in a directory based on time filters.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            directoryPath = new { type = "string", description = "Optional. Defaults to C:\\Program Files (x86)\\HAMILTON\\LogFiles" },
                            timeFilter = new { type = "string", @enum = new[] { "latest", "today", "this_week", "this_month", "custom", "all" } },
                            startTime = new { type = "string", description = "Format: yyyy-MM-ddTHH:mm:ss. Required if timeFilter is custom." },
                            endTime = new { type = "string", description = "Format: yyyy-MM-ddTHH:mm:ss. Required if timeFilter is custom." }
                        },
                        required = new[] { "timeFilter" }
                    }
                },
                new
                {
                    name = "parse_trace",
                    description = "Parse a .trc file and return granular pipetting steps and channel actions.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            filePath = new { type = "string", description = "The full path to the trace file." }
                        },
                        required = new[] { "filePath" }
                    }
                },
                new
                {
                    name = "hamilton_ensure_started",
                    description = "Ensures the Hamilton Run Control process is running and ready to accept commands.",
                    inputSchema = new { type = "object", properties = new { } }
                },
                new
                {
                    name = "hamilton_get_status",
                    description = "Gets the current execution state of the Hamilton system (e.g., Idle, Running, Paused, Error) and the currently loaded method name.",
                    inputSchema = new { type = "object", properties = new { } }
                },
                new
                {
                    name = "hamilton_load_method",
                    description = "Silently loads a .hsl method file into the Hamilton Run Control.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            methodPath = new { type = "string", description = "The full absolute path to the .hsl method file." }
                        },
                        required = new[] { "methodPath" }
                    }
                },
                new
                {
                    name = "hamilton_arrange_window",
                    description = "Arranges the Hamilton Run Control window layout.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            preset = new { type = "string", @enum = new[] { "Custom", "Maximize", "LeftHalf", "RightHalf", "Center" } },
                            customX = new { type = "integer" },
                            customY = new { type = "integer" },
                            customWidth = new { type = "integer" },
                            customHeight = new { type = "integer" }
                        },
                        required = new[] { "preset" }
                    }
                },
                new
                {
                    name = "hamilton_start_run",
                    description = "Clicks the Start button on the Hamilton Run Control to begin execution.",
                    inputSchema = new { type = "object", properties = new { } }
                },
                new
                {
                    name = "hamilton_pause_run",
                    description = "Clicks the Pause button to suspend execution.",
                    inputSchema = new { type = "object", properties = new { } }
                },
                new
                {
                    name = "hamilton_resume_run",
                    description = "Clicks Resume on an active pause dialog.",
                    inputSchema = new { type = "object", properties = new { } }
                },
                new
                {
                    name = "hamilton_abort_run",
                    description = "Aborts the current run and dismisses the confirmation dialog.",
                    inputSchema = new { type = "object", properties = new { } }
                },
                new
                {
                    name = "hamilton_graceful_shutdown",
                    description = "Closes the Hamilton Run Control process gracefully.",
                    inputSchema = new { type = "object", properties = new { } }
                },
                new
                {
                    name = "hamilton_scan_methods",
                    description = "Scans configured directories on the local agent for complete .med and .hsl methods.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            directoryPath = new { type = "string", description = "Optional. A specific absolute path to scan for methods. If omitted, scans the default configured directories." }
                        }
                    }
                }
            }
        };
    }
}