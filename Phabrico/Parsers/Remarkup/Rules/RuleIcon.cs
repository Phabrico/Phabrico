using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Remarkup parser for (FontAwesome) icons
    /// </summary>
    [RuleXmlTag("IC")]
    public class RuleIcon : RemarkupRule
    {
        /// <summary>
        /// Converts Remarkup encoded text into HTML
        /// </summary>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="browser">Reference to web browser</param>
        /// <param name="url">URL from where this method was executed</param>
        /// <param name="remarkup">Remarkup content to be converted</param>
        /// <param name="html">Translated HTML</param>
        /// <returns>True if the content could successfully be converted</returns>
        public override bool ToHTML(Storage.Database database, Browser browser, string url, ref string remarkup, out string html)
        {
            html = "";
            Match match = RegexSafe.Match(remarkup, @"^{icon +([^ ,}]*)(,? *([^ }]*) *)*}", RegexOptions.Singleline);
            if (match.Success == false) return false;

            string icon = match.Groups[1].Value;
            Dictionary<string,string> parameters = remarkup.Substring(match.Groups[1].Index + match.Groups[1].Length, 
                                                                      match.Length - match.Groups[1].Index - match.Groups[1].Length - 1
                                                                     )
                                                        .Split(',')
                                                        .Select(item => item.Split('='))
                                                        .ToDictionary(
                                                              key => key.FirstOrDefault().Trim(), 
                                                              value => string.Join("=", value.Skip(1))
                                                        );

            string animation = "";
            string color;
            if (parameters.ContainsKey("spin")) animation = "fa-spin";
            parameters.TryGetValue("color", out color);

            if (color != null)
            {
                color = FixColorValueIfNeeded(color);
            }
            else
            {
                color = "unset";
            }

            remarkup = remarkup.Substring(match.Length);
            html = string.Format("<span class='visual-only phui-icon-view phui-font-fa fa-{0} {1}' style='{2};'></span>", 
                icon, 
                animation, 
                string.IsNullOrWhiteSpace(color) ? "" : string.Format("color:{0};", color));

            Length = match.Length;

            return true;
        }

        /// <summary>
        /// Generates remarkup content
        /// </summary>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="browser">Reference to browser</param>
        /// <param name="innerText">Text between XML opening and closing tags</param>
        /// <param name="attributes">XML attributes</param>
        /// <returns>Remarkup content, translated from the XML</returns>
        internal override string ConvertXmlToRemarkup(Database database, Browser browser, string innerText, Dictionary<string, string> attributes)
        {
            return innerText;
        }

        /// <summary>
        /// Phabricator uses some different color values for some given color names
        /// These method will 'correct' these color values
        /// </summary>
        /// <param name="color">Name of color to be corrected</param>
        /// <returns>Value of color according to Phabricator</returns>
        private string FixColorValueIfNeeded(string color)
        {
            Dictionary<string,string> phabricatorColors = new Dictionary<string, string>()
            {
                { "dark",           "#4B4D51" },
                { "bluegrey",       "#6B748C" },
                { "white",          "#fff" },
                { "red",            "#c0392b" },
                { "orange",         "#e67e22" },
                { "yellow",         "#f1c40f" },
                { "green",          "#139543" },
                { "blue",           "#2980b9" },
                { "sky",            "#3498db" },
                { "indigo",         "#6e5cb6" },
                { "pink",           "#da49be" },
                { "fire",           "#e62f17" },
                { "violet",         "#8e44ad" },
                { "lightbluetext",  "#8C98B8" },
                { "lightgreytext",  "rgba(55,55,55,0.3)" },
                { "grey",           "rgba(55,55,55,0.3)" }
            };

            string phabricoColor;
            if (phabricatorColors.TryGetValue(color.ToLowerInvariant(), out phabricoColor) == false)
            {
                phabricoColor = color.ToLowerInvariant();
            }

            return phabricoColor;
        }
    }
}
