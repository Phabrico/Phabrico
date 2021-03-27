using System.Text.RegularExpressions;
using System.Web;

using Phabrico.Http;
using Phabrico.Miscellaneous;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Remarkup parser for monospaced text
    /// </summary>
    public class RuleFormattingMonospace : RemarkupRule
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
            Match matchSquare = RegexSafe.Match(remarkup, @"^##((.+?(?<!##))|#+)##", RegexOptions.Singleline);
            if (matchSquare.Success)
            {
                remarkup = remarkup.Substring(matchSquare.Length);
                html = string.Format("<tt class='remarkup-monospaced'>{0}</tt>", HttpUtility.HtmlEncode(matchSquare.Groups[1].Value));

                Length = matchSquare.Length;

                return true;
            }

            Match matchBackTick = RegexSafe.Match(remarkup, @"^(\W)`([^`\r\n]+)`", RegexOptions.Singleline);
            if (matchBackTick.Success)
            {
                remarkup = remarkup.Substring(matchBackTick.Length);
                html = string.Format("{0}<tt class='remarkup-monospaced'>{1}</tt>", matchBackTick.Groups[1].Value, HttpUtility.HtmlEncode(matchBackTick.Groups[2].Value));

                Length = matchBackTick.Length;

                return true;
            }

            if (RuleStartOnNewLine)
            {
                matchBackTick = RegexSafe.Match(remarkup, @"^`([^`\r\n]+)`", RegexOptions.Singleline);
                if (matchBackTick.Success)
                {
                    remarkup = remarkup.Substring(matchBackTick.Length);
                    html = string.Format("<tt class='remarkup-monospaced'>{0}</tt>", HttpUtility.HtmlEncode(matchBackTick.Groups[1].Value));

                    Length = matchBackTick.Length;

                    return true;
                }
            }

            return false;
        }
    }
}
