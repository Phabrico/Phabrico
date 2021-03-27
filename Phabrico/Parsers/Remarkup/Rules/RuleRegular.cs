using System.Text.RegularExpressions;

using Phabrico.Http;
using Phabrico.Miscellaneous;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// This rule will check for sequential alphanumeric characters.
    /// This rule is executed first to improve the performance of the Remarkup decoding
    /// </summary>
    [RulePriority(-100)]
    public class RuleRegular : RemarkupRule
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

            if (RegexSafe.IsMatch(remarkup, "^(https?://|mailto:|tel:)", RegexOptions.Singleline)) return false;  // hyperlink syntax
            if (RegexSafe.IsMatch(remarkup, @"^((?=.{0,64}@.{0,255}$)(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*|""(?:[\x01-\x08\x0b\x0c\x0e-\x1f\x21\x23-\x5b\x5d-\x7f]|\\[\x01-\x09\x0b\x0c\x0e-\x7f])*""))@(?:(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?|\[(?:(?:(2(5[0-5]|[0-4][0-9])|1[0-9][0-9]|[1-9]?[0-9]))\.){3}(?:(2(5[0-5]|[0-4][0-9])|1[0-9][0-9]|[1-9]?[0-9])|[a-z0-9-]*[a-z0-9]:(?:[\x01-\x08\x0b\x0c\x0e-\x1f\x21-\x5a\x53-\x7f]|\\[\x01-\x09\x0b\x0c\x0e-\x7f])+)\])", RegexOptions.Singleline)) return false;  // email-address syntax

            if (RuleStartOnNewLine)
            {
                if (RegexSafe.IsMatch(remarkup, @"^(IMPORTANT|NOTE|WARNING):(\r?\n)?(.+?(?=\n\r?\n|$))", RegexOptions.Singleline)) return false;  // notification syntax
                if (RegexSafe.IsMatch(remarkup, @"^\((IMPORTANT|NOTE|WARNING)\)(\r?\n)?(.+?(?=\n\r?\n|$))", RegexOptions.Singleline)) return false;  // notification syntax
            }

            // check first line
            Match match = RegexSafe.Match(remarkup, "^[^\n]*", RegexOptions.Singleline);
            if (match.Success)
            {
                if (match.Value.Contains("{{{")) return false;  // contains interpreter syntax
            }

            // check for numeric characters, but make sure it's no numeric list item (which has a ') ' or a '. ) after it)
            match = RegexSafe.Match(remarkup, @"^[1-9][0-9]*[).] ", System.Text.RegularExpressions.RegexOptions.Singleline);
            if (match.Success) return false;

            // check for alphanumeric characters, but make sure there's no underlining (by '=' or '-' characters) on the next line (=> header syntax)
            match = RegexSafe.Match(remarkup, @"^[A-Za-z0-9]+(?![^\n]*\n[=-])", System.Text.RegularExpressions.RegexOptions.Singleline);
            if (match.Success == false) return false;

            remarkup = remarkup.Substring(match.Length);
            html = match.Value;

            Length = match.Length;

            return true;
        }
    }
}
