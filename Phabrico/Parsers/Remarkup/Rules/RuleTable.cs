using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Phabricator.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Remarkup parser for Tables
    /// </summary>
    [RuleNotInnerRuleFor(typeof(RuleCodeBlockBy2WhiteSpaces))]
    [RuleNotInnerRuleFor(typeof(RuleCodeBlockBy3BackTicks))]
    [RuleNotInnerRuleFor(typeof(RuleHeader))]
    [RuleNotInnerRuleFor(typeof(RuleHorizontalRule))]
    [RuleNotInnerRuleFor(typeof(RuleInterpreter))]
    [RuleNotInnerRuleFor(typeof(RuleList))]
    [RuleNotInnerRuleFor(typeof(RuleLiteral))]
    [RuleNotInnerRuleFor(typeof(RuleNewline))]
    [RuleNotInnerRuleFor(typeof(RuleNotification))]
    [RuleNotInnerRuleFor(typeof(RuleQuote))]
    [RuleNotInnerRuleFor(typeof(RuleTable))]
    public class RuleTable : RemarkupRule
    {
        /// <summary>
        /// Converts Remarkup encoded table into a HTML table
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
            Match match = RegexSafe.Match(remarkup, @"^( ?\|([^\n]*\n))( *\|([^\n]*\n))*", RegexOptions.Singleline);
            if (match.Success)
            {
                // content is pure Remarkup table
                //    E.g.
                //    | header 1 | header 2
                //    | -------- | --------
                //    | data 1   | data2
                //
                // Start converting it to a HTML table

                string tableContent = "<table>";
                string lastLine = null;
                foreach (string line in match.Value.Split('\n').Select(m => m.Trim('\r')))
                {
                    if (string.IsNullOrEmpty(line)) continue;

                    string lineContent = line;
                    if (lineContent.EndsWith("|"))
                    {
                        // remove last '|' character because it's completely useless
                        lineContent = lineContent.Substring(0, lineContent.Length - 1);

                        // check if we still have some '|' characters
                        if (lineContent.Contains('|') == false)
                        {
                            // no '|' characters found...
                            return false;
                        }
                    }

                    // search for hyperlinks in table cells and replace them temporarilly
                    Dictionary<string, string> hyperlinks = new Dictionary<string, string>();
                    foreach (Match hyperlink in RegexSafe.Matches(lineContent, @"\[\[(.+?(?=\]\]))\]\]", RegexOptions.Singleline).OfType<Match>().OrderByDescending(m => m.Index).ToList())
                    {
                        string key = string.Format("\x02_hyperlink_{0}_\x03", hyperlinks.Count);
                        hyperlinks[key] = hyperlink.Value;

                        lineContent = lineContent.Substring(0, hyperlink.Index) +
                                      key +
                                      lineContent.Substring(hyperlink.Index + hyperlink.Length);
                    }

                    if (string.IsNullOrEmpty(lineContent)) break;

                    lineContent = lineContent.Replace("|", "</td><td>");
                    lineContent = lineContent.Substring("</td>".Length);
                    lineContent = lineContent + "</td>";
                    lineContent = "<tr>" + lineContent + "</tr>\n";

                    // restore hyperlinks in table cells again (if any)
                    foreach (KeyValuePair<string, string> hyperlink in hyperlinks)
                    {
                        lineContent = lineContent.Replace(hyperlink.Key, hyperlink.Value);
                    }

                    // in case we have a cell of dashes, we need to convert the cell on the previous row to a header cell
                    lineContent = RegexSafe.Replace(lineContent, "<td> *--+ *</td>", "<th>---</th>");
                    if (lastLine != null && lineContent.Contains("<th>---</th>"))
                    {
                        string[] cellTypeInfo = Regex.Matches(lineContent, "(<t[dh]>)[^<]*</t[dh]>").OfType<Match>().Select(m => m.Groups[1].Value).ToArray();

                        tableContent = tableContent.Substring(0, tableContent.Length - lastLine.Length);

                        lineContent = "<tr>";
                        Match[] cellInfo = Regex.Matches(lastLine, "(<td[^>]*>)([^<]*)</td>").OfType<Match>().ToArray();
                        for (int c=0; c < cellInfo.Length; c++)
                        {
                            if (c < cellTypeInfo.Length && cellTypeInfo[c].Equals("<th>"))
                            {
                                lineContent += cellInfo[c].Groups[1].Value.Replace("<td", "<th") + cellInfo[c].Groups[2].Value.Trim() + "</th>";
                            }
                            else
                            {
                                lineContent += cellInfo[c].Groups[1].Value + cellInfo[c].Groups[2].Value.Trim() + "</td>";
                            }
                        }
                        lineContent += "</tr>\n";
                    }

                    tableContent += lineContent;

                    lastLine = lineContent;
                }

                tableContent += "</table>";

                if (tableContent.Equals("<table></table>"))
                {
                    return false;
                }
                else
                {
                    remarkup = remarkup.Substring(match.Length);

                    // process HTML further
                    if (ProcessHtmlTable(browser, database, url, ref tableContent, out html))
                    {
                        Length = match.Length;
                        return true;
                    }

                    return false;
                }
            }
            else
            {
                // If content is a HTML table, process it further
                return ProcessHtmlTable(browser, database, url, ref remarkup, out html);
            }
        }

        /// <summary>
        /// Do some post-processing on the table data (e.g. formatting of table cells, ...)
        /// </summary>
        /// <param name="browser">Reference to browser</param>
        /// <param name="database">Reference to database</param>
        /// <param name="url">URL from where the content origins from</param>
        /// <param name="remarkup">Content to be validated</param>
        /// <param name="html">Generated HTML (if success)</param>
        /// <returns>True if content was HTML table</returns>
        private bool ProcessHtmlTable(Browser browser, Storage.Database database, string url, ref string remarkup, out string html)
        {
            html = "";

            Match match = RegexSafe.Match(remarkup, @"^ ?\<table\>(.*?(?<!\</table\>))\</table\>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (match.Success == false) return false;

            string tableContent;
            MatchCollection rows = RegexSafe.Matches(match.Groups[1].Value, @"\<tr\>(.+?(?<!\</tr\>))\</tr\>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (rows.Count == 0)
            {
                tableContent = HttpUtility.HtmlEncode(match.Value);
            }
            else
            {
                Storage.Account accountStorage = new Storage.Account();
                Account existingAccount = accountStorage.WhoAmI(database, browser);

                string[] concealedHeaders = existingAccount.Parameters.ColumnHeadersToHide?.Select(columnHeaderToHide => columnHeaderToHide.ToLower()).ToArray() ?? new string[0];
                List<int> concealedColumnIndices = new List<int>();
                List<int> concealedHeaderIndices = new List<int>();

                tableContent = "<table class='remarkup-table'>\n";
                foreach (Match row in rows.OfType<Match>())
                {
                    List<Match> tableRowData = RegexSafe.Matches(row.Value, "<(th)>([^<]*)</th>|<(td)>([^<]*)</td>", RegexOptions.Singleline).OfType<Match>().ToList();
                    bool firstCellIsHeader = false;
                    bool allCellsAreHeaders = false;

                    if (tableRowData.Any())
                    {
                        firstCellIsHeader = tableRowData[0].Groups[1].Value.Equals("th");
                        allCellsAreHeaders = tableRowData.All(cellData => cellData.Groups[1].Value.Equals("th"));

                        if (allCellsAreHeaders)
                        {
                            concealedColumnIndices.AddRange(tableRowData.Where(headerData => concealedHeaders.Contains(headerData.Groups[2].Value.Trim().ToLower()))
                                                                        .Select(headerData => tableRowData.IndexOf(headerData))
                                                                        .ToArray());

                            concealedHeaderIndices.AddRange(concealedColumnIndices);
                        }
                        else
                        if (firstCellIsHeader && concealedHeaders.Contains(tableRowData[0].Groups[2].Value.Trim().ToLower()))
                        {
                            concealedHeaderIndices.AddRange(tableRowData.Where(cellData => cellData.Groups[3].Value.Equals("td"))
                                                                        .Select(cellData => tableRowData.IndexOf(cellData))
                                                                        .ToArray());
                        }
                    }

                    tableContent += "  <tr>\n";

                    int cellIndex = 0;
                    MatchCollection rowCells = RegexSafe.Matches(row.Value, @"(\<(td|th)[^>]*\>)(.*?(?<!\</(td|th)\>))\</(td|th)\>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    RemarkupParserOutput remarkupParserOutput;
                    foreach (Match cell in rowCells.OfType<Match>())
                    {
                        string cellType = HttpUtility.HtmlEncode(cell.Groups[2].Value);
                        string cellValue = Engine.ToHTML(this, database, browser, url, cell.Groups[3].Value.Trim(' ', '\r'), out remarkupParserOutput, false);
                        string cellConcealed = cellType.Equals("td") && concealedHeaderIndices.Contains(cellIndex) ? " class='concealed'" : "";

                        LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                        ChildTokenList.AddRange(remarkupParserOutput.TokenList);

                        tableContent += string.Format("    <{0}{1}>{2}</{0}>\n", cellType, cellConcealed, cellValue.Replace("\r", "").Replace("\n", "<br>"));
                        cellIndex++;
                    }

                    tableContent += "  </tr>\n";

                    concealedHeaderIndices.Clear();
                    concealedHeaderIndices.AddRange(concealedColumnIndices);
                }

                tableContent += "</table>\n";


                Match regexTableContentWithFirstRowAsHeaders = RegexSafe.Match(tableContent, "(<table[^>]*>)([^<]*<tr>([^<]*<th>[^<]*</th>)*[^<]*</tr>)[^<]+((<tr>.+?</tr>[^<]*)*)", RegexOptions.Singleline);
                if (regexTableContentWithFirstRowAsHeaders.Success)
                {
                    tableContent = regexTableContentWithFirstRowAsHeaders.Groups[1].Value
                                 + "\n<thead>" + regexTableContentWithFirstRowAsHeaders.Groups[2].Value + "\n</thead>"
                                 + "\n<tbody>\n  " + regexTableContentWithFirstRowAsHeaders.Groups[4].Value + "</tbody>\n"
                                 + "</table>\n";
                }

                remarkup = remarkup.Substring(match.Length);

                html = tableContent;

                Length = match.Length;

                return true;
            }

            return false;
        }
    }
}
