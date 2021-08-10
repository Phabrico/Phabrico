using Phabrico.Http;
using Phabrico.Miscellaneous;
using System.Text.RegularExpressions;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Remarkup parser for content between round brackets.
    /// It will parse its inner content further and adds the brackets afterwards
    /// </summary>
    [RuleNotInnerRuleFor(typeof(RuleCodeBlock))]
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
    public class RuleBrackets : RemarkupRule
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
            Match match = RegexSafe.Match(remarkup,
                                          @"^\(                       # opening (
                                               (                      # begin of content
                                                   (?>                # now match...
                                                      [^()]+          # any characters except braces
                                                   |                  # or
                                                      \(  (?<DEPTH>)  # a {, increasing the depth counter
                                                   |                  # or
                                                      \)  (?<-DEPTH>) # a }, decreasing the depth counter
                                                   )*                 # any number of times
                                                   (?(DEPTH)(?!))     # until the depth counter is zero again
                                               )                      # end of content
                                             \)                       # then match the closing )",
                                          RegexOptions.IgnorePatternWhitespace);
            if (match.Success == false) return false;
            
            if (RegexSafe.IsMatch(remarkup, @"^\((IMPORTANT|NOTE|WARNING)\)(\r?\n)?(.+?(?=\n\r?\n|$))", RegexOptions.Singleline)) return false;  // notification syntax

            Length = match.Length;

            remarkup = remarkup.Substring(match.Length);

            RemarkupParserOutput remarkupParserOutput;
            html = string.Format("({0})", Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false));
            LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);

            return true;
        }
    }
}
