using System;
using System.Windows.Media;

namespace VerisFlow.VenusAuto.Sample.Models
{
    // Represents a single point-in-time capture of UI element properties.
    public class CaptureSnapshot
    {
        /// <summary>
        /// Gets or sets the system timestamp when the UI capture was performed.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the native window handle (HWND) of the target element.
        /// </summary>
        public IntPtr Hwnd { get; set; }

        /// <summary>
        /// Gets or sets the native window handle (HWND) of the parent window.
        /// </summary>
        public IntPtr ParentHwnd { get; set; }

        /// <summary>
        /// Gets or sets the Win32 window class name associated with the element.
        /// </summary>
        public string ClassName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the caption, title, or accessible text of the window or control.
        /// </summary>
        public string WindowText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the absolute horizontal screen coordinate in pixels.
        /// </summary>
        public int AbsoluteX { get; set; }

        /// <summary>
        /// Gets or sets the absolute vertical screen coordinate in pixels.
        /// </summary>
        public int AbsoluteY { get; set; }

        /// <summary>
        /// Gets or sets the horizontal coordinate relative to the parent window bounds.
        /// </summary>
        public int RelativeX { get; set; }

        /// <summary>
        /// Gets or sets the vertical coordinate relative to the parent window bounds.
        /// </summary>
        public int RelativeY { get; set; }

        /// <summary>
        /// Gets or sets the color sampled from the pixel at the target coordinate.
        /// </summary>
        public Color PixelColor { get; set; }

        /// <summary>
        /// Gets the hexadecimal RGB string representation of <see cref="PixelColor"/> (e.g., "#FF0000").
        /// </summary>
        public string ColorHex => $"#{PixelColor.R:X2}{PixelColor.G:X2}{PixelColor.B:X2}";
    }
}