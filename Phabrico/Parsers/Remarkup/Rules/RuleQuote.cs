using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Remarkup parser for quoted text
    /// </summary>
    [RuleXmlTag("Q")]
    public class RuleQuote : RemarkupRule
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
                Match match = RegexSafe.Match(remarkup, @"^>(>+!)? ?([^\n]*\n)(> ?([^\n]*\n))*", RegexOptions.Singleline);
                if (match.Success == false) return false;

                List<string> quotedLines = RegexSafe.Matches(match.Value, @"^> ?(.*)", RegexOptions.Multiline)
                                                .OfType<Match>()
                                                .Select(m => m.Groups[1].Value)
                                                .ToList();
                string header = "";
                if (match.Groups[1].Value.Equals(">!"))
                {
                    header = match.Groups[2].Value.Trim(' ', '\r', '\n');
                    quotedLines.RemoveAt(0);
                }

                string quotedContent = string.Join("\n", quotedLines);
                quotedContent += "\n";

                remarkup = remarkup.Substring(match.Length);

                RemarkupParserOutput remarkupParserOutput;
                html = Engine.ToHTML(this, database, browser, url, quotedContent, out remarkupParserOutput, false, PhabricatorObjectToken);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                ChildTokenList.Add(new RuleRegular() { Text = "> " });  // add 1st '>'
                ChildTokenList.AddRange(remarkupParserOutput.TokenList);

                // add '>' for each new line (except for the last one)
                for (int r = 0; r < ChildTokenList.Count - 1; r++)
                {
                    if (ChildTokenList[r] is RuleNewline)
                    {
                        ChildTokenList.Insert(r + 1, new RuleRegular() { Text = "> " });
                    }
                }

                if (ChildTokenList.LastOrDefault().Text.EndsWith("\n"))
                {
                    string lastTextPart = ChildTokenList.LastOrDefault().Text;
                    ChildTokenList.LastOrDefault().Text = lastTextPart.Substring(0, lastTextPart.Length - 1);
                }
                if (ChildTokenList.LastOrDefault().Text.EndsWith("\r"))
                {
                    string lastTextPart = ChildTokenList.LastOrDefault().Text;
                    ChildTokenList.LastOrDefault().Text = lastTextPart.Substring(0, lastTextPart.Length - 1);
                }

                while (html.StartsWith("<br>\n"))
                {
                    html = html.Substring("<br>\n".Length);
                }

                if (html.EndsWith("<br>"))
                {
                    html = "<p>" + html.Substring(0, html.Length - "<br>".Length) + "</p>";
                }

                if (string.IsNullOrEmpty(header))
                {
                    html = string.Format("<blockquote>{0}</blockquote>", html);
                }
                else
                {
                    html = string.Format("<blockquote>\n<div class='remarkup-reply-head'>{0}</div>\n<div class='remarkup-reply-body'>{1}</div>\n</blockquote>", header, html);
                }

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
            return innerText + "\n";
        }
    }
}
