using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Remarkup parser for horizontal line
    /// </summary>
    [RuleXmlTag("R")]
    public class RuleHorizontalRule : RemarkupRule
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
            if (RuleStartOnNewLine)
            {
                Match match = RegexSafe.Match(remarkup, @"^ *(___+|\*\*\*+|\* \*( \*)+|---+|- -( -)+) *(\r?\n|$)", RegexOptions.Singleline);
                if (match.Success == false) return false;

                remarkup = remarkup.Substring(match.Length);
                html = "<hr class='remarkup-hr' />";

                Length = match.Length;

                return true;
            }

            return false;
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
            return "---\n";
        }
    }
}
