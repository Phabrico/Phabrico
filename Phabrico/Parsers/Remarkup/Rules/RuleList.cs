using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Remarkup parser for lists.
    /// These include numeric, dotted and checkbox lists
    /// </summary>
    [RulePriority(10)]
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
    [RuleXmlTag("LS")]
    public class RuleList : RemarkupRule
    {
        /// <summary>
        /// Subclass for a single List item
        /// </summary>
        public class ListElement
        {
            /// <summary>
            /// Type of list item
            /// </summary>
            public enum ListBulletType
            {
                /// <summary>
                /// List item with a big dot as bullet
                /// </summary>
                Regular,

                /// <summary>
                /// List item with a number as bullet
                /// </summary>
                Numeric,

                /// <summary>
                /// List item with a checked checkbox as bullet
                /// </summary>
                Checked,

                /// <summary>
                /// List item with an unchecked checkbox as bullet
                /// </summary>
                Unchecked
            }

            /// <summary>
            /// The depth of the list-item; this is calculated by the number of spaces preceding the list-bullet
            /// </summary>
            public int Depth { get; set; }

            /// <summary>
            /// The text content after the list-bullet
            /// </summary>
            public string Content { get; set; }

            /// <summary>
            /// Type of list-bullet (e.g. numeric, regular, checkbox)
            /// </summary>
            public ListBulletType Bullet { get; set; }

            /// <summary>
            /// In case the list-bullet is a numeric one, BulletStart contains the number that should be representing the first list-bullet
            /// </summary>
            public int BulletStart { get; set; }

            /// <summary>
            /// Start position in Remarkup content
            /// </summary>
            public int StartOffset { get; set; }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="depth"></param>
            /// <param name="bullet"></param>
            /// <param name="content"></param>
            public ListElement(int depth, string bullet, string content)
            {
                this.Depth = depth;
                this.Content = content;

                // determine bullet type (i.e. numeric, regular or checkboxes)
                if (bullet.StartsWith("#"))
                {
                    this.Bullet = ListBulletType.Numeric;
                    this.Depth += bullet.Length - 1;
                }
                else
                {
                    Match numericBulletMatch = RegexSafe.Match(bullet, "([1-9][0-9]*)[.)]", RegexOptions.Singleline);
                    if (numericBulletMatch.Success)
                    {
                        this.Bullet = ListBulletType.Numeric;
                        this.BulletStart = Int32.Parse(numericBulletMatch.Groups[1].Value);
                    }
                    else
                    if (RegexSafe.IsMatch(bullet, @"\[[xX]\]", RegexOptions.Singleline))
                    {
                        this.Bullet = ListBulletType.Checked;
                        this.BulletStart = 1;
                    }
                    else
                    if (RegexSafe.IsMatch(bullet, @"\[[ ]?\]", RegexOptions.Singleline))
                    {
                        this.Bullet = ListBulletType.Unchecked;
                    }
                    else
                    {
                        this.Bullet = ListBulletType.Regular;
                        this.Depth += bullet.Length - 1;
                    }
                }

                Match match = RegexSafe.Match(this.Content, @"^\[([ xX]?)\] *", RegexOptions.Singleline);
                if (match.Success)
                {
                    if (match.Groups[1].Value.Equals("X", StringComparison.OrdinalIgnoreCase))
                    {
                        this.Bullet = ListBulletType.Checked;
                    }
                    else
                    {
                        this.Bullet = ListBulletType.Unchecked;
                    }

                    this.Content = this.Content.Substring(match.Length);
                }
            }

            /// <summary>
            /// Returns the list content
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return Content;
            }
        }

        private List<ListElement> listElements;
        private RemarkupParserOutput remarkupParserOutput;

        public DataTreeNode<ListElement> Tree { get; private set; }

        /// <summary>
        /// Deeper cloner
        /// </summary>
        /// <param name="originalRemarkupRule"></param>
        public override RemarkupRule Clone()
        {
            RuleList copy = base.Clone() as RuleList;
            if (copy != null)
            {
                copy.Tree = Tree;
            }

            return copy;
        }

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
                Match match = RegexSafe.Match(remarkup, @"^( *(-+|#+|\*+|\[[ xX]?\]|[1-9][0-9]*[.)]) +[^\n]*)((\r?\n){0,2} *(-+|#+|\*+|\[[ xX]?\]|[1-9][0-9]*[.)])? +[^\n]*)*(\r?\n?\r?\n?)", RegexOptions.Singleline);
                if (match.Success == false) return false;

                string localRemarkup = match.Value;

                // check if match is a real list
                if (match.Value.Trim('\r', '\n').Equals(remarkup.Trim('\r', '\n')) == false)  // check if not at the end of the remarkup content
                {
                    if (RegexSafe.IsMatch(localRemarkup, "^#+ ", RegexOptions.Singleline))    // check if match is not part of a header
                    {
                        // check content of second line (does it contain a list item bullet ?)
                        string remarkupSecondLine = localRemarkup.Split('\n').Skip(1).FirstOrDefault() ?? "";
                        remarkupSecondLine = remarkupSecondLine.Trim('\r');
                        if (RegexSafe.IsMatch(remarkupSecondLine, @"^( *[#-\*] | *[^ \r])", RegexOptions.Singleline) == false)
                        {
                            // match is not part of list -> skip
                            return false;
                        }
                    }
                }

                if (RegexSafe.IsMatch(localRemarkup, @"^(\*\*\*|\* \* \*|- ?- ?-)", RegexOptions.Singleline)) // check if match is not part of a horizontal rule
                {
                    // match is not part of list -> skip
                    return false;
                }

                Match matchNextList = RegexSafe.Match(localRemarkup, "\r?\n\r?\n([^ ])", RegexOptions.Singleline);
                if (matchNextList.Success)
                {
                    localRemarkup = localRemarkup.Substring(0, matchNextList.Groups[1].Index);
                }

                // start converting lines into ListElements
                Match[] lineMatches = RegexSafe.Matches(localRemarkup.TrimEnd('\r', '\n'), "[^\n]+(\n|$)")
                                         .OfType<Match>()
                                         .ToArray();

                listElements = new List<ListElement>();
                foreach (Match lineMatch in lineMatches.SkipWhile(m => m.Value.Trim('\r', '\n').Length == 0))
                {
                    string line = lineMatch.Value.TrimEnd(' ', '\r', '\n');
                    Match matchLine = RegexSafe.Match(line, @"^( *)(-+|#+|\*+|\[[ xX]?\]|[1-9[0-9]*[).])? +(.*) *", RegexOptions.Singleline);
                    if (matchLine.Success == false)
                    {
                        ListElement lastListElement = listElements.LastOrDefault();
                        if (lastListElement != null)
                        {
                            lastListElement.Content += "\n\n";
                        }

                        continue;  // skip empty line
                    }

                    int depth = matchLine.Groups[1].Value.Length;
                    string listBullet = matchLine.Groups[2].Value;
                    string content = matchLine.Groups[3].Value;

                    if (string.IsNullOrEmpty(listBullet))
                    {
                        if (listElements.Any())
                        {
                            listElements.Last().Content += " " + content;
                        }
                    }
                    else
                    {
                        ListElement listElement = new ListElement(depth, listBullet, content);
                        listElement.StartOffset = lineMatch.Index;
                        listElements.Add(listElement);
                    }
                }

                // convert ListElements into DataTree
                Tree = new DataTreeNode<ListElement>(14);
                DataTreeNode<ListElement> previousTreeNode = Tree;
                foreach (ListElement listElement in listElements)
                {
                    DataTreeNode<ListElement> newTreeListNode = new DataTreeNode<ListElement>(listElement);

                    while (true)
                    {
                        if (previousTreeNode.Parent == null || previousTreeNode.Me.Depth < listElement.Depth)
                        {
                            // new depth detected: create new sub-list

                            // check if we have a crazy-stair-case sub-list
                            //   a crazy-stair-case list is a list which is defined backwards
                            //   for example:
                            //       - top
                            //         - middle
                            //             - crazy-middle
                            //           - bottom
                            if (previousTreeNode.Children.Any(child => child.Depth < listElement.Depth))
                            {
                                // crazy-stair-case sub-list detected: move previous 'sibling' to a new phantom sub-list
                                DataTreeNode<ListElement> newTreePhantomNode = new DataTreeNode<ListElement>();
                                newTreePhantomNode.Children = previousTreeNode.Children;
                                previousTreeNode.Children = new List<DataTreeNode<ListElement>>();
                                previousTreeNode.Add(newTreePhantomNode);
                            }
                            
                            previousTreeNode.Add(newTreeListNode);

                            if (previousTreeNode.Depth == newTreeListNode.Depth)
                            {
                                // maximum depth reached => move newTreeListNode to parent node
                                newTreeListNode.MoveTo(previousTreeNode.Parent);
                            }

                            previousTreeNode = newTreeListNode;
                            break;
                        }

                        if (previousTreeNode.Me.Depth == listElement.Depth)
                        {
                            // same treenode depth: add new treenode-item as a sibling
                            previousTreeNode.Parent.Add(newTreeListNode);
                            previousTreeNode = newTreeListNode;
                            break;
                        }

                        // check if previous node-depth was closed
                        if (previousTreeNode.Me.Depth > listElement.Depth)
                        {
                            // check if we have a crazy-stair-case list
                            //   a crazy-stair-case list is a list which is defined backwards
                            //   for example:
                            //            - top
                            //          - middle
                            //        - bottom
                            if (previousTreeNode.Parent.Me == null)
                            {
                                // crazy-stair-case list detected

                                // create a phantom list which will contain the new item and a sublist containing the previous item
                                DataTreeNode<ListElement> newTreePhantomNode = new DataTreeNode<ListElement>(previousTreeNode.MaximumDepth);
                                DataTreeNode<ListElement> originalParent = previousTreeNode.Parent;

                                // move all children to the phantom list
                                foreach (DataTreeNode<ListElement> sibling in previousTreeNode.Siblings.ToList())
                                {
                                    sibling.MoveTo(newTreePhantomNode);
                                }
                                previousTreeNode.MoveTo(newTreePhantomNode);

                                // move the phantom list and the new item to the original parent
                                originalParent.Add(newTreePhantomNode);
                                originalParent.Add(newTreeListNode);

                                previousTreeNode = newTreeListNode;
                                break;
                            }
                            else
                            {
                                // previous node depth was closed
                                previousTreeNode = previousTreeNode.Parent;
                            }
                        }
                    }
                }

                // convert tree to HTML
                string[] htmlLines = GenerateListHtml(Tree, browser, database, url).Split('\n').ToArray();

                // complete whitespace indenting
                int indentSize = 0;
                foreach (string line in htmlLines.Where(htmlLine => htmlLine.Any()))
                {
                    Match htmlTagMatch = RegexSafe.Match(line, "^<(/?)([^/> ]*)", RegexOptions.None);
                    bool openingTag = htmlTagMatch.Groups[1].Value.Length == 0;
                    bool closingTag = htmlTagMatch.Groups[1].Value.Length > 0;
                    string tag = htmlTagMatch.Groups[2].Value;

                    if (line.EndsWith("/>") || (line.StartsWith("</" + tag + ">") == false && line.EndsWith("</" + tag + ">")))
                    {
                        openingTag = false;
                        closingTag = false;
                    }

                    if (openingTag)
                    {
                        html += new string(' ', indentSize) + line + "\n";
                        indentSize += 2;
                    }
                    else
                    if (closingTag)
                    {
                        indentSize -= 2;
                        html += new string(' ', indentSize) + line + "\n";
                    }
                    else
                    {
                        html += new string(' ', indentSize) + line + "\n";
                    }
                }

                remarkup = remarkup.Substring(localRemarkup.Length);
                if (string.IsNullOrEmpty(html) == false)
                {
                    Length = match.Length;
                    return true;
                }

                return false;
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
            string result = "";
            string[] listItems = RegexSafe.Split(innerText, "</[bcnu][0-9]+>", RegexOptions.Singleline)
                                          .Select(item => item.Trim(' ', '\r', '\n', '\t'))
                                          .Where(item => item.Any())
                                          .SelectMany(item => RegexSafe.Matches(item, 
                                                                                "<[bcnu][0-9]+[^>]*>[^\n]*", RegexOptions.Singleline)
                                                                       .OfType<Match>()
                                                                       .Select(match => match.Value)
                                                     )
                                          .ToArray();
            foreach (string listItem in listItems)
            {
                Match header = RegexSafe.Match(listItem, "^<([bcnu])([0-9]+)([^>]*)>", RegexOptions.None);
                if (header.Success)
                {
                    char listType = header.Groups[1].Value[0];
                    int listDepth = Int32.Parse(header.Groups[2].Value) - 1;
                    int? startValue = null;
                    string listAttributes = header.Groups[3].Value;
                    Match matchStartValue = RegexSafe.Match(listAttributes, "s=\"([0-9]+)\"", RegexOptions.None);
                    if (matchStartValue.Success)
                    {
                        startValue = Int32.Parse(matchStartValue.Groups[1].Value);
                    }
                    
                    result += new string(' ', listDepth * 2);
                    switch (listType)
                    {
                        case 'b':
                            result += "-";
                            break;

                        case 'c':
                            result += "- [X]";
                            break;

                        case 'n':
                            if (startValue.HasValue)
                            {
                                result += startValue + ")";
                            }
                            else
                            {
                                result += "#";
                            }
                            break;

                        case 'u':
                            result += "- [ ]";
                            break;
                    }

                    result += " ";
                    result += RemarkupTokenList.XMLToRemarkup(Database, Browser, DocumentURL, listItem.Substring(header.Length));
                    result += "\n";
                }
            }

            return result + "<N>[1]</N>\n";
        }

        /// <summary>
        /// Converts a given Treelist of ListElements into a HTML list
        /// </summary>
        /// <param name="treeNode">Treelist of ListElements</param>
        /// <param name="browser">Link to browser</param>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="url">Current browser's url</param>
        /// <returns></returns>
        private string GenerateListHtml(DataTreeNode<ListElement> treeNode, Browser browser, Storage.Database database, string url)
        {
            DataTreeNode<ListElement> firstChildNode = treeNode.Children.FirstOrDefault(elem => elem.Me != null);
            string html = "";
            string bgnTag = "";
            string endTag = "";

            // determine the list-type (e.g. numeric, regular or checkbox) based on the first list-item
            if (firstChildNode != null)
            {
                if (firstChildNode.Me.Bullet == ListElement.ListBulletType.Numeric)
                {
                    if (firstChildNode.Me.BulletStart <= 1)
                    {
                        bgnTag = "<ol class='remarkup-list";
                    }
                    else
                    {
                        bgnTag = string.Format("<ol start='{0}' class='remarkup-list",
                                                firstChildNode.Me.BulletStart);
                    }

                    endTag = "</ol>\n";
                }
                else
                {
                    bgnTag = "<ul class='remarkup-list";
                    endTag = "</ul>\n";
                }

                if (treeNode.Children.Any(child => child.Me != null && (child.Me.Bullet == ListElement.ListBulletType.Checked || child.Me.Bullet == ListElement.ListBulletType.Unchecked)))
                {
                    bgnTag += " remarkup-list-with-checkmarks'>\n";
                }
                else
                {
                    bgnTag += "'>\n";
                }
            }

            // start generating the resulting HTML
            html += bgnTag;

            foreach (DataTreeNode<ListElement> child in treeNode.Children)
            {
                if (child.Me != null)
                {
                    switch (child.Me.Bullet)
                    {
                        case ListElement.ListBulletType.Unchecked:
                            html += string.Format("<li class='remarkup-list-item remarkup-unchecked-item'><input type='checkbox' disabled='disabled'> {0}",
                                            Engine.ToHTML(this, database, browser, url, child.Me.Content.TrimEnd('\r', '\n'), out remarkupParserOutput, false));
                            LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                            ChildTokenList.AddRange(remarkupParserOutput.TokenList);
                            break;

                        case ListElement.ListBulletType.Checked:
                            html += string.Format("<li class='remarkup-list-item remarkup-checked-item'><input type='checkbox' checked='checked' disabled='disabled'> {0}",
                                            Engine.ToHTML(this, database, browser, url, child.Me.Content.TrimEnd('\r', '\n'), out remarkupParserOutput, false));
                            LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                            ChildTokenList.AddRange(remarkupParserOutput.TokenList);
                            break;

                        case ListElement.ListBulletType.Numeric:
                        case ListElement.ListBulletType.Regular:
                        default:
                            html += string.Format("<li class='remarkup-list-item'>{0}",
                                            ParagraphText(Engine.ToHTML(this, database, browser, url, child.Me.Content.TrimEnd('\r', '\n'), out remarkupParserOutput, false)));
                            LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                            ChildTokenList.AddRange(remarkupParserOutput.TokenList);
                            break;
                    }

                    foreach (RemarkupRule rule in remarkupParserOutput.TokenList)
                    {
                        rule.Start += Start + child.Me.StartOffset;
                    }

                    string innerHTML = GenerateListHtml(child, browser, database, url);

                    if (string.IsNullOrEmpty(innerHTML) == false)
                    {
                        html += "\n";
                    }

                    html += innerHTML;

                    html += "</li>\n";
                }
                else
                {
                    html += "<li class='remarkup-list-item phantom-item'>\n";

                    html += GenerateListHtml(child, browser, database, url);

                    html += "</li>\n";
                }
            }

            html += endTag;

            return html;
        }

        /// <summary>
        /// Generates a BrokenXml string based on the content of a tree of ListElements
        /// </summary>
        /// <param name="database"></param>
        /// <param name="browser"></param>
        /// <param name="url"></param>
        /// <param name="treeNode"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        public string GenerateListXML(Storage.Database database, Browser browser, string url, DataTreeNode<ListElement> treeNode = null, int level = 1)
        {
            string result = "";
            IEnumerable<DataTreeNode<ListElement>> nodes;

            if (treeNode == null) 
                nodes = Tree.Children;
            else
                nodes = treeNode.Children;

            if (level == 1) result += "\n" + BGN + RuleXmlTag.GetXmlTag(GetType()) + END;

            RemarkupEngine remarkupEngine = new RemarkupEngine();

            foreach (DataTreeNode<ListElement> node in nodes)
            {
                RemarkupParserOutput innerRemarkupParserOutput;
                string content = node.Me.Content.TrimEnd('\n');
                remarkupEngine.ToHTML(this, database, browser, url, content, out innerRemarkupParserOutput, false);

                string xmlContent = innerRemarkupParserOutput.TokenList.PrepareForXmlExport(database, browser, url);

                string tagName;
                string bulletStart = "";
                switch (node.Me.Bullet)
                {
                    case ListElement.ListBulletType.Checked:
                        tagName = "c";
                        break;

                    case ListElement.ListBulletType.Numeric:
                        tagName = "n";
                        if (node.Me.BulletStart != 0)
                        {
                            bulletStart = " s=\"" + node.Me.BulletStart + "\"";
                        }
                        break;

                    case ListElement.ListBulletType.Unchecked:
                        tagName = "u";
                        break;

                    default:
                        tagName = "b";
                        break;
                }

                if (node.Children.Any())
                {
                    result += string.Format("\n{0}" + BGN + "{1}{2}{3}" + END + "{4}{5}{0}" + BGN + "/{1}{2}" + END,
                                                new string(' ', level * 2),
                                                tagName,
                                                level,
                                                bulletStart,
                                                xmlContent.Replace("&", "&amp;")
                                                          .Replace(">", "&gt;")
                                                          .Replace("<", "&lt;"),
                                                GenerateListXML(database, browser, url, node, level + 1)
                                           );
                }
                else
                {
                    result += string.Format("\n{0}" + BGN + "{1}{2}{3}" + END + "{4}" + BGN + "/{1}{2}" + END,
                                                new string(' ', level * 2),
                                                tagName,
                                                level,
                                                bulletStart,
                                                xmlContent.Replace("&", "&amp;")
                                                          .Replace(">", "&gt;")
                                                          .Replace("<", "&lt;")
                                           );
                }
            }

            result += "\n";

            if (level == 1) result += "</" + RuleXmlTag.GetXmlTag(GetType()) + ">\n";

            return result;
        }

        /// <summary>
        /// Converts a given text into a HTML paragraph in case the text contains newlines.
        /// </summary>
        /// <param name="text">Text to be converted</param>
        /// <returns>If the text contains newlines, the result will be a HTML paragraphed text; if no newlines exist, the original text will be returned</returns>
        private string ParagraphText(string text)
        {
            if (text.Contains('\n'))
            {
                string result = "";
                foreach (string line in text.Split('\n'))
                {
                    result += string.Format("<p>{0}</p>", line.Trim());
                }

                result = result.Replace("<p></p>", "");
                result = result.Replace("</p>", "</p>\n");

                return "\n" + result;
            }
            else
            {
                return text;
            }
        }
    }
}
