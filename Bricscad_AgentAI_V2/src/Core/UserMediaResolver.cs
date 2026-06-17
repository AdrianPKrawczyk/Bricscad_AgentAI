using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Bricscad_AgentAI_V2.Core
{
    public class UserMediaMatch
    {
        public string UserFormName { get; set; }
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        public double ToleranceMm { get; set; }
        public bool ExactMatch { get; set; }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0} ({1:F2}x{2:F2}mm, {3}, tolerancja {4:F2}mm)",
                UserFormName, WidthMm, HeightMm,
                ExactMatch ? "exact" : "approximate", ToleranceMm);
        }
    }

    public static class UserMediaResolver
    {
        private const double DefaultToleranceMm = 0.5;

        private static readonly Regex CustomMediaRegex = new Regex(
            @"^\s*(\d+(?:\.\d+)?)\s*[xX×]\s*(\d+(?:\.\d+)?)\s*(_p)?\s*$",
            RegexOptions.Compiled);

        public static bool TryParseCustomMediaName(string mediaName, out double widthMm, out double heightMm, out bool isMultipleBaseUnit)
        {
            widthMm = 0;
            heightMm = 0;
            isMultipleBaseUnit = false;
            if (string.IsNullOrWhiteSpace(mediaName)) return false;

            var match = CustomMediaRegex.Match(mediaName);
            if (!match.Success) return false;

            if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out widthMm))
                return false;
            if (!double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out heightMm))
                return false;

            isMultipleBaseUnit = match.Groups[3].Success;
            return widthMm > 0 && heightMm > 0;
        }

        public static List<UserMediaMatch> FindUserFormatsBySize(
            Win32PrinterCapabilitiesResult capabilities,
            double widthMm,
            double heightMm,
            double toleranceMm = DefaultToleranceMm)
        {
            var matches = new List<UserMediaMatch>();
            if (capabilities == null || !capabilities.QuerySucceeded) return matches;

            foreach (var media in capabilities.UserFormats)
            {
                double wDelta = Math.Abs(media.WidthMm - widthMm);
                double hDelta = Math.Abs(media.HeightMm - heightMm);
                double tolerance = Math.Max(wDelta, hDelta);

                if (wDelta <= toleranceMm && hDelta <= toleranceMm)
                {
                    matches.Add(new UserMediaMatch
                    {
                        UserFormName = media.FormName,
                        WidthMm = media.WidthMm,
                        HeightMm = media.HeightMm,
                        ToleranceMm = tolerance,
                        ExactMatch = wDelta < 0.01 && hDelta < 0.01
                    });
                }
            }

            return matches.OrderBy(m => m.ToleranceMm).ToList();
        }

        public static UserMediaMatch ResolveCustomNameToUserFormat(
            string mediaName,
            Win32PrinterCapabilitiesResult capabilities,
            double toleranceMm = DefaultToleranceMm)
        {
            if (capabilities == null || !capabilities.QuerySucceeded) return null;
            if (!TryParseCustomMediaName(mediaName, out double w, out double h, out bool isMultiple))
                return null;

            var matches = FindUserFormatsBySize(capabilities, w, h, toleranceMm);
            return matches.FirstOrDefault();
        }

        public static bool IsWithinGpdBounds(double widthMm, double heightMm, GpdInfo gpd)
        {
            if (gpd == null || gpd.CustomSize == null) return true;

            var cs = gpd.CustomSize;
            if (widthMm < cs.MinWidthMm - 1.0 || widthMm > cs.MaxWidthMm + 1.0) return false;
            if (heightMm < cs.MinHeightMm - 1.0 || heightMm > cs.MaxHeightMm + 1.0) return false;
            return true;
        }
    }
}