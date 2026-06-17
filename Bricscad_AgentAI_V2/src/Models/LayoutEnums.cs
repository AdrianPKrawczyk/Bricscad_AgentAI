using System;
using System.Collections.Generic;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Models
{
    public static class LayoutEnums
    {
        public static readonly Dictionary<string, int> PlotTypeMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Layout", 0 },
            { "Extents", 1 },
            { "Display", 2 },
            { "Limits", 3 },
            { "View", 4 },
            { "Window", 5 }
        };

        public static readonly Dictionary<string, int> PlotRotationMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Zero", 0 },
            { "Ninety", 1 },
            { "OneEighty", 2 },
            { "TwoSeventy", 3 }
        };

        public static readonly Dictionary<string, int> StdScaleTypeMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "ScaleToFit", 0 },
            { "1_1", 1 },
            { "1_2", 2 },
            { "1_4", 3 },
            { "1_5", 4 },
            { "1_8", 5 },
            { "1_10", 6 },
            { "1_16", 7 },
            { "1_20", 8 },
            { "1_30", 9 },
            { "1_40", 10 },
            { "1_50", 11 },
            { "1_100", 12 },
            { "2_1", 13 },
            { "4_1", 14 },
            { "8_1", 15 },
            { "10_1", 16 },
            { "100_1", 17 }
        };

        public static readonly Dictionary<string, int> ShadePlotMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "AsDisplayed", 0 },
            { "Wireframe", 1 },
            { "Hidden", 2 },
            { "Rendered", 3 }
        };

        public static readonly Dictionary<string, int> ShadePlotResLevelMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Draft", 0 },
            { "Preview", 1 },
            { "Normal", 2 },
            { "Presentation", 3 },
            { "Maximum", 4 },
            { "Custom", 5 }
        };

        public static readonly Dictionary<string, int> PlotPaperUnitsMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Inches", 0 },
            { "Millimeters", 1 },
            { "Pixels", 2 }
        };

        public static readonly Dictionary<string, string> PlotStyleTypeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ColorDependent", "CTB" },
            { "Named", "STB" }
        };

        public static string FormatAllowed<TValue>(Dictionary<string, TValue> map)
        {
            return string.Join(", ", map.Keys);
        }

        public static bool TryParseEnum<TEnum>(string input, Dictionary<string, TEnum> map, out TEnum result)
        {
            if (!string.IsNullOrEmpty(input) && map.TryGetValue(input, out result))
            {
                return true;
            }
            result = default;
            return false;
        }

        public static bool TryParseDouble(string input, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;
            return double.TryParse(
                input.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
        }
    }
}