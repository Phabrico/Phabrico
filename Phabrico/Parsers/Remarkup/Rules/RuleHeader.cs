using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Remarkup parser for paragraph headers
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
    [RuleXmlTag("H")]
    public class RuleHeader : RemarkupRule
    {
        RemarkupParserOutput remarkupParserOutput;

        public enum LineTypes {
            Hash = 0,
            Single = 1,
            Double = 2
        }

        public int Depth;
        public LineTypes LineType;
        public int HeaderNameCopy;
        public string HeaderName;

        public override string Attributes
        {
            get
            {
                return "d=\"" + Depth + "\" t=\"" + (int)LineType + "\"";
            }
        }

        /// <summary>
        /// clones a RemarkupRule into another one
        /// </summary>
        /// <param name="originalRemarkupRule"></param>
        public override void Clone(RemarkupRule originalRemarkupRule)
        {
            RuleHeader originalRuleHeader = originalRemarkupRule as RuleHeader;
            if (originalRuleHeader != null)
            {
                Depth = originalRuleHeader.Depth;
                HeaderName = originalRuleHeader.HeaderName;
                HeaderNameCopy = originalRuleHeader.HeaderNameCopy;
                LineType = originalRuleHeader.LineType;
            }
        }

        /// <summary>
        /// This method is executed before the ToHTML() is executed.
        /// It is meant for initializing some local variables
        /// </summary>
        public override void Initialize()
        {
            Depth = 0;
            LineType = LineTypes.Single;
            HeaderName = "";
            HeaderNameCopy = 0;
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
                if (ProcessSingleUnderlinedHeader(database, browser, url, ref remarkup, ref html) == true) return true;
                if (ProcessDoubleUnderlinedHeader(database, browser, url, ref remarkup, ref html) == true) return true;

                if (ProcessDoubleLinedHeaderLevel5(database, browser, url, ref remarkup, ref html) == true) return true;
                if (ProcessDoubleLinedHeaderLevel4(database, browser, url, ref remarkup, ref html) == true) return true;
                if (ProcessDoubleLinedHeaderLevel3(database, browser, url, ref remarkup, ref html) == true) return true;
                if (ProcessDoubleLinedHeaderLevel2(database, browser, url, ref remarkup, ref html) == true) return true;
                if (ProcessDoubleLinedHeaderLevel1(database, browser, url, ref remarkup, ref html) == true) return true;

                if (ProcessHashLinedHeaderLevel5(database, browser, url, ref remarkup, ref html) == true) return true;
                if (ProcessHashLinedHeaderLevel4(database, browser, url, ref remarkup, ref html) == true) return true;
                if (ProcessHashLinedHeaderLevel3(database, browser, url, ref remarkup, ref html) == true) return true;
                if (ProcessHashLinedHeaderLevel2(database, browser, url, ref remarkup, ref html) == true) return true;
                if (ProcessHashLinedHeaderLevel1(database, browser, url, ref remarkup, ref html) == true) return true;
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
            Depth = Int32.Parse(attributes["d"]);
            LineType = (LineTypes)Int32.Parse(attributes["t"]);

            switch (LineType)
            {
                case LineTypes.Hash:
                    return (new string('#', Depth)) + " " + innerText + "\n";

                case LineTypes.Double:
                    if (Depth == 0)
                    {
                        return innerText + "\n" + (new string('=', innerText.Length)) + "\n";
                    }
                    else
                    {
                        return (new string('=', Depth)) + " " + innerText + "\n";
                    }

                case LineTypes.Single:
                    if (Depth == 0)
                    {
                        return innerText + "\n" + (new string('-', innerText.Length)) + "\n";
                    }
                    else
                    {
                        return "- " + innerText + "\n";
                    }
            }

            return "";
        }

        /// <summary>
        /// Adds a soft hyphen character (&shy;) before some specific characters.
        /// This way the browser can correctly wrap some text if the text doesn't fit on 1 line
        /// </summary>
        /// <param name="headerText"></param>
        /// <returns></returns>
        private string FormatHeaderText(string headerText)
        {
            return headerText.Replace("@", "&shy;@");  // for prettier text-wrapping
        }

        /// <summary>
        /// Generates a name for a non-href anchor tag (which is used to for navigating through the table of contents)
        /// </summary>
        /// <param name="headerText"></param>
        /// <returns></returns>
        private string GenerateHeaderName(string headerText)
        {
            // replace all non-alphanumeric characters by dashes (and convert all characters to lowercase characters)
            string result = RegexSafe.Replace(headerText.ToLower(), "[^a-z0-9]", "-", RegexOptions.Singleline);

            // remove all duplicated dashes
            result = RegexSafe.Replace(result, "-+", "-");

            // in case a dash ppears at the end: remove it
            result = result.Trim('-');

            // name should contain all words until the 30th position
            string[] words = result.Split('-');
            result = "";
            foreach (string word in words)
            {
                if (result.Length >= 30) break;

                result += word + "-";
            }

            // remove generated dash at end
            result = result.TrimEnd('-');

            // remember results
            HeaderName = result;
            HeaderNameCopy = TokenList.OfType<RuleHeader>().Count(header => header.HeaderName.Equals(result));

            // if a duplicated header was found -> add counter
            if (HeaderNameCopy >= 1)
            {
                result = result + "-" + HeaderNameCopy;
            }

            return result;
        }

        /// <summary>
        /// Converts a "=" formatted Remarkup code to a H2 tag
        /// </summary>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="browser">Reference to web browser</param>
        /// <param name="url">URL from where this method was executed</param>
        /// <param name="remarkup">Remarkup content to be converted</param>
        /// <param name="html">Translated HTML</param>
        /// <returns>True if a "=" formatted header was found</returns>
        private bool ProcessDoubleLinedHeaderLevel1(Storage.Database database, Browser browser, string url, ref string remarkup, ref string html)
        {
            Match match = RegexSafe.Match(remarkup, "^= *(.+?) *=* *($|[\r\n]+)", RegexOptions.Singleline);
            if (match.Success)
            {
                string headerName = GenerateHeaderName(match.Groups[1].Value);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                ChildTokenList.AddRange(remarkupParserOutput.TokenList);

                html = string.Format("<h2 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h2>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

                LineType = LineTypes.Double;
                Depth = 1;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Converts a "==" formatted Remarkup code to a H3 tag
        /// </summary>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="browser">Reference to web browser</param>
        /// <param name="url">URL from where this method was executed</param>
        /// <param name="remarkup">Remarkup content to be converted</param>
        /// <param name="html">Translated HTML</param>
        /// <returns>True if a "==" formatted header was found</returns>
        private bool ProcessDoubleLinedHeaderLevel2(Storage.Database database, Browser browser, string url, ref string remarkup, ref string html)
        {
            Match match = RegexSafe.Match(remarkup, "^== *(.+?) *=* *($|[\r\n]+)", RegexOptions.Singleline);
            if (match.Success)
            {
                string headerName = GenerateHeaderName(match.Groups[1].Value);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                ChildTokenList.AddRange(remarkupParserOutput.TokenList);

                html = string.Format("<h3 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h3>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

                LineType = LineTypes.Double;
                Depth = 2;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Converts a "===" formatted Remarkup code to a H4 tag
        /// </summary>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="browser">Reference to web browser</param>
        /// <param name="url">URL from where this method was executed</param>
        /// <param name="remarkup">Remarkup content to be converted</param>
        /// <param name="html">Translated HTML</param>
        /// <returns>True if a "===" formatted header was found</returns>
        private bool ProcessDoubleLinedHeaderLevel3(Storage.Database database, Browser browser, string url, ref string remarkup, ref string html)
        {
            Match match = RegexSafe.Match(remarkup, "^=== *(.+?) *=* *($|[\r\n]+)", RegexOptions.Singleline);
            if (match.Success)
            {
                string headerName = GenerateHeaderName(match.Groups[1].Value);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                ChildTokenList.AddRange(remarkupParserOutput.TokenList);

                html = string.Format("<h4 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h4>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

                LineType = LineTypes.Double;
                Depth = 3;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Converts a "====" formatted Remarkup code to a H5 tag
        /// </summary>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="browser">Reference to web browser</param>
        /// <param name="url">URL from where this method was executed</param>
        /// <param name="remarkup">Remarkup content to be converted</param>
        /// <param name="html">Translated HTML</param>
        /// <returns>True if a "====" formatted header was found</returns>
        private bool ProcessDoubleLinedHeaderLevel4(Storage.Database database, Browser browser, string url, ref string remarkup, ref string html)
        {
            Match match = RegexSafe.Match(remarkup, "^==== *(.+?) *=* *($|[\r\n]+)", RegexOptions.Singleline);
            if (match.Success)
            {
                string headerName = GenerateHeaderName(match.Groups[1].Value);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                ChildTokenList.AddRange(remarkupParserOutput.TokenList);

                html = string.Format("<h5 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h5>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

                LineType = LineTypes.Double;
                Depth = 4;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Converts a "=====" formatted Remarkup code to a H6 tag
        /// </summary>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="browser">Reference to web browser</param>
        /// <param name="url">URL from where this method was executed</param>
        /// <param name="remarkup">Remarkup content to be converted</param>
        /// <param name="html">Translated HTML</param>
        /// <returns>True if a "=====" formatted header was found</returns>
        private bool ProcessDoubleLinedHeaderLevel5(Storage.Database database, Browser browser, string url, ref string remarkup, ref string html)
        {
            Match match = RegexSafe.Match(remarkup, "^===== *(.+?) *=* *($|[\r\n]+)", RegexOptions.Singleline);
            if (match.Success)
            {
                string headerName = GenerateHeaderName(match.Groups[1].Value);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                ChildTokenList.AddRange(remarkupParserOutput.TokenList);

                html = string.Format("<h6 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h6>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

                LineType = LineTypes.Double;
                Depth = 5;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Converts a "#" formatted Remarkup code to a H2 tag
        /// </summary>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="browser">Reference to web browser</param>
        /// <param name="url">URL from where this method was executed</param>
        /// <param name="remarkup">Remarkup content to be converted</param>
        /// <param name="html">Translated HTML</param>
        /// <returns>True if a "#" formatted header was found</returns>
        private bool ProcessHashLinedHeaderLevel1(Storage.Database database, Browser browser, string url, ref string remarkup, ref string html)
        {
            Match match = RegexSafe.Match(remarkup, "^# +(.+?) *#* *($|[\r\n]+)", RegexOptions.Singleline);
            if (match.Success)
            {
                string headerName = GenerateHeaderName(match.Groups[1].Value);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                ChildTokenList.AddRange(remarkupParserOutput.TokenList);

                html = string.Format("<h2 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h2>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

                LineType = LineTypes.Hash;
                Depth = 1;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Converts a "##" formatted Remarkup code to a H3 tag
        /// </summary>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="browser">Reference to web browser</param>
        /// <param name="url">URL from where this method was executed</param>
        /// <param name="remarkup">Remarkup content to be converted</param>
        /// <param name="html">Translated HTML</param>
        /// <returns>True if a "##" formatted header was found</returns>
        private bool ProcessHashLinedHeaderLevel2(Storage.Database database, Browser browser, string url, ref string remarkup, ref string html)
        {
            Match match = RegexSafe.Match(remarkup, "^## +(.+?) *#* *($|[\r\n]+)", RegexOptions.Singleline);
            if (match.Success)
            {
                string headerName = GenerateHeaderName(match.Groups[1].Value);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                ChildTokenList.AddRange(remarkupParserOutput.TokenList);

                html = string.Format("<h3 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h3>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

                LineType = LineTypes.Hash;
                Depth = 2;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Converts a "###" formatted Remarkup code to a H4 tag
        /// </summary>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="browser">Reference to web browser</param>
        /// <param name="url">URL from where this method was executed</param>
        /// <param name="remarkup">Remarkup content to be converted</param>
        /// <param name="html">Translated HTML</param>
        /// <returns>True if a "###" formatted header was found</returns>
        private bool ProcessHashLinedHeaderLevel3(Storage.Database database, Browser browser, string url, ref string remarkup, ref string html)
        {
            Match match = RegexSafe.Match(remarkup, "^### +(.+?) *#* *($|[\r\n]+)", RegexOptions.Singleline);
            if (match.Success)
            {
                string headerName = GenerateHeaderName(match.Groups[1].Value);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                ChildTokenList.AddRange(remarkupParserOutput.TokenList);

                html = string.Format("<h4 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h4>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

                LineType = LineTypes.Hash;
                Depth = 3;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Converts a "####" formatted Remarkup code to a H5 tag
        /// </summary>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="browser">Reference to web browser</param>
        /// <param name="url">URL from where this method was executed</param>
        /// <param name="remarkup">Remarkup content to be converted</param>
        /// <param name="html">Translated HTML</param>
        /// <returns>True if a "####" formatted header was found</returns>
        private bool ProcessHashLinedHeaderLevel4(Storage.Database database, Browser browser, string url, ref string remarkup, ref string html)
        {
            Match match = RegexSafe.Match(remarkup, "^#### +(.+?) *#* *($|[\r\n]+)", RegexOptions.Singleline);
            if (match.Success)
            {
                string headerName = GenerateHeaderName(match.Groups[1].Value);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                ChildTokenList.AddRange(remarkupParserOutput.TokenList);

                html = string.Format("<h5 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h5>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

                LineType = LineTypes.Hash;
                Depth = 4;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Converts a "#####" formatted Remarkup code to a H6 tag
        /// </summary>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="browser">Reference to web browser</param>
        /// <param name="url">URL from where this method was executed</param>
        /// <param name="remarkup">Remarkup content to be converted</param>
        /// <param name="html">Translated HTML</param>
        /// <returns>True if a "#####" formatted header was found</returns>
        private bool ProcessHashLinedHeaderLevel5(Storage.Database database, Browser browser, string url, ref string remarkup, ref string html)
        {
            Match match = RegexSafe.Match(remarkup, "^##### +(.+?) *#* *($|[\r\n]+)", RegexOptions.Singleline);
            if (match.Success)
            {
                string headerName = GenerateHeaderName(match.Groups[1].Value);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                ChildTokenList.AddRange(remarkupParserOutput.TokenList);

                html = string.Format("<h6 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h6>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

                LineType = LineTypes.Hash;
                Depth = 5;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Converts a "-" formatted Remarkup code to a H3 tag
        /// </summary>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="browser">Reference to web browser</param>
        /// <param name="url">URL from where this method was executed</param>
        /// <param name="remarkup">Remarkup content to be converted</param>
        /// <param name="html">Translated HTML</param>
        /// <returns>True if a "-" formatted header was found</returns>
        private bool ProcessSingleUnderlinedHeader(Storage.Database database, Browser browser, string url, ref string remarkup, ref string html)
        {
            Match match = RegexSafe.Match(remarkup, "^([^\r\n]+)\r?\n-+ *\r?\n", RegexOptions.Singleline);
            if (match.Success)
            {
                string headerName = GenerateHeaderName(match.Groups[1].Value);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                ChildTokenList.AddRange(remarkupParserOutput.TokenList);

                html = string.Format("<h3 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h3>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

                LineType = LineTypes.Single;
                Depth = 0;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Converts a "=" formatted Remarkup code to a H2 tag
        /// </summary>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="browser">Reference to web browser</param>
        /// <param name="url">URL from where this method was executed</param>
        /// <param name="remarkup">Remarkup content to be converted</param>
        /// <param name="html">Translated HTML</param>
        /// <returns>True if a "-" formatted header was found</returns>
        private bool ProcessDoubleUnderlinedHeader(Storage.Database database, Browser browser, string url, ref string remarkup, ref string html)
        {
            Match match = RegexSafe.Match(remarkup, "^([^\r\n]+)\r?\n=+ *\r?\n", RegexOptions.Singleline);
            if (match.Success)
            {
                string headerName = GenerateHeaderName(match.Groups[1].Value);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                ChildTokenList.AddRange(remarkupParserOutput.TokenList);

                html = string.Format("<h2 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h2>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

                LineType = LineTypes.Double;
                Depth = 0;

                return true;
            }
            return false;
        }
    }
}
