﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using static Phabrico.Parsers.Remarkup.Rules.RemarkupRule;

namespace Phabrico.Parsers.Remarkup
{
    public class RemarkupTokenList : List<Rules.RemarkupRule>
    {
        private const string BGN = Rules.RemarkupRule.BGN;  // During XML Export the < and > characters in the XML tags will be replaced by BGN and END
        private const string END = Rules.RemarkupRule.END;  // During XML Export the < and > characters in the XML tags will be replaced by BGN and END

        private static Dictionary<Type, string> xmlTagPerRemarkupRule = null;

        private static Dictionary<Type, string> XmlTag
        {
            get
            {
                if (xmlTagPerRemarkupRule == null)
                {
                    // collect all Remarkup parsers compiled in this assembly
                    xmlTagPerRemarkupRule = Assembly.GetCallingAssembly()
                                                    .GetTypes()
                                                    .Where(type => typeof(Rules.RemarkupRule).IsAssignableFrom(type)
                                                                && type.IsAbstract == false
                                                          )
                                                    .ToDictionary(
                                                       key => key,
                                                       value => value.GetCustomAttribute<RuleXmlTag>()
                                                                     .XmlTag
                                                    );
                }

                return xmlTagPerRemarkupRule;
            }
        }

        /// <summary>
        /// Flat version of token list
        /// </summary>
        public RemarkupTokenList Flat
        {
            get
            {
                RemarkupTokenList result = new RemarkupTokenList();
                foreach (Rules.RemarkupRule token in this)
                {
                    if (token.ChildTokenList.Any())
                    {
                        foreach (Rules.RemarkupRule childToken in token.ChildTokenList.Flat)
                        {
                            result.Add(childToken);
                        }
                    }
                    else
                    {
                        result.Add(token);
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Inner function used for converting Remarkup to XML
        /// The &lt; and &gt; characters in XML tag names however are replaced by \x02 and \x03.
        /// The will be replaced again by the outer export function ToXML
        /// </summary>
        /// <param name="database">Phabrico database</param>
        /// <param name="browser">Link to browser</param>
        /// <param name="url">URL of the wiki page from where the export should start</param>
        /// <param name="phabricatorObjectToken"></param>
        /// <returns>Pseudo XML generated from the Remarkup content</returns>
        internal string PrepareForXmlExport(Storage.Database database, Http.Browser browser, string url, string phabricatorObjectToken)
        {
            string result = "";

            foreach (Rules.RemarkupRule rule in this)
            {
                string xmlTag = XmlTag[rule.GetType()];

                if (rule is Rules.RuleNewline)
                {
                    if (string.IsNullOrWhiteSpace(result))
                    {
                        continue;
                    }
                    else
                    {
                        int nbrNewlines = rule.Text.Count(ch => ch == '\n');
                        for (int copy = 0; copy < nbrNewlines; copy++)
                        {
                            result += "<N>[" + (this.IndexOf(rule) + copy) + "]</N>\n";
                        }
                    }
                }
                else
                if (rule is Rules.RuleUnknownToken)
                {
                    Rules.RuleUnknownToken unknownToken = rule as Rules.RuleUnknownToken;
                    result += unknownToken.Content.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;");
                }
                else
                if (string.IsNullOrEmpty(rule.Text.Trim('\n')))
                {
                    result += BGN + xmlTag + " /" + END + "\n";
                }
                else
                if (rule is Rules.RuleRegular)
                {
                    result += rule.Text.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;");
                }
                else
                if (rule is Rules.RuleList)
                {
                    Rules.RuleList list = rule as Rules.RuleList;

                    result += list.GenerateListXML(database, browser, url).TrimStart('\n');
                }
                else
                if (rule is Rules.RuleHyperLink)
                {
                    Rules.RuleHyperLink ruleHyperLink = rule as Rules.RuleHyperLink;
                    xmlTag = RuleXmlTag.GetXmlTag(rule.GetType());

                    if (ruleHyperLink.InvalidHyperlink)
                    {
                        result += BGN + xmlTag + " " + ruleHyperLink.Attributes + END
                                + BGN + "/" + xmlTag + END;
                    }
                    else
                    {
                        result += BGN + xmlTag + " " + ruleHyperLink.Attributes + END
                                + ruleHyperLink.Description
                                + BGN + "/" + xmlTag + END;
                    }
                }
                else
                if (rule is Rules.RuleNotification)
                {
                    Rules.RuleNotification ruleNotification = rule as Rules.RuleNotification;
                    xmlTag = RuleXmlTag.GetXmlTag(rule.GetType());

                    string notificationType = "";
                    if (ruleNotification.Style == Rules.RuleNotification.NotificationStyle.Warning) notificationType = " t=\"w\"";
                    if (ruleNotification.Style == Rules.RuleNotification.NotificationStyle.Important) notificationType = " t=\"i\"";

                    result += string.Format(BGN + "{0}{1} p=\"{2}\"" + END, xmlTag, notificationType, ruleNotification.HideNotificationPrefix ? 0 : 1);
                    result += rule.ChildTokenList.PrepareForXmlExport(database, browser, url, phabricatorObjectToken);
                    result += BGN + "/" + xmlTag + END;
                }
                else
                if (rule is Rules.RuleTable)
                {
                    bool htmlTable = rule.Text.StartsWith("<");
                    if (htmlTable)
                    {
                        // html syntax
                        result += string.Format(BGN + "{0} t=\"h\"" + END + "\n", xmlTag);

                        Match match = RegexSafe.Match(rule.Text, @"^ ?\<table\>(.*?(?<!\</table\>))\</table\>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        MatchCollection rows = RegexSafe.Matches(match.Groups[1].Value, @"\<tr\>(.+?(?<!\</tr\>))\</tr\>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        foreach (Match row in rows.OfType<Match>())
                        {
                            result += "  <tr>";
                            MatchCollection rowCells = RegexSafe.Matches(row.Value, @"(\<(td|th)[^>]*\>)(.*?(?<!\</(td|th)\>))\</(td|th)\>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                            foreach (Match cell in rowCells.OfType<Match>())
                            {
                                string tdth = cell.Groups[1].Value;
                                string cellRemarkupContent = RemarkupToXML(database, browser, url, cell.Groups[3].Value, phabricatorObjectToken);
                                result += tdth + cellRemarkupContent + tdth.Replace("<", "</");
                            }
                            result += "</tr>\n";
                        }

                        result += string.Format(BGN + "/{0}" + END + "\n", xmlTag);
                    }
                    else
                    {
                        // simple syntax
                        result += string.Format(BGN + "{0} t=\"s\"" + END + "\n", xmlTag);

                        string[] rows = rule.Text.TrimEnd('\r', '\n')
                                                 .Split('\n')
                                                 .Select(row => row.TrimEnd('\r'))
                                                 .ToArray();
                        for (int r=0; r<rows.Length; r++)
                        {
                            string row = rows[r];

                            result += "  <tr>";

                            // verify if cells contain sub-rules which contain '|' characters (e.g. hyperlinks)
                            Match[] hyperlinksInCells = RegexSafe.Matches(row, @"\[\[[^]]*\]\]", RegexOptions.Singleline).OfType<Match>().ToArray();

                            // replace content of sub-rules temporary by something else
                            Dictionary<int, string> originalContent = new Dictionary<int, string>();
                            int replaceIndex = 1;
                            foreach (Match subRuleMatch in hyperlinksInCells.OrderByDescending(m => m.Index))
                            {
                                originalContent[replaceIndex] = subRuleMatch.Value;

                                row = row.Substring(0, subRuleMatch.Index)
                                    + "\x04" + replaceIndex.ToString() + "\x04"
                                    + row.Substring(subRuleMatch.Index + subRuleMatch.Length);

                                replaceIndex++;
                            }

                            List<string> cells = row.Split('|').Skip(1).ToList();

                            // loop through cells of row
                            for (int c = 0; c < cells.Count; c++)
                            {
                                string cell = cells[c];
                                string cellRemarkupContent;
                                if (RegexSafe.IsMatch(cell, "^\\W*--+\\W$*", RegexOptions.None))
                                {
                                    // horizontal line for table column header -> do not decode
                                    cellRemarkupContent = cell;
                                }
                                else
                                {
                                    Match[] subRuleReplacements = RegexSafe.Matches(cell, "\x04([0-9]+)\x04", RegexOptions.Singleline).OfType<Match>().ToArray();
                                    foreach (Match subRuleReplacement in subRuleReplacements.OrderByDescending(m => m.Index))
                                    {
                                        cell = cell.Substring(0, subRuleReplacement.Index)
                                             + originalContent[Int32.Parse(subRuleReplacement.Groups[1].Value)]
                                             + cell.Substring(subRuleReplacement.Index + subRuleReplacement.Length);
                                    }

                                    cellRemarkupContent = RemarkupToXML(database, browser, url, cell, phabricatorObjectToken);
                                }
                                result += "<td>" + cellRemarkupContent + "</td>";
                            }
                            result += "</tr>\n";
                        }

                        result += string.Format(BGN + "/{0}" + END + "\n", xmlTag);
                    }
                }
                else
                if (rule.ChildTokenList.Any())
                {
                    result += BGN + xmlTag + (rule.Attributes == null ? "" : " " + rule.Attributes) + END;
                    result += rule.ChildTokenList.PrepareForXmlExport(database, browser, url, phabricatorObjectToken);
                    result += BGN + "/" + xmlTag + END;

                    if (result.Any() && (
                            rule is Rules.RuleFormatting
                         || rule is Rules.RuleBrackets) == false
                       )
                    {
                        result += "\n";
                    }
                }
                else
                {
                    string innerText = rule.Text;
                    if (rule is Rules.RuleMacro)
                    {
                        if (innerText.EndsWith("\n")) innerText = innerText.Substring(0, innerText.Length - 1);
                        if (innerText.EndsWith("\r")) innerText = innerText.Substring(0, innerText.Length - 1);
                    }

                    result += BGN + xmlTag + (rule.Attributes == null ? "" : " " + rule.Attributes) + END
                            + innerText.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;")
                            + BGN + "/" + xmlTag + END;

                    if (result.Any() && (
                            rule is Rules.RuleFormatting
                         || rule is Rules.RuleKey
                         || rule is Rules.RuleReferenceFile
                         || rule is Rules.RuleReferenceManiphestTask
                         || rule is Rules.RuleReferencePhameBlogPost
                         || rule is Rules.RuleReferenceProject
                         || rule is Rules.RuleReferenceUser) == false
                       )
                    {
                        result += "\n";
                    }
                }
            }

            return result.TrimEnd(' ', '\n')
                         .Replace("\r", "");
        }

        /// <summary>
        /// Converts XML formatted text to Remarkup
        /// </summary>
        /// <param name="database">Phabrico database</param>
        /// <param name="browser">Link to browser</param>
        /// <param name="url">URL of the wiki page from where the export should start</param>
        /// <param name="xml"></param>
        /// <param name="phabricatorObjectToken"></param>
        /// <returns></returns>
        public static string XMLToRemarkup(Storage.Database database, Http.Browser browser, string url, string xml, string phabricatorObjectToken)
        {
            Controllers.Remarkup remarkupController = new Controllers.Remarkup();
            remarkupController.browser = browser;
            remarkupController.EncryptionKey = database.EncryptionKey;

            RemarkupParserOutput remarkupParserOutput;
            remarkupController.ConvertRemarkupToHTML(database, url, xml, out remarkupParserOutput, false, phabricatorObjectToken);
            string result = remarkupParserOutput.TokenList.FromXML(database, browser, url, xml, true);

            return result;
        }

        /// <summary>
        /// Converts remarkup formatted text to XML
        /// </summary>
        /// <param name="database">Phabrico database</param>
        /// <param name="browser">Link to browser</param>
        /// <param name="url">URL of the wiki page from where the export should start</param>
        /// <param name="remarkup"></param>
        /// <param name="phabricatorObjectToken"></param>
        /// <returns></returns>
        public static string RemarkupToXML(Storage.Database database, Http.Browser browser, string url, string remarkup, string phabricatorObjectToken)
        {
            Controllers.Remarkup remarkupController = new Controllers.Remarkup();
            remarkupController.browser = browser;
            remarkupController.EncryptionKey = database.EncryptionKey;

            RemarkupParserOutput remarkupParserOutput;
            remarkupController.ConvertRemarkupToHTML(database, url, remarkup, out remarkupParserOutput, false, phabricatorObjectToken);
            string result = remarkupParserOutput.TokenList.ToXML(database, browser, "", phabricatorObjectToken);

            return result;
        }

        /// <summary>
        /// Converting XML to Remarkup content
        /// </summary>
        /// <param name="database">Phabrico database</param>
        /// <param name="browser">Link to browser</param>
        /// <param name="url">URL of the wiki page from where the import should start</param>
        /// <param name="xml">XML to be converted to Remarkup</param>
        /// <param name="initial">If false, the function is called from FromXML()</param>
        /// <returns>Remarkup generated from the XML content</returns>
        public string FromXML(Database database, Browser browser, string url, string xml, bool initial = true)
        {
            string result = "";
            BrokenXML.BrokenXmlParser brokenXmlParser = new BrokenXML.BrokenXmlParser();
            BrokenXML.BrokenXmlToken[] tokens = brokenXmlParser.Parse(xml).ToArray();

            for (int t = 0; t < tokens.Length; t++)
            {
                BrokenXML.BrokenXmlText text = tokens[t] as BrokenXML.BrokenXmlText;
                if (text != null)
                {
                    result += text.Value.TrimStart('\n');
                    continue;
                }

                BrokenXML.BrokenXmlOpeningTag openingTag = tokens[t] as BrokenXML.BrokenXmlOpeningTag;
                if (openingTag != null)
                {
                    Type ruleType = XmlTag.FirstOrDefault(tag => tag.Value.Equals(openingTag.Name)).Key;
                    if (ruleType == null)
                    {
                        return xml;
                    }

                    Rules.RemarkupRule rule = ruleType.GetConstructor(Type.EmptyTypes).Invoke(null) as Rules.RemarkupRule;
                    if (rule == null) continue;

                    rule.Database = database;
                    rule.Browser = browser;
                    rule.DocumentURL = url;
                    rule.TokenList = this;
                    rule.Initialize();

                    var openCloseTags = tokens.Skip(t+1)
                                              .OfType<BrokenXML.BrokenXmlClosingTag>()
                                              .Where(token => token.Name.Equals(openingTag.Name))
                                              .Select(token => new
                                              {
                                                  Token = token,
                                                  Depth = token is BrokenXML.BrokenXmlOpeningTag ? 1
                                                        : token is BrokenXML.BrokenXmlClosingTag ? -1
                                                        : 0
                                              })
                                              .ToArray();

                    BrokenXML.BrokenXmlClosingTag closingTag = null;
                    int currentDepth = 1;
                    for (int i = 0; i < openCloseTags.Length; i++)
                    {
                        currentDepth += openCloseTags[i].Depth;

                        if (currentDepth == 0)
                        {
                            closingTag = openCloseTags[i].Token;
                            t = Array.IndexOf(tokens, closingTag);
                            break;
                        }
                    }

                    if (rule is Rules.RuleNewline)
                    {
                        result += "\n";
                    }
                    else
                    if (closingTag != null)
                    {
                        string innerText = xml.Substring(openingTag.Index + openingTag.Length, 
                                                         closingTag.Index - openingTag.Index - openingTag.Length
                                                        );
                        innerText = FromXML(database, browser, url, innerText, false);

                        result += rule.ConvertXmlToRemarkup(database, browser, innerText, openingTag.Attributes.ToDictionary(key => key.Name, value => value.Value));
                    }

                    continue;
                }

                throw new System.Exception("RemarkupTokenList.FromXML: BrokenXmlParser returned invalid result");
            }

            if (initial)
            {
                result = RegexSafe.Replace(result, "<N>[[][0-9]+[]]</N>\n", "\n", RegexOptions.Singleline);
                result = System.Web.HttpUtility.HtmlDecode(result);
                result = result.Trim('\r', '\n');
            }

            return result;
        }

        /// <summary>
        /// Converting Remarkup content to XML
        /// </summary>
        /// <param name="database">Phabrico database</param>
        /// <param name="browser">Link to browser</param>
        /// <param name="url">URL of the wiki page from where the export should start</param>
        /// <param name="phabricatorObjectToken"></param>
        /// <returns>XML generated from the Remarkup content</returns>
        public string ToXML(Storage.Database database, Http.Browser browser, string url, string phabricatorObjectToken)
        {
            string result = PrepareForXmlExport(database, browser, url, phabricatorObjectToken);
            while (true)
            {
                Match newlineEnding = RegexSafe.Match(result, "<N>[[][0-9]+[]]</N>$", RegexOptions.None);
                if (newlineEnding.Success == false) break;

                result = result.Substring(0, result.Length - newlineEnding.Length);
                result = result.TrimEnd('\r', '\n');
            }

            result = result.Replace(BGN, "<")  // restore <
                           .Replace(END, ">"); // restore >
            return result;
        }
    }
}
