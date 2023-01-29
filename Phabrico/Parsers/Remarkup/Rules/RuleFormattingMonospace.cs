using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Remarkup parser for monospaced text
    /// </summary>
    [RuleXmlTag("M")]
    public class RuleFormattingMonospace : RuleFormatting
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
            Match matchSquare = RegexSafe.Match(remarkup, @"^##((.+?(?<![^\n]##))|#+)([^\n])##", RegexOptions.Singleline);
            if (matchSquare.Success)
            {
                remarkup = remarkup.Substring(matchSquare.Length);
                UnformattedText = HttpUtility.HtmlEncode(matchSquare.Groups[1].Value) + HttpUtility.HtmlEncode(matchSquare.Groups[3].Value);
                html = string.Format("<tt class='remarkup-monospaced'>{0}</tt>", UnformattedText);

                Length = matchSquare.Length;

                return true;
            }

            if (RuleStartAfterWhiteSpace || RuleStartAfterPunctuation)
            {
                Match matchBackTick = RegexSafe.Match(remarkup, @"^`([^`\r\n]+)`", RegexOptions.Singleline);
                if (matchBackTick.Success)
                {
                    remarkup = remarkup.Substring(matchBackTick.Length);
                    UnformattedText = HttpUtility.HtmlEncode(matchBackTick.Groups[1].Value);
                    html = string.Format("<tt class='remarkup-monospaced'>{0}</tt>", UnformattedText);

                    Length = matchBackTick.Length;

                    return true;
                }
            }

            if (RuleStartOnNewLine)
            {
                Match matchBackTick = RegexSafe.Match(remarkup, @"^`([^`\r\n]+)`", RegexOptions.Singleline);
                if (matchBackTick.Success)
                {
                    remarkup = remarkup.Substring(matchBackTick.Length);
                    UnformattedText = HttpUtility.HtmlEncode(matchBackTick.Groups[1].Value);
                    html = string.Format("<tt class='remarkup-monospaced'>{0}</tt>", UnformattedText);

                    Length = matchBackTick.Length;

                    return true;
                }
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
            return innerText;
        }
    }
}
