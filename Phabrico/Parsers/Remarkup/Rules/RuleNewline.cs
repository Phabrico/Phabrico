using Phabrico.Http;
using Phabrico.Miscellaneous;
using System.Linq;
using System.Text.RegularExpressions;

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
            if (match.Value.Count(character => character == '\n') >= 2  &&                                                          // multiple newlines
                remarkup.StartsWith("  ") == false &&                                                                               // next line is NOT a codeblock
                remarkup.StartsWith("```") == false &&                                                                              // next line is NOT a codeblock
                remarkup.StartsWith("lang=") == false &&                                                                            // next line is NOT a codeblock
                remarkup.StartsWith("#") == false &&                                                                                // next line is NOT a header
                remarkup.StartsWith("=") == false &&                                                                                // next line is NOT a header
                ( remarkup.Replace("\r", "").Split('\n').Skip(1).FirstOrDefault()?.Any() == false ||                                // next line is NOT a header
                  remarkup.Replace("\r", "").Split('\n').Skip(1).FirstOrDefault()?.Any(character => character != '=') == true       // next line is NOT a header
                ) &&
                remarkup.StartsWith("---") == false &&                                                                              // next line is NOT a line
                remarkup.StartsWith("(IMPORTANT)") == false &&                                                                      // next line is NOT a notification
                remarkup.StartsWith("(NOTE)") == false &&                                                                           // next line is NOT a notification
                remarkup.StartsWith("(WARNING)") == false &&                                                                        // next line is NOT a notification
                remarkup.StartsWith("IMPORTANT:") == false &&                                                                       // next line is NOT a notification
                remarkup.StartsWith("NOTE:") == false &&                                                                            // next line is NOT a notification
                remarkup.StartsWith("WARNING:") == false &&                                                                         // next line is NOT a notification
                remarkup.StartsWith("|") == false &&                                                                                // next line is NOT a table
                remarkup.StartsWith(" |") == false &&                                                                               // next line is NOT a table
                remarkup.StartsWith("<table>") == false &&                                                                          // next line is NOT a table
                remarkup.StartsWith("{nav ") == false &&                                                                            // next line is NOT a navigation sequence
                (ParentRemarkupRule is RuleList) == false &&                                                                        // parent rule is not a list element
                (ParentRemarkupRule?.ParentRemarkupRule is RuleList) == false &&                                                    // parent rule is not a list element
                (ParentRemarkupRule?.ParentRemarkupRule?.ParentRemarkupRule is RuleList) == false &&                                // parent rule is not a list element
                remarkup.TrimStart().StartsWith("- ") == false &&                                                                   // next rule is not a list element
                remarkup.TrimStart().StartsWith("* ") == false &&                                                                   // next rule is not a list element
                remarkup.TrimStart().StartsWith("# ") == false &&                                                                   // next rule is not a list element
                (TokenList.LastOrDefault() is RuleNavigation) == false &&                                                           // previous rule is not a navigation sequence
                (TokenList.LastOrDefault() is RuleNotification) == false &&                                                         // previous rule is not a notification
                (TokenList.LastOrDefault() is RuleTable) == false &&                                                                // previous rule is not a table
                (ParentRemarkupRule != null  ||  TokenList.Any() == true) &&                                                        // not first character(s)
                remarkup.All(character => character == '\r' || character == '\n') == false                                          // not last character(s)
               )
            {
                html = "<p class='paragraph'></p>";
            }
            else
            {
                html = "<br>";
            }

            Length = match.Length;

            return true;
        }
    }
}
