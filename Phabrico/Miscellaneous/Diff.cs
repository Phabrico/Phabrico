using System;
using System.Collections.Generic;
using System.Linq;

namespace Phabrico.Miscellaneous
{
    /// <summary>
    /// Contains some functionality to compare 2 blocks of text and visualize the differences between the two.
    /// </summary>
    public class Diff
    {
        /// <summary>
        /// Diff partial from 2 strings
        /// </summary>
        public class Part
        {
            /// <summary>
            /// First string
            /// </summary>
            public string Left { get; private set; }

            /// <summary>
            /// Second string
            /// </summary>
            public string Right { get; private set; }

            /// <summary>
            /// Initializes a new instance of a Diff.Part
            /// </summary>
            /// <param name="left"></param>
            /// <param name="right"></param>
            public Part(string left, string right)
            {
                this.Left = left;
                this.Right = right;
            }

            /// <summary>
            /// True if any of the strings to be diff'ed contains a newline character
            /// </summary>
            public bool ContainsNewLine
            {
                get
                {
                    return (Left != null && Left.Contains("\n"))
                        || (Right != null && Right.Contains("\n"));
                }
            }

            /// <summary>
            /// Returns the Diff.Part in a readable format
            /// Is just for debugging
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return string.Format("Left: {0}\r\nRight: {1}", Left, Right);
            }
        }

        /// <summary>
        /// Calculates the difference between two specified strings by means of the Patience algorithm
        /// </summary>
        /// <param name="originalText"></param>
        /// <param name="modifiedText"></param>
        /// <returns></returns>
        private IEnumerable<Diff.Part> Execute(string originalText, string modifiedText)
        {
            List<Diff.Part> diffParts = new List<Diff.Part>();

            // remove \r characters
            if (originalText != null) originalText = originalText.Replace("\r", "");
            if (modifiedText != null) modifiedText = modifiedText.Replace("\r", "");

            if (originalText == null)
            {
                if (modifiedText != null)
                {
                    foreach (string modifiedTextLine in modifiedText.Split('\n'))
                    {
                        Diff.Part diffPart = new Diff.Part(null, modifiedTextLine);
                        diffParts.Add(diffPart);
                    }
                }

                return diffParts;
            }

            if (modifiedText == null)
            {
                foreach (string originalTextLine in originalText.Split('\n'))
                {
                    Diff.Part diffPart = new Diff.Part(originalTextLine, null);
                    diffParts.Add(diffPart);
                }

                return diffParts;
            }

            if (originalText == "" && modifiedText == "")
            {
                // empty line on both sides
                Diff.Part diffPart = new Diff.Part("", "");
                diffParts.Add(diffPart);
                return diffParts;
            }

            // search for begin and end of modified text
            List<string> originalTextLines = originalText.Split('\n').ToList();
            List<string> modifiedTextLines = modifiedText.Split('\n').ToList();

            // return positions of lines which appear only once in both originalTextLines and modifiedTextLines
            // key=position of line in modifiedTextLines;  value=position of line in originalTextLines
            Dictionary<int, int> uniqueLinePositions = originalTextLines.Where(line => originalTextLines.Count(o => o.Trim().Equals(line.Trim())) == 1
                                                                                    && modifiedTextLines.Count(m => m.Trim().Equals(line.Trim())) == 1
                                                                              )
                                                                       .ToDictionary(k => modifiedTextLines.Select(line => line.Trim()).ToList().IndexOf(k.Trim()),
                                                                                     v => originalTextLines.Select(line => line.Trim()).ToList().IndexOf(v.Trim())
                                                                                    );

            // calculate longest increasing subsequence
            int[] lis = GetLongestIncreasingSubsequence(uniqueLinePositions.Keys);

            int originalPosition = 0, modifiedPosition = 0;
            string originalPart, modifiedPart;
            foreach (int lineIndex in lis)
            {
                int nbrLinesToTakeFromOriginal = uniqueLinePositions[lineIndex] - originalPosition;
                int nbrLinesToTakeFromModified = lineIndex - modifiedPosition;
                originalPart = string.Join("\n", originalTextLines.Skip(originalPosition).Take(nbrLinesToTakeFromOriginal));
                modifiedPart = string.Join("\n", modifiedTextLines.Skip(modifiedPosition).Take(nbrLinesToTakeFromModified));

                if (nbrLinesToTakeFromOriginal == 0)
                {
                    if (nbrLinesToTakeFromModified == 0)
                    {
                        break;
                    }

                    originalPart = null;
                }

                if (nbrLinesToTakeFromModified == 0)
                {
                    modifiedPart = null;
                }

                diffParts.Add(new Diff.Part(originalPart, modifiedPart));

                originalPosition = uniqueLinePositions[lineIndex];
                modifiedPosition = lineIndex;
            }

            originalPart = string.Join("\n", originalTextLines.Skip(originalPosition));
            modifiedPart = string.Join("\n", modifiedTextLines.Skip(modifiedPosition));
            if (originalPart.Any() || modifiedPart.Any())
            {
                diffParts.Add(new Diff.Part(originalPart, modifiedPart));
            }

            List<Diff.Part> newDiffParts = new List<Diff.Part>();
            for (int d = 0; d < diffParts.Count; d++)
            {
                if (diffParts[d].ContainsNewLine)
                {
                    if (diffParts[d].Left == null)
                    {
                        if (diffParts[d].Right != null)
                        {
                            foreach (string right in diffParts[d].Right.Split('\n'))
                            {
                                Diff.Part diffPart = new Diff.Part(null, right);
                                newDiffParts.Add(diffPart);
                            }
                        }
                    }
                    else
                    {
                        if (diffParts[d].Right != null)
                        {
                            string[] leftLines = diffParts[d].Left.Split('\n').ToArray();
                            string[] rightLines = diffParts[d].Right.Split('\n').ToArray();

                            string leftAllButFirst, rightAllButFirst;
                            if (leftLines.Count() == 1)
                            {
                                leftAllButFirst = null;
                            }
                            else
                            {
                                leftAllButFirst = string.Join("\n", leftLines.Skip(1));
                            }

                            if (rightLines.Count() == 1)
                            {
                                rightAllButFirst = null;
                            }
                            else
                            {
                                rightAllButFirst = string.Join("\n", rightLines.Skip(1));
                            }

                            newDiffParts.Add(new Diff.Part(leftLines.First(), rightLines.First()));
                            newDiffParts.AddRange(Execute(leftAllButFirst, rightAllButFirst));
                        }
                        else
                        {
                            foreach (string left in diffParts[d].Left.Split('\n'))
                            {
                                Diff.Part diffPart = new Diff.Part(left, null);
                                newDiffParts.Add(diffPart);
                            }
                        }
                    }
                }
                else
                {
                    newDiffParts.Add(diffParts[d]);
                }
            }

            return newDiffParts;
        }

        /// <summary>
        /// Compares 2 blocks of text and returns the differences between them in HTML format
        /// </summary>
        /// <param name="originalText">First block of text to be compared. The differences with the second block will be written in this variable.</param>
        /// <param name="modifiedText">Second block of text to be compared. The differences with the first block will be written in this variable.</param>
        /// <param name="isReadOnly">If false, the HTML result wil also contain merge buttons</param>
        /// <param name="locale">Language code in which some of the fixed text (e.g. button tooltips) should be translated to</param>
        public static void GenerateDiffLeftRight(ref string originalText, ref string modifiedText, bool isReadOnly, Language locale)
        {
            Diff diff = new Diff();

            originalText = originalText.Replace("\t", " ");      // Replace tab with space
            modifiedText = modifiedText.Replace("\t", " ");      // Replace tab with space
            modifiedText = modifiedText.Replace("\u00A0", " ");  // Replace non breaking space with space

            string path = diff.GenerateDiffPath(originalText, modifiedText);

            // convert full path to line-specific paths
            string[] lines = path.Split('\n').ToArray();
            int lineCounterLeft = 1;
            int lineCounterRight = 1;
            string contentLeft = "";
            string contentRight = "";
            foreach (string line in lines.Select(l => l + "\n"))
            {
                bool left = false;
                bool right = false;
                bool equal = true;
                bool replace = false;
                if (line.Length > 2)
                {
                    for (int c = 0; c < (line.Length / 2) * 2; c += 2)
                    {
                        if (line[c] != line[2] && line[c + 1] != '\n' && (c == 0 || line[c - 1] != '\n'))
                        {
                            replace = true;
                            break;
                        }
                    }

                    if (replace == false)
                    {
                        for (int eq = 0; eq < (line.Length / 2) * 2; eq += 2)
                        {
                            if (line[eq] != '=' && line[eq + 1] != '\n')
                            {
                                equal = false;
                                right = (line[eq] == '>');
                                left = (line[eq] == '<');
                                break;
                            }
                        }
                    }
                }
                else
                {
                    equal = (line[0] == '=');
                    if (equal == false)
                    {
                        right = (line[0] == '>');
                        left = (line[0] == '<');
                    }
                }

                // start converting to HTML
                string strLeft = "";
                string strRight = "";
                if (replace)
                {
                    if (line.Length > 0)
                    {
                        if (string.IsNullOrEmpty(contentLeft) || contentLeft.EndsWith("</td></tr>"))
                        {
                            strLeft += string.Format("<td class='replace left left{0}'>", lineCounterLeft);
                        }

                        strRight += string.Format("<td class='replace right{0}'>", lineCounterRight);

                        for (int c = 0; c < (line.Length / 2) * 2; c += 2)
                        {
                            if (line[c] == '=')
                            {
                                strLeft += System.Web.HttpUtility.HtmlEncode(line[c + 1]).Replace(" ", "&nbsp;");
                                strRight += System.Web.HttpUtility.HtmlEncode(line[c + 1]).Replace(" ", "&nbsp;");
                            }
                            else
                            if (line[c] == '<')
                            {
                                strLeft += "<em>";
                                for (; c < (line.Length / 2) * 2; c += 2)
                                {
                                    strLeft += System.Web.HttpUtility.HtmlEncode(line[c + 1]).Replace(" ", "&nbsp;");

                                    if (c + 2 == (line.Length / 2) * 2 ||
                                        line[c + 2] != '<')
                                    {
                                        break;
                                    }
                                }
                                strLeft += "</em>";
                            }
                            else
                            if (line[c] == '>')
                            {
                                strRight += "<em>";
                                for (; c < (line.Length / 2) * 2; c += 2)
                                {
                                    strRight += System.Web.HttpUtility.HtmlEncode(line[c + 1]).Replace(" ", "&nbsp;");

                                    if (c + 2 == (line.Length / 2) * 2 ||
                                        line[c + 2] != '>')
                                    {
                                        break;
                                    }
                                }
                                strRight += "</em>";
                            }
                        }

                        strLeft += "</td>";
                        strRight += "</td>";

                        contentLeft += string.Format("<tr><th class='line-nr'>{0}</th>{1}</tr>",
                                            lineCounterLeft,
                                            strLeft);
                        contentRight += string.Format("<tr data-leftline='{3}' data-rightline='{1}'><th class='insert'>{0}</th><th class='line-nr'>{1}</th>{2}</tr>", 
                                            isReadOnly ? "" 
                                                       : diff.GenerateInsertButtons(lineCounterLeft, "insert", locale), 
                                            lineCounterRight, 
                                            strRight, 
                                            lineCounterLeft);

                        lineCounterLeft++;
                        lineCounterRight++;
                    }
                }
                else
                if (equal)
                {
                    if (line.Length > 0)
                    {
                        for (int c = 0; c < (line.Length / 2) * 2; c += 2)
                        {
                            strLeft += line[c + 1];
                        }

                        strLeft = System.Web.HttpUtility.HtmlEncode(strLeft).Replace(" ", "&nbsp;");

                        contentLeft += string.Format("<tr><th class='line-nr'>{0}</th><td class='equal left left{0}'>{1}</td></tr>",
                                                lineCounterLeft, 
                                                strLeft);
                        contentRight += string.Format("<tr data-leftline='{2}' data-rightline='{0}'><th class='insert'></th><th class='line-nr'>{0}</th><td class='equal right{0}'>{1}</td></tr>",
                                                lineCounterRight,
                                                strLeft,
                                                lineCounterLeft);

                        lineCounterLeft++;
                        lineCounterRight++;
                    }
                }
                else
                if (left)
                {
                    for (int c = 0; c < (line.Length / 2) * 2; c += 2)
                    {
                        if (line[c] == '<' || line[c] == '=')
                        {
                            strLeft += line[c + 1];
                        }
                    }

                    strLeft = System.Web.HttpUtility.HtmlEncode(strLeft).Replace(" ", "&nbsp;");

                    contentLeft += string.Format("<tr><th class='line-nr'>{0}</th><td class='delete left left{0}'>{1}</td></tr>",
                                        lineCounterLeft,
                                        strLeft);
                    contentRight += string.Format("<tr data-leftline='{2}' data-rightline='{1}'><th class='insert'>{0}</th><th class='line-nr'></th><td class='empty right right{1}'></td></tr>", 
                                        isReadOnly ? "" 
                                                   : diff.GenerateInsertButtons(lineCounterLeft, "empty", locale), 
                                        lineCounterRight, 
                                        lineCounterLeft);
                    lineCounterLeft++;
                }
                else
                if (right)
                {
                    for (int c = 0; c < (line.Length / 2) * 2; c += 2)
                    {
                        if (line[c] == '>' || line[c] == '=')
                        {
                            strRight += line[c + 1];
                        }
                    }

                    strRight = System.Web.HttpUtility.HtmlEncode(strRight).Replace(" ", "&nbsp;");

                    contentRight += string.Format("<tr data-leftline='{3}' data-rightline='{1}'><th class='insert'>{0}</th><th class='line-nr'>{1}</th><td class='insert right{1}'>{2}</td></tr>", 
                                        isReadOnly ? "" 
                                                   : diff.GenerateInsertButtons(lineCounterLeft, "empty", locale), 
                                        lineCounterRight, 
                                        strRight, 
                                        lineCounterLeft);
                    contentLeft += "<tr><th class='line-nr'></th><td class='empty left'></td></tr>";
                    lineCounterRight++;
                }
            }

            originalText = contentLeft;
            modifiedText = contentRight;
        }


        /// <summary>
        /// Generates the HTML code for the merge button(s)
        /// </summary>
        /// <param name="lineCounterLeft"></param>
        /// <param name="actionRight"></param>
        /// <param name="locale"></param>
        /// <returns></returns>
        private object GenerateInsertButtons(int lineCounterLeft, string actionRight, Language locale)
        {
            if (actionRight.Equals("empty"))
            {
                return string.Format(@"<span>
                  <div></div>
                  <div title=""{0}"" class='button small insert-replace button-blue' style='padding: 0px 5px;font-size: 2em;'></div>
                  <div></div>
                </span>",
                Locale.TranslateText("Replace right line with left line", locale));
            }
            else
            {
                return string.Format(@"<span>
                  <div title=""{0}"" class='button small insert-before button-blue' style='padding: 0px 5px;font-size: 2em;'></div>
                  <div title=""{1}"" class='button small insert-replace button-blue' style='padding: 0px 5px;font-size: 2em;'></div>
                  <div title=""{2}"" class='button small insert-after button-blue' style='padding: 0px 5px;font-size: 2em;'></div>
                </span>",
                Locale.TranslateText("Insert left line before right line", locale),
                Locale.TranslateText("Replace right line with left line", locale),
                Locale.TranslateText("Append left line after right line", locale));
            }
        }

        /// <summary>
        /// Generates a string which identifies the differences between 2 strings.
        /// Each character is prefixed by a '&lt;', '=' or a '&gt;' character.
        /// If the character is prefixed by a '=', the character is present in both strings.
        /// If the character is prefixed by a '&lt;', the character is only present in the originalText string.
        /// If the character is prefixed by a '&gt;', the character is only present in the modifiedText string.
        /// </summary>
        /// <param name="originalText"></param>
        /// <param name="modifiedText"></param>
        /// <returns>A string which identifies the differences between 2 strings</returns>
        public string GenerateDiffPath(string originalText, string modifiedText)
        {
            string diffPath = "";

            Diff.Part[] diffParts = Execute(originalText, modifiedText).ToArray();

            foreach (Diff.Part diffPart in diffParts)
            {
                if (diffPart.Left == null)
                {
                    foreach (char character in diffPart.Right)
                    {
                        diffPath += ">" + character;
                    }

                    if (diffParts.LastOrDefault() != diffPart || modifiedText.EndsWith("\n"))
                    {
                        diffPath += ">\n";
                    }
                }
                else if (diffPart.Right == null)
                {
                    foreach (char character in diffPart.Left)
                    {
                        diffPath += "<" + character;
                    }


                    if (diffParts.LastOrDefault() != diffPart || originalText.EndsWith("\n"))
                    {
                        diffPath += "<\n";
                    }
                }
                else
                {
                    // start calculating longest common subsequence
                    string lcs = "";
                    int[,] lcsMatrix = new int[diffPart.Left.Length + 1, diffPart.Right.Length + 1];
                    for (int i = 0; i <= diffPart.Left.Length; i++)
                    {
                        for (int j = 0; j <= diffPart.Right.Length; j++)
                        {
                            if (i == 0 || j == 0)
                            {
                                lcsMatrix[i, j] = 0;
                            }
                            else if (diffPart.Left[i - 1] == diffPart.Right[j - 1])
                            {
                                lcsMatrix[i, j] = lcsMatrix[i - 1, j - 1] + 1;
                            }
                            else
                            {
                                lcsMatrix[i, j] = Math.Max(lcsMatrix[i - 1, j], lcsMatrix[i, j - 1]);
                            }
                        }
                    }

                    int index = lcsMatrix[diffPart.Left.Length, diffPart.Right.Length];
                    int k = diffPart.Left.Length, l = diffPart.Right.Length;
                    while (k > 0 && l > 0)
                    {
                        if (diffPart.Left[k - 1] == diffPart.Right[l - 1])
                        {
                            // equal characters found -> put it into result
                            lcs = diffPart.Left[k - 1] + lcs;

                            k--;
                            l--;
                            index--;
                        }
                        else if (lcsMatrix[k - 1, l] > lcsMatrix[k, l - 1])
                        {
                            k--;
                        }
                        else
                        {
                            l--;
                        }
                    }


                    // create path string:
                    // left and right will be compared character by character
                    // if both characters are the same, a '=' will be prefixed
                    // if character differs from left, a '>' will be prefixed
                    // otherwise a '<' will be prefixed
                    int u = 0;
                    int ds1 = 0;
                    int ds2 = 0;
                    while (lcs.Any() && ds1 < diffPart.Left.Length && ds2 < diffPart.Right.Length)
                    {
                        if (diffPart.Left[ds1] == lcs[u] && diffPart.Right[ds2] == lcs[u])
                        {
                            diffPath += "=" + lcs[u];
                            u++;
                            ds1++;
                            ds2++;

                            if (u == lcs.Length) break;

                            continue;
                        }

                        if (diffPart.Left[ds1] != lcs[u])
                        {
                            diffPath += "<" + diffPart.Left[ds1];
                            ds1++;
                        }

                        if (diffPart.Right[ds2] != lcs[u])
                        {
                            diffPath += ">" + diffPart.Right[ds2];
                            ds2++;
                        }
                    }

                    // right part is larger than left => prefix for each remaining character a '+'
                    for (; ds1 < diffPart.Left.Length; ds1++)
                    {
                        diffPath += "<" + diffPart.Left[ds1];
                    }

                    // left part is larger than right => prefix for each remaining character a '-'
                    for (; ds2 < diffPart.Right.Length; ds2++)
                    {
                        diffPath += ">" + diffPart.Right[ds2];
                    }

                    if (diffParts.LastOrDefault() != diffPart || originalText.EndsWith("\n"))
                    {
                        diffPath += "=\n";
                    }
                }
            }

            return diffPath;
        }

        /// <summary>
        /// Returns the longest increasing subsequence of a sequence
        /// </summary>
        /// <param name="sequence"></param>
        /// <returns></returns>
        private static int[] GetLongestIncreasingSubsequence(IEnumerable<int> sequence)
        {
            List<int> result = new List<int>();

            if (sequence.Any())
            {
                List<List<int>> piles = new List<List<int>>();
                piles.Add(new List<int>());
                piles[0].Add(sequence.First());

                // start playing patience
                foreach (int card in sequence.Skip(1))
                {
                    bool pileFound = false;
                    foreach (List<int> pile in piles)
                    {
                        if (pile.LastOrDefault() > card)
                        {
                            pile.Add(card);
                            pileFound = true;
                            break;
                        }
                    }

                    if (pileFound == false)
                    {
                        piles.Add(new List<int>());
                        piles.Last().Add(card);
                    }
                }

                // calculate longest increasing subsequence from cards in piles
                result.Add(piles.Last().Last());

                for (int p = piles.Count - 2; p >= 0; p--)
                {
                    int value = piles[p].FirstOrDefault(v => v < result.FirstOrDefault());
                    result.Insert(0, value);
                }
            }

            return result.ToArray();
        }
    }
}
