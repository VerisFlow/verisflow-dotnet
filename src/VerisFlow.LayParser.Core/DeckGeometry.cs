using System.Globalization;
using System.Text.RegularExpressions;

namespace VerisFlow.LayParser.Core
{
    /// <summary>
    /// Deck geometry of Hamilton instruments.
    /// </summary>
    /// <remarks>
    /// The ML_STAR track values are derived from layouts, not from a deck definition file: carriers placed with
    /// SiteId "{width}T-{track}" have their left edge at <c>100 + (track - 1) * 22.5</c> mm. Treat them as inferred.
    /// </remarks>
    public static class DeckGeometry
    {
        /// <summary>Distance between two ML_STAR tracks, in mm (inferred).</summary>
        public const double MlStarTrackPitch = 22.5;

        /// <summary>X coordinate of the left edge of ML_STAR track 1, in mm (inferred).</summary>
        public const double MlStarTrack1X = 100.0;

        private static readonly Regex TrackSitePattern = new Regex(@"^\s*(\d+)T-(\d+)\s*$", RegexOptions.CultureInvariant);

        /// <summary>
        /// Parses a carrier SiteId of the form "{width}T-{track}", e.g. "6T-30" (6 tracks wide, starting at track 30).
        /// Other SiteIds (e.g. "7T-M1", "WasteBlock") are not track positions and return false.
        /// </summary>
        public static bool TryParseTrackSite(string siteId, out int widthInTracks, out int track)
        {
            widthInTracks = 0;
            track = 0;

            if (string.IsNullOrEmpty(siteId))
            {
                return false;
            }

            var match = TrackSitePattern.Match(siteId);
            return match.Success
                && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out widthInTracks)
                && int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out track);
        }

        /// <summary>
        /// X coordinate of the left edge of an ML_STAR track (inferred).
        /// </summary>
        public static double GetMlStarTrackX(int track)
        {
            return HxCfgText.Round3(MlStarTrack1X + (track - 1) * MlStarTrackPitch);
        }
    }
}