using System;
using System.Text.RegularExpressions;

namespace Phabrico.Miscellaneous
{
    /// <summary>
    /// Helper class for color-based functions
    /// </summary>
    public static class ColorFunctionality
    {
        /// <summary>
        /// This method produces the foreground color for a given background color.
        /// It detects if a the given background color is a dark or a light color.
        /// If the color is dark, "white" will be returned. Otherwise "black" will be returned.
        /// </summary>
        /// <param name="rgbColorString">RGB or RGBA formatted color string</param>
        /// <returns>"white" or "black"</returns>
        public static string WhiteOrBlackTextOnBackground(string rgbColorString)
        {
            Match rgbMatch = RegexSafe.Match(rgbColorString, "rgba?[(] *([0-9]+) *, *([0-9]+) *, *([0-9]+) *(, *[0-9]+ *)?[)]", System.Text.RegularExpressions.RegexOptions.None);
            if (rgbMatch.Success)
            {
                int R = Int32.Parse(rgbMatch.Groups[1].Value);
                int G = Int32.Parse(rgbMatch.Groups[2].Value);
                int B = Int32.Parse(rgbMatch.Groups[3].Value);

                if (R * 0.2126 + G * 0.6052 + B * 0.0582 < 255 / 2)
                {
                    // rgbColorString is a dark color
                    return "white";
                }
                else
                {
                    // rgbColorString is a light color
                    return "black";
                }
            }
            else
            {
                return "";
            }
        }
    }
}
