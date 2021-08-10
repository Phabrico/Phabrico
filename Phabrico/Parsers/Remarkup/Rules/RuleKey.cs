using Phabrico.Http;
using Phabrico.Miscellaneous;
using System;
using System.Text.RegularExpressions;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Remarkup parser for rendering keystrokes
    /// </summary>
    public class RuleKey : RemarkupRule
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
            Match match = RegexSafe.Match(remarkup, "^{key ([^}]*)}", RegexOptions.Singleline);
            if (match.Success == false) return false;

            // convert "{key xxx}" to a KBD statement
            string keyboardShortcut = match.Groups[1].Value.ToUpper();
            string encodedKeyboardShortcut = "";
            string kbdJoin = "<span class='kbd-join'>+</span>";
            foreach (string key in keyboardShortcut.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                encodedKeyboardShortcut += kbdJoin;
                encodedKeyboardShortcut += "<kbd>";
                encodedKeyboardShortcut += (key.Equals("CMD") || key.Equals("COMMAND")) ? "&#8984;" :
                                            (key.Equals("OPT") || key.Equals("OPTION")) ? "&#8997;" :
                                            key.Equals("SHIFT") ? "&#8679;" :
                                            key.Equals("TAB") ? "&#x21E5;" :
                                            (key.Equals("ESC") || key.Equals("ESCAPE")) ? "&#x238B;" :
                                            (key.Equals("UP") || key.Equals("ARROW-UP") || key.Equals("UP-ARROW") || key.Equals("NORTH")) ? "&#x2B61;" :
                                            (key.Equals("RIGHT") || key.Equals("ARROW-RIGHT") || key.Equals("RIGHT-ARROW") || key.Equals("EAST")) ? "&#x2B62;" :
                                            (key.Equals("LEFT") || key.Equals("ARROW-LEFT") || key.Equals("LEFT-ARROW") || key.Equals("WEST")) ? "&#x2B60;" :
                                            (key.Equals("DOWN") || key.Equals("ARROW-DOWN") || key.Equals("DOWN-ARROW") || key.Equals("SOUTH")) ? "&#x2B63;" :
                                            (key.Equals("UP-LEFT") || key.Equals("UPLEFT") ||
                                                key.Equals("UP-LEFT-ARROW") || key.Equals("UPLEFT-ARROW") ||
                                                key.Equals("ARROW-UP-LEFT") || key.Equals("ARROW-UPLEFT") ||
                                                key.Equals("NORTHWEST") || key.Equals("NORTH-WEST")
                                            ) ? "&#x2B66;" :
                                            (key.Equals("UP-RIGHT") || key.Equals("UPRIGHT") ||
                                                key.Equals("UP-RIGHT-ARROW") || key.Equals("UPRIGHT-ARROW") ||
                                                key.Equals("ARROW-UP-RIGHT") || key.Equals("ARROW-UPRIGHT") ||
                                                key.Equals("NORTHEAST") || key.Equals("NORTH-EAST")
                                            ) ? "&#x2B67;" :
                                            (key.Equals("DOWN-LEFT") || key.Equals("DOWNLEFT") ||
                                                key.Equals("DOWN-LEFT-ARROW") || key.Equals("DOWNLEFT-ARROW") ||
                                                key.Equals("ARROW-DOWN-LEFT") || key.Equals("ARROW-DOWNLEFT") ||
                                                key.Equals("SOUTHWEST") || key.Equals("SOUTH-WEST")
                                            ) ? "&#x2B69;" :
                                            (key.Equals("DOWN-RIGHT") || key.Equals("DOWNRIGHT") ||
                                                key.Equals("DOWN-RIGHT-ARROW") || key.Equals("DOWNRIGHT-ARROW") ||
                                                key.Equals("ARROW-DOWN-RIGHT") || key.Equals("ARROW-DOWNRIGHT") ||
                                                key.Equals("SOUTHEAST") || key.Equals("SOUTH-EAST")
                                            ) ? "&#x2B68;" :
                                            key;
                encodedKeyboardShortcut += "</kbd>";
            }

            remarkup = remarkup.Substring(match.Length);
            html = encodedKeyboardShortcut.Substring(kbdJoin.Length);

            Length = match.Length;

            return true;
        }
    }
}
