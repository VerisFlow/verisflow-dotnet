using System;
using System.Windows.Media;

namespace VerisFlow.VenusAuto.Sample.Models
{
    /// <summary>
    /// A point-in-time capture of the window under the cursor, with the IDs needed to configure RunControlIds.
    /// </summary>
    public class CaptureSnapshot
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>Deepest child window at the cursor, including disabled controls.</summary>
        public IntPtr Hwnd { get; set; }

        public IntPtr ParentHwnd { get; set; }

        /// <summary>Top-level window containing <see cref="Hwnd"/>.</summary>
        public IntPtr RootHwnd { get; set; }

        /// <summary>Owner of the top-level window (for dialogs, usually the main window).</summary>
        public IntPtr RootOwnerHwnd { get; set; }

        public string ClassName { get; set; } = string.Empty;

        public string WindowText { get; set; } = string.Empty;

        /// <summary>Control ID (GetDlgCtrlID); 0 for top-level windows.</summary>
        public int ControlId { get; set; }

        public int Style { get; set; }

        public uint ProcessId { get; set; }

        public int AbsoluteX { get; set; }

        public int AbsoluteY { get; set; }

        /// <summary>Client coordinates relative to the top-level window.</summary>
        public int RelativeX { get; set; }

        public int RelativeY { get; set; }

        public Color PixelColor { get; set; }

        public string ColorHex => $"#{PixelColor.R:X2}{PixelColor.G:X2}{PixelColor.B:X2}";

        public string HwndText => Hex(Hwnd);

        public string ParentHwndText => Hex(ParentHwnd);

        public string RootHwndText => Hex(RootHwnd);

        public string RootOwnerHwndText => RootOwnerHwnd == IntPtr.Zero ? "none" : Hex(RootOwnerHwnd);

        public string ControlIdText => $"{ControlId} (0x{ControlId:X})";

        public string StyleText => $"0x{Style:X8}";

        public string AbsoluteText => $"{AbsoluteX}, {AbsoluteY}";

        public string RelativeText => $"{RelativeX}, {RelativeY}";

        private static string Hex(IntPtr handle) => $"0x{handle.ToInt64():X8}";
    }
}