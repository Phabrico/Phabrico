using Phabrico.Http;
using Phabrico.Miscellaneous;
using System.Linq;
using System.Text.RegularExpressions;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Remarkup parser for rendering a sequence or a navigation
    /// E.g.
    ///     Wake up &gt; Take a shower &gt; Have breakfast &gt; > Have fun
    /// </summary>
    public class RuleNavigation : RemarkupRule
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
            Match match = RegexSafe.Match(remarkup, "^{nav ([^}]*)}", RegexOptions.Singleline);
            if (match.Success == false) return false;

            string[] navigationSequenceItems = match.Groups[1]
                                                    .Value
                                                    .Split('>')
                                                    .Select(item => item.Trim(' ', '\r', '\n'))
                                                    .ToArray();

            string htmlNavigationArrowRight = "<span class='remarkup-nav-sequence-arrow'>&#x2B62;</span>";
            foreach (string navigationSequenceItem in navigationSequenceItems)
            {
                Match navigationSequenceItemMatch = RegexSafe.Match(navigationSequenceItem, "((((icon=([^,]*))|(name=([^,]*))|(href=([^,]*))|(type=([^,]*))),? *)*)", RegexOptions.Singleline);
                string icon = navigationSequenceItemMatch.Groups[5].Value;
                string name = navigationSequenceItemMatch.Groups[7].Value;
                string href = navigationSequenceItemMatch.Groups[9].Value;
                string type = navigationSequenceItemMatch.Groups[11].Value;
                string fulltext = navigationSequenceItemMatch.Groups[5].Success == false &&
                                    navigationSequenceItemMatch.Groups[7].Success == false &&
                                    navigationSequenceItemMatch.Groups[9].Success == false &&
                                    navigationSequenceItemMatch.Groups[11].Success == false ?
                                    navigationSequenceItem : "";

                html += htmlNavigationArrowRight;

                if (string.IsNullOrEmpty(href) == false)
                {
                    html += "<a class='remarkup-nav-sequence-anchor' href='" + href + "'>";
                }

                html += "<span class='remarkup-nav-sequence-item ";
                if (string.IsNullOrEmpty(type) == false)
                {
                    html += type.ToLower();
                }
                html += "'>";

                if (string.IsNullOrEmpty(icon) == false)
                {
                    html += "<span class='phui-icon-view phui-font-fa fa-" + icon + "'></span>";
                }

                if (string.IsNullOrEmpty(fulltext) == false)
                {
                    html += fulltext;
                }

                if (string.IsNullOrEmpty(name) == false)
                {
                    html += name;
                }

                html += "</span>";

                if (string.IsNullOrEmpty(href) == false)
                {
                    html += "</a>";
                }
            }

            remarkup = remarkup.Substring(match.Length);
            html = "<div class='remarkup-nav-sequence'>" + html.Substring(htmlNavigationArrowRight.Length) + "</div>";

            Length = match.Length;

            return true;
        }
    }
}
