using System.Text.RegularExpressions;

using Phabrico.Http;
using Phabrico.Miscellaneous;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Remarkup parser for newlines
    /// </summary>
    [RulePriority(100)]
    public class RuleNewline : RemarkupRule
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
            Match match = RegexSafe.Match(remarkup, @"^(\r?\n)+", System.Text.RegularExpressions.RegexOptions.Singleline);
            if (match.Success == false) return false;

            remarkup = remarkup.Substring(match.Length);
            html = "<br>";

            Length = match.Length;

            return true;
        }
    }
}
