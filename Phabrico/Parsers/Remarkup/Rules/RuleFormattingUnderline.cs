using System.Text.RegularExpressions;

using Phabrico.Http;
using Phabrico.Miscellaneous;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Remarkup parser for underlined text
    /// </summary>
    [RuleNotInnerRuleFor(typeof(RuleCodeBlockBy2WhiteSpaces))]
    [RuleNotInnerRuleFor(typeof(RuleCodeBlockBy3BackTicks))]
    [RuleNotInnerRuleFor(typeof(RuleHeader))]
    [RuleNotInnerRuleFor(typeof(RuleHorizontalRule))]
    [RuleNotInnerRuleFor(typeof(RuleInterpreter))]
    [RuleNotInnerRuleFor(typeof(RuleList))]
    [RuleNotInnerRuleFor(typeof(RuleLiteral))]
    [RuleNotInnerRuleFor(typeof(RuleNotification))]
    [RuleNotInnerRuleFor(typeof(RuleQuote))]
    [RuleNotInnerRuleFor(typeof(RuleTable))]
    public class RuleFormattingUnderline : RemarkupRule
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
            if (RuleStartAfterWhiteSpace || RuleStartOnNewLine)
            {
                Match matchEscapedUnderline = RegexSafe.Match(remarkup, @"^/__([^\r\n].+?(?<!__))?__", RegexOptions.Singleline);
                if (matchEscapedUnderline.Success)
                {
                    // underline code is escaped by starting slash
                    return false;
                }

                Match match = RegexSafe.Match(remarkup, @"^__([^\r\n].+?(?<!__))?__", RegexOptions.Singleline);
                if (match.Success)
                {
                    if (remarkup.Substring(match.Length).StartsWith("/"))
                    {
                        // underline code is escaped by trailing slash
                        return false;
                    }
                    else
                    {
                        RemarkupParserOutput remarkupParserOutput;
                        remarkup = remarkup.Substring(match.Length);
                        html = string.Format("<u>{0}</u>", Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false));
                        LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);

                        Length = match.Length;

                        return true;
                    }
                }
            }

            return false;
        }
    }
}
