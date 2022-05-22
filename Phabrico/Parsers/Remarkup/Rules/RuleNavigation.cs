using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Remarkup parser for rendering a sequence or a navigation
    /// E.g.
    ///     Wake up &gt; Take a shower &gt; Have breakfast &gt; > Have fun
    /// </summary>
    [RuleXmlTag("NV")]
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
            html = "<span class='remarkup-nav-sequence'>" + html.Substring(htmlNavigationArrowRight.Length) + "</span>";

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
    }
}
