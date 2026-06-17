using System;
using System.Diagnostics;
using Bricscad_AgentAI_V2.Core;

namespace Bricscad_AgentAI_V2.Tests.Core
{
    public static class UserMediaResolverTests
    {
        public static void RunTests()
        {
            TestParseCustomMediaNameValid();
            TestParseCustomMediaNameInvalid();
            TestParseCustomMediaNameWithPSuffix();
            TestParseCustomMediaNameWithDecimal();
            TestIsWithinGpdBoundsInside();
            TestIsWithinGpdBoundsOutside();
            TestFindUserFormatsBySizeNoCapabilities();
            TestAPanelsFoldableFormula();
            TestBuildAPanelsFoldableSize();
            TestDescribeFoldPattern();
        }

        private static void TestParseCustomMediaNameValid()
        {
            bool ok = UserMediaResolver.TryParseCustomMediaName("297x600", out double w, out double h, out bool isMult);
            Debug.Assert(ok, "297x600 should parse.");
            Debug.Assert(Math.Abs(w - 297.0) < 0.001, "297x600 width should be 297. Got: " + w);
            Debug.Assert(Math.Abs(h - 600.0) < 0.001, "297x600 height should be 600. Got: " + h);
            Debug.Assert(!isMult, "297x600 should not be marked as _p.");
        }

        private static void TestParseCustomMediaNameInvalid()
        {
            bool ok = UserMediaResolver.TryParseCustomMediaName("A4", out double w, out double h, out bool isMult);
            Debug.Assert(!ok, "A4 should not parse as custom format (no 'x').");

            ok = UserMediaResolver.TryParseCustomMediaName("", out w, out h, out isMult);
            Debug.Assert(!ok, "Empty should not parse.");

            ok = UserMediaResolver.TryParseCustomMediaName(null, out w, out h, out isMult);
            Debug.Assert(!ok, "Null should not parse.");
        }

        private static void TestParseCustomMediaNameWithPSuffix()
        {
            bool ok = UserMediaResolver.TryParseCustomMediaName("297x1320_p", out double w, out double h, out bool isMult);
            Debug.Assert(ok, "297x1320_p should parse.");
            Debug.Assert(Math.Abs(w - 297.0) < 0.001, "297x1320_p width should be 297. Got: " + w);
            Debug.Assert(Math.Abs(h - 1320.0) < 0.001, "297x1320_p height should be 1320. Got: " + h);
            Debug.Assert(isMult, "297x1320_p should be marked as multiple base unit.");
        }

        private static void TestParseCustomMediaNameWithDecimal()
        {
            bool ok = UserMediaResolver.TryParseCustomMediaName("297.5x600.25", out double w, out double h, out _);
            Debug.Assert(ok, "297.5x600.25 should parse.");
            Debug.Assert(Math.Abs(w - 297.5) < 0.001, "Decimal width. Got: " + w);
            Debug.Assert(Math.Abs(h - 600.25) < 0.001, "Decimal height. Got: " + h);
        }

        private static void TestIsWithinGpdBoundsInside()
        {
            var gpd = new GpdInfo
            {
                CustomSize = new GpdCustomSize
                {
                    UnitsPerInch = 1200,
                    MinWidthUnits = 3732,
                    MinHeightUnits = 6614,
                    MaxWidthUnits = 28800,
                    MaxHeightUnits = 4299213
                }
            };

            bool ok = UserMediaResolver.IsWithinGpdBounds(297, 600, gpd);
            Debug.Assert(ok, "297x600 (T120 range 79-609mm wide, 140-91000mm long) should be within bounds.");
        }

        private static void TestIsWithinGpdBoundsOutside()
        {
            var gpd = new GpdInfo
            {
                CustomSize = new GpdCustomSize
                {
                    UnitsPerInch = 1200,
                    MinWidthUnits = 3732,
                    MinHeightUnits = 6614,
                    MaxWidthUnits = 28800,
                    MaxHeightUnits = 4299213
                }
            };

            bool tooWide = UserMediaResolver.IsWithinGpdBounds(1000, 600, gpd);
            Debug.Assert(!tooWide, "1000mm width exceeds T120 max 609.6mm.");

            bool tooShort = UserMediaResolver.IsWithinGpdBounds(297, 50, gpd);
            Debug.Assert(!tooShort, "50mm height is below T120 min 140mm.");
        }

        private static void TestFindUserFormatsBySizeNoCapabilities()
        {
            var matches = UserMediaResolver.FindUserFormatsBySize(null, 297, 600);
            Debug.Assert(matches.Count == 0, "Null capabilities should return empty list.");
        }

        private static void TestAPanelsFoldableFormula()
        {
            Debug.Assert(UserMediaResolver.CountAPanelsForFoldableSize(580) == 3,
                "580mm = 3*185+25 = 3 panele A4");
            Debug.Assert(UserMediaResolver.CountAPanelsForFoldableSize(950) == 5,
                "950mm = 5*185+25 = 5 paneli A4");
            Debug.Assert(UserMediaResolver.CountAPanelsForFoldableSize(1320) == 7,
                "1320mm = 7*185+25 = 7 paneli A4 (594x1320_p)");
            Debug.Assert(UserMediaResolver.CountAPanelsForFoldableSize(1690) == 9,
                "1690mm = 9*185+25 = 9 paneli A4");
            Debug.Assert(UserMediaResolver.CountAPanelsForFoldableSize(2060) == 11,
                "2060mm = 11*185+25 = 11 paneli A4");
        }

        private static void TestBuildAPanelsFoldableSize()
        {
            double h;
            bool ok = UserMediaResolver.TryBuildAPanelsFoldableSize(297, 7, 25, out h);
            Debug.Assert(ok, "Build should succeed for 7 panels");
            Debug.Assert(Math.Abs(h - 1320.0) < 0.001, "7*185+25=1320. Got: " + h);

            ok = UserMediaResolver.TryBuildAPanelsFoldableSize(594, 9, 25, out h);
            Debug.Assert(ok, "Build should succeed for 9 panels at 594mm width");
            Debug.Assert(Math.Abs(h - 1690.0) < 0.001, "9*185+25=1690. Got: " + h);
        }

        private static void TestDescribeFoldPattern()
        {
            string desc = UserMediaResolver.DescribeFoldPattern(594, 1320, true);
            Debug.Assert(!string.IsNullOrEmpty(desc), "Description should be generated");
            Debug.Assert(desc.Contains("1320"), "Should mention 1320mm");
            Debug.Assert(desc.Contains("7 paneli") || desc.Contains("7 panel"), "Should mention 7 panels");

            string noDesc = UserMediaResolver.DescribeFoldPattern(594, 1320, false);
            Debug.Assert(noDesc == null, "Should return null for non-foldable");
        }
    }
}