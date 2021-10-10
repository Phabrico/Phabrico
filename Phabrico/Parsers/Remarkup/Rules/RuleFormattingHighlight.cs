using Phabrico.Http;
using Phabrico.Miscellaneous;
using System.Linq;
using System.Text.RegularExpressions;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Remarkup parser for highlighted text
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
    public class RuleFormattingHighlight : RemarkupRule
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
            Match match = RegexSafe.Match(remarkup, @"^!!([^\n]*?!*)!!", RegexOptions.Singleline);
            if (match.Success == false) return false;
            if (match.Value.All(ch => ch == '!')) return false;

            RemarkupParserOutput remarkupParserOutput;
            remarkup = remarkup.Substring(match.Length);
            html = string.Format("<span class='remarkup-highlight'>{0}</span>", Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false));
            LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
            ChildTokenList.AddRange(remarkupParserOutput.TokenList);

            Length = match.Length;

            return true;
        }
    }
}
