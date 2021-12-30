using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Remarkup parser for strike-through text
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
    [RuleXmlTag("S")]
    public class RuleFormattingStrikeThrough : RuleFormatting
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
            Match match = RegexSafe.Match(remarkup, @"^(~~([^ \r\n~].+?(?=~~(\W|$)))~~)(\W|$)", RegexOptions.Singleline);
            if (match.Success == false) return false;

            RemarkupParserOutput remarkupParserOutput;
            remarkup = remarkup.Substring(match.Groups[1].Length);
            UnformattedText = Engine.ToHTML(this, database, browser, url, match.Groups[2].Value, out remarkupParserOutput, false);
            html = string.Format("<del>{0}</del>", UnformattedText);
            LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
            ChildTokenList.AddRange(remarkupParserOutput.TokenList);

            Length = match.Length;

            return true;
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
            return "~~" + innerText + "~~";
        }
    }
}
