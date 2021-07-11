using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;

using Phabrico.Http;
using Phabrico.Parsers.Remarkup.Rules;
using Phabrico.Plugin;

namespace Phabrico.Parsers.Remarkup
{
    /// <summary>
    /// Represents the engine that translates the Remarkup content
    /// </summary>
    public class RemarkupEngine
    {
        private static List<RemarkupRule> remarkupRules = null;

        /// <summary>
        /// Initializes a new instance of RemarkupEngine
        /// </summary>
        public RemarkupEngine()
        {
            if (remarkupRules == null)
            {
                remarkupRules = new List<RemarkupRule>();

                // collect all Remarkup parsers compiled in this assembly
                Type[] availableRuleClassTypes = Assembly.GetCallingAssembly()
                                                         .GetTypes()
                                                         .Where(type => typeof(RemarkupRule).IsAssignableFrom(type))
                                                         .OrderBy(type => type.CustomAttributes.Any(attr => attr.AttributeType == typeof(RemarkupRule.RulePriority)) == false ? RemarkupRule.RulePriority.DefaultPriority :
                                                                          type.GetCustomAttribute<RemarkupRule.RulePriority>().Priority
                                                                 )
                                                         .ToArray();
                foreach (Type remarkupRuleType in availableRuleClassTypes)
                {
                    if (remarkupRuleType.IsAbstract) continue;

                    RemarkupRule newRemarkupRule = Activator.CreateInstance(remarkupRuleType) as RemarkupRule;
                    newRemarkupRule.Engine = this;
                    remarkupRules.Add(newRemarkupRule);

                    newRemarkupRule.Initialize();
                }
            }
        }

        /// <summary>
        /// Converts Remarkup encoded text into HTML
        /// </summary>
        /// <param name="currentRemarkupRule">The outer Remarkup rule from where this method is called. Not all Remarkup rules can be executed from anywhere (e.g. a Table cannot be defined in another Table). Can be null if at start of decoding.</param>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="browser">Link to browser</param>
        /// <param name="url">URL in browser from where this method was called</param>
        /// <param name="remarkupText">Remarkup encoded text</param>
        /// <param name="remarkupParserOutput">Additional information generated from the specified remarkup (e.g. linked files)</param>
        /// <param name="includeLineNumbers">If true, empty SPAN elements with a line data-attribute will be added to generated HTML code. This line data-attribute contains the (first) line number of the original Remarkup code for all the generated elements</param>
        /// <returns>The decoded HTML</returns>
        public string ToHTML(RemarkupRule currentRemarkupRule, Storage.Database database, Browser browser, string url, string remarkupText, out RemarkupParserOutput remarkupParserOutput, bool includeLineNumbers)
        {
            lock (remarkupRules)
            {
                string html = "";
                string unprocessedRemarkupText = remarkupText;
                bool ruleStartsOnNewLine = true;
                bool ruleStartsAfterWhiteSpace = true;
                int previousLineNumber = 0;

                remarkupParserOutput = new RemarkupParserOutput();

                List<Type> disallowedParentRules;
                if (currentRemarkupRule == null)
                {
                    disallowedParentRules = new List<Type>();
                }
                else
                {
                    disallowedParentRules = currentRemarkupRule.GetType()
                                                               .GetCustomAttributes(true)
                                                               .OfType<RemarkupRule.RuleNotInnerRuleFor>()
                                                               .Select(attr => attr.ParentRuleType)
                                                               .ToList();
                }

                while (unprocessedRemarkupText.Any())
                {
                    string localHtml = "";
                    bool success = false;
                    bool addNewLine = true;
                    foreach (RemarkupRule remarkupRule in remarkupRules)
                    {
                        if (disallowedParentRules.Contains(remarkupRule.GetType()))
                        {
                            continue;
                        }
                        else
                        {
                            string processedRemarkupText = unprocessedRemarkupText;
                            remarkupRule.RuleStartOnNewLine = ruleStartsOnNewLine;
                            remarkupRule.RuleStartAfterWhiteSpace = ruleStartsAfterWhiteSpace;
                            remarkupRule.LinkedPhabricatorObjects.Clear();
                            success = remarkupRule.ToHTML(database, browser, url, ref unprocessedRemarkupText, out localHtml);
                            if (success)
                            {
                                remarkupRule.Start = remarkupText.IndexOf(processedRemarkupText);
                                remarkupRule.Text = processedRemarkupText.Substring(0, remarkupRule.Length);

                                RemarkupRule clonedRemarkupRule = remarkupRule.Clone();
                                remarkupParserOutput.TokenList.Add(clonedRemarkupRule);
                                remarkupParserOutput.LinkedPhabricatorObjects.AddRange(clonedRemarkupRule.LinkedPhabricatorObjects);

                                if (processedRemarkupText.Substring(0, unprocessedRemarkupText.Length).LastOrDefault() == ' ')
                                {
                                    ruleStartsAfterWhiteSpace = true;
                                }
                                else
                                {
                                    ruleStartsAfterWhiteSpace = false;
                                }

                                ruleStartsOnNewLine = remarkupRule is RuleHorizontalRule ||
                                                      remarkupRule is RuleNewline ||
                                                      remarkupRule is RuleHeader ||
                                                      remarkupRule is RuleLiteral ||
                                                      remarkupRule is RuleList ||
                                                      remarkupRule is RuleCodeBlock;

                                addNewLine = (remarkupRule is RuleRegular) == false &&
                                             (remarkupRule is RuleHyperLink) == false &&
                                             (remarkupRule is RuleIcon) == false &&
                                             (remarkupRule is RuleBrackets) == false &&
                                             (remarkupRule is RuleFormattingBold) == false &&
                                             (remarkupRule is RuleFormattingHighlight) == false &&
                                             (remarkupRule is RuleFormattingItalic) == false &&
                                             (remarkupRule is RuleFormattingMonospace) == false &&
                                             (remarkupRule is RuleFormattingStrikeThrough) == false &&
                                             (remarkupRule is RuleFormattingUnderline) == false;

                                if (includeLineNumbers)
                                {
                                    int lineNumber = remarkupText.Substring(0, remarkupText.IndexOf(processedRemarkupText))
                                                                 .Count(ch => ch == '\n');
                                    if (lineNumber != previousLineNumber)
                                    {
                                        html += string.Format("<span data-line='{0}'></span>", lineNumber);
                                        previousLineNumber = lineNumber;
                                    }
                                }

                                break;
                            }
                        }
                    }

                    if (success)
                    {
                        html += localHtml;
                        if (addNewLine)
                        {
                            html += "\n";
                        }
                    }
                    else
                    {
                        if (unprocessedRemarkupText.Length > 0)
                        {
                            byte[] utf16 = System.Text.UnicodeEncoding.Unicode.GetBytes( unprocessedRemarkupText );

                            if (unprocessedRemarkupText.Length == 1 || utf16[1] == 0x00)
                            {
                                html += HttpUtility.HtmlEncode(unprocessedRemarkupText[0]);
                                ruleStartsAfterWhiteSpace = unprocessedRemarkupText[0] == ' ';
                            }
                            else
                            {
                                html += (char)BitConverter.ToUInt16(utf16, 0);
                                if (utf16.Length >= 4 && utf16[3] != 0x00)
                                {
                                    html += (char)BitConverter.ToUInt16(utf16, 2);
                                    unprocessedRemarkupText = unprocessedRemarkupText.Substring(1);
                                }
                            }
                            
                            unprocessedRemarkupText = unprocessedRemarkupText.Substring(1);
                        }

                        ruleStartsOnNewLine = false;
                    }
                }

                if (currentRemarkupRule == null)
                {
                    // reinitialize all remarkupRules again
                    foreach (RemarkupRule remarkupRule in remarkupRules)
                    {
                        remarkupRule.Initialize();
                    }
                }

                remarkupParserOutput.LinkedPhabricatorObjects = remarkupParserOutput.LinkedPhabricatorObjects
                                                                                    .Where(o => o != null)
                                                                                    .GroupBy(o => o.Token)
                                                                                    .Select(g => g.First())
                                                                                    .ToList();

                return html.Trim(' ', '\r', '\n');
            }
        }
    }
}
