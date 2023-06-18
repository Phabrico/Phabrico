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
    [RuleXmlTag("TB")]
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

            if (RuleStartOnNewLine)
            {
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

                    List<List<int>> tokenPositions = new List<List<int>>();
                    string tableContent = "<table>";
                    string lastLine = null;
                    foreach (Match matchRow in RegexSafe.Matches(match.Value, "[^\n]*\n"))
                    {
                        string line = matchRow.Value.Trim('\r', '\n');

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
                        string lineContentForTokenPositions = lineContent;
                        Dictionary<string, string> hyperlinks = new Dictionary<string, string>();
                        foreach (Match hyperlink in RegexSafe.Matches(lineContent, @"\[\[(.+?(?=\]\]))\]\]", RegexOptions.Singleline).OfType<Match>().OrderByDescending(m => m.Index).ToList())
                        {
                            string key = string.Format("\x02_hyperlink_{0}_\x03", hyperlinks.Count);
                            hyperlinks[key] = hyperlink.Value;

                            lineContent = lineContent.Substring(0, hyperlink.Index) +
                                          key +
                                          lineContent.Substring(hyperlink.Index + hyperlink.Length);

                            lineContentForTokenPositions = lineContentForTokenPositions.Substring(0, hyperlink.Index) +
                                                           new string('?', hyperlink.Length) +
                                                           lineContentForTokenPositions.Substring(hyperlink.Index + hyperlink.Length);
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
                            for (int c = 0; c < cellInfo.Length; c++)
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
                        else
                        {
                            List<int> tokenPositionsRow = new List<int>();
                            tokenPositionsRow.AddRange(RegexSafe.Matches(lineContentForTokenPositions, "\\|[ \t]*")
                                                                .OfType<Match>()
                                                                .Select(m => matchRow.Index + m.Index + m.Length)
                                                      );
                            tokenPositions.Add(tokenPositionsRow);
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
                        if (ProcessHtmlTable(browser, database, url, ref tableContent, out html, tokenPositions))
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
                    return ProcessHtmlTable(browser, database, url, ref remarkup, out html, null);
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
        internal override string ConvertXmlToRemarkup(Storage.Database database, Browser browser, string innerText, Dictionary<string, string> attributes)
        {
            bool isHtmlTable = attributes["t"][0] == 'h';
            if (isHtmlTable)
            {
                string result = "<table>\n";
                foreach (Match row in RegexSafe.Matches(innerText, @"\<tr\>(.+?(?<!\</tr\>))\</tr\>", RegexOptions.Singleline).OfType<Match>())
                {
                    result += "    <tr>\n";
                    foreach (Match cell in RegexSafe.Matches(row.Value, @"\<(t[dh])\>(.+?(?<!\</t[dh]\>))\</t[dh]\>", RegexOptions.Singleline).OfType<Match>())
                    {
                        string cellTag = cell.Groups[1].Value;
                        string cellContent = RemarkupTokenList.XMLToRemarkup(Database, Browser, DocumentURL, cell.Groups[2].Value, PhabricatorObjectToken);

                        result += string.Format("        <{0}>{1}</{0}>\n", cellTag, cellContent);
                    }

                    result += "    </tr>\n";
                }

                result += "</table>";

                return result;
            }
            else
            {
                string result = "";
                foreach (Match row in RegexSafe.Matches(innerText, @"\<tr\>(.+?(?<!\</tr\>))\</tr\>", RegexOptions.Singleline).OfType<Match>())
                {
                    foreach (Match cell in RegexSafe.Matches(row.Value, @"\<td\>(.+?(?<!\</td\>))\</td\>", RegexOptions.Singleline).OfType<Match>())
                    {
                        string cellContent = RemarkupTokenList.XMLToRemarkup(Database, Browser, DocumentURL, cell.Groups[1].Value, PhabricatorObjectToken);

                        result += "|" + cellContent;
                    }

                    result += "\n";
                }

                return result;
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
        /// <param name="tokenPositions">Start position of each cell token per row (if null, ProcessHtmlTable will generate it itself)</param>
        /// <returns>True if content was HTML table</returns>
        private bool ProcessHtmlTable(Browser browser, Storage.Database database, string url, ref string remarkup, out string html, List<List<int>> tokenPositions)
        {
            html = "";

            Match match = RegexSafe.Match(remarkup, @"^ ?\<table\>(.*?(?<!\</table\>))\</table\>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (match.Success == false) return false;

            MatchCollection rows = RegexSafe.Matches(match.Groups[1].Value, @"\<tr\>(.+?(?<!\</tr\>))\</tr\>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (rows.Count > 0)
            {
                Storage.Account accountStorage = new Storage.Account();
                Account existingAccount = accountStorage.WhoAmI(database, browser);

                string[] concealedHeaders = existingAccount?.Parameters?.ColumnHeadersToHide?.Select(columnHeaderToHide => columnHeaderToHide.ToLower()).ToArray() ?? new string[0];
                List<int> concealedColumnIndices = new List<int>();
                List<int> concealedHeaderIndices = new List<int>();

                bool generateTokenPositions = (tokenPositions == null);
                if (generateTokenPositions)
                {
                    tokenPositions = new List<List<int>>();
                }

                int rowIndex = -1;
                string tableContent = "<table class='remarkup-table'>\n";
                foreach (Match row in rows.OfType<Match>())
                {
                    List<Match> tableRowData = RegexSafe.Matches(row.Value, "<(th)>([^<]*)</th>|<(td)>([^<]*)</td>", RegexOptions.Singleline).OfType<Match>().ToList();
                    bool firstCellIsHeader = false;
                    bool allCellsAreHeaders = false;

                    rowIndex++;
                    if (generateTokenPositions)
                    {
                        tokenPositions.Add(new List<int>());
                    }

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
                        else
                        if (tableRowData.Count == 2 && concealedHeaders.Contains(tableRowData[0].Groups[4].Value.Trim().ToLower()))
                        {
                            // simple table with 2 columns and first column contains confidential header => second column contains confidential value
                            concealedHeaderIndices.AddRange(new int[] { 1 });
                        }
                    }

                    tableContent += "  <tr>\n";

                    int cellIndex = 0;
                    MatchCollection rowCells = RegexSafe.Matches(row.Value, @"(\<(td|th)[^>]*\>)(.*?(?<!\</(td|th)\>))\</(td|th)\>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    RemarkupParserOutput remarkupParserOutput;
                    foreach (Match cell in rowCells.OfType<Match>())
                    {
                        string cellType = HttpUtility.HtmlEncode(cell.Groups[2].Value);
                        string cellValue = Engine.ToHTML(this, database, browser, url, cell.Groups[3].Value.Trim(' ', '\r'), out remarkupParserOutput, false, PhabricatorObjectToken);
                        string cellConcealed = cellType.Equals("td") && concealedHeaderIndices.Contains(cellIndex) ? " class='concealed'" : "";

                        if (generateTokenPositions)
                        {
                            tokenPositions[rowIndex].Add(match.Index + match.Groups[1].Index + row.Index + cell.Groups[3].Index);
                        }

                        LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                        foreach (Rules.RemarkupRule childtoken in remarkupParserOutput.TokenList.ToArray())
                        {
                            childtoken.Start += Start + tokenPositions[rowIndex][cellIndex];
                            ChildTokenList.Add(childtoken);
                        }

                        string cellValueWithBR = "";
                        for (int c = 0; c < cellValue.Length; c++)
                        {
                            if (cellValue[c] == '\r') continue;
                            if (cellValue[c] == '\n')
                            {
                                if (cellValue.Substring(c + 1).TrimStart(' ', '\t').StartsWith("<"))
                                {
                                    cellValueWithBR += "\n";
                                }
                                else
                                if (c > 0 && cellValueWithBR.TrimEnd().EndsWith(">"))
                                {
                                    cellValueWithBR += "\n";
                                }
                                else
                                {
                                    cellValueWithBR += "<br>";
                                }

                                continue;
                            }

                            cellValueWithBR += cellValue[c];
                        }

                        tableContent += string.Format("    <{0}{1}>{2}</{0}>\n", 
                                                cellType,
                                                cellConcealed,
                                                cellValueWithBR);
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
