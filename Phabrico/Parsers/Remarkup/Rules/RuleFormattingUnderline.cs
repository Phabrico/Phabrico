using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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
    [RuleXmlTag("U")]
    public class RuleFormattingUnderline : RuleFormatting
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
                    if (match.Value.Split('\n')
                                   .Skip(1)
                                   .Any(line => string.IsNullOrWhiteSpace(line)     // no empty line
                                             || line.StartsWith("=")                // no line should start with a '=' character
                                             || line.StartsWith("*")                // no line should start with a '*' character
                                             || line.StartsWith("%%%")              // no line should start with "%%%"
                                       ))
                    {
                        return false;
                    }

                    if (remarkup.Substring(match.Length).StartsWith("/"))
                    {
                        // underline code is escaped by trailing slash
                        return false;
                    }
                    else
                    {
                        RemarkupParserOutput remarkupParserOutput;
                        remarkup = remarkup.Substring(match.Length);
                        UnformattedText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                        html = string.Format("<u>{0}</u>", UnformattedText);
                        LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                        ChildTokenList.AddRange(remarkupParserOutput.TokenList);

                        Length = match.Length;

                        return true;
                    }
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
            return "__" + innerText + "__";
        }
    }
}
