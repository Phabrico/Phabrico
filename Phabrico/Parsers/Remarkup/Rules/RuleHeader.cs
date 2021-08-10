using Phabrico.Http;
using Phabrico.Miscellaneous;
using System.Collections.Generic;
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
    public class RuleHeader : RemarkupRule
    {
        List<string> existingHeaderNames;
        RemarkupParserOutput remarkupParserOutput;

        /// <summary>
        /// This method is executed before the ToHTML() is executed.
        /// It is meant for initializing some local variables
        /// </summary>
        public override void Initialize()
        {
            existingHeaderNames = new List<string>();
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
                string headerName = "hdr" + RegexSafe.Replace(match.Groups[1].Value, "[^A-Za-z0-9]", "_", RegexOptions.Singleline);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);

                if (existingHeaderNames.Contains(headerName))
                {
                    int counter = 1;
                    string newHeaderName;
                    do
                    {
                        newHeaderName = string.Format("{0}-{1}", headerName, counter);
                        counter++;
                    }
                    while (existingHeaderNames.Contains(newHeaderName));

                    headerName = newHeaderName;
                }

                existingHeaderNames.Add(headerName);

                html = string.Format("<h2 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h2>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

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
                string headerName = "hdr" + RegexSafe.Replace(match.Groups[1].Value, "[^A-Za-z0-9]", "_", RegexOptions.Singleline);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);

                if (existingHeaderNames.Contains(headerName))
                {
                    int counter = 1;
                    string newHeaderName;
                    do
                    {
                        newHeaderName = string.Format("{0}-{1}", headerName, counter);
                        counter++;
                    }
                    while (existingHeaderNames.Contains(newHeaderName));

                    headerName = newHeaderName;
                }

                existingHeaderNames.Add(headerName);

                html = string.Format("<h3 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h3>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

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
                string headerName = "hdr" + RegexSafe.Replace(match.Groups[1].Value, "[^A-Za-z0-9]", "_", RegexOptions.Singleline);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                
                if (existingHeaderNames.Contains(headerName))
                {
                    int counter = 1;
                    string newHeaderName;
                    do
                    {
                        newHeaderName = string.Format("{0}-{1}", headerName, counter);
                        counter++;
                    }
                    while (existingHeaderNames.Contains(newHeaderName));

                    headerName = newHeaderName;
                }

                existingHeaderNames.Add(headerName);

                html = string.Format("<h4 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h4>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

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
                string headerName = "hdr" + RegexSafe.Replace(match.Groups[1].Value, "[^A-Za-z0-9]", "_", RegexOptions.Singleline);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                
                if (existingHeaderNames.Contains(headerName))
                {
                    int counter = 1;
                    string newHeaderName;
                    do
                    {
                        newHeaderName = string.Format("{0}-{1}", headerName, counter);
                        counter++;
                    }
                    while (existingHeaderNames.Contains(newHeaderName));

                    headerName = newHeaderName;
                }

                existingHeaderNames.Add(headerName);

                html = string.Format("<h5 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h5>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

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
                string headerName = "hdr" + RegexSafe.Replace(match.Groups[1].Value, "[^A-Za-z0-9]", "_", RegexOptions.Singleline);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                
                if (existingHeaderNames.Contains(headerName))
                {
                    int counter = 1;
                    string newHeaderName;
                    do
                    {
                        newHeaderName = string.Format("{0}-{1}", headerName, counter);
                        counter++;
                    }
                    while (existingHeaderNames.Contains(newHeaderName));

                    headerName = newHeaderName;
                }

                existingHeaderNames.Add(headerName);

                html = string.Format("<h6 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h6>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

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
                string headerName = "hdr" + RegexSafe.Replace(match.Groups[1].Value, "[^A-Za-z0-9]", "_", RegexOptions.Singleline);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                
                if (existingHeaderNames.Contains(headerName))
                {
                    int counter = 1;
                    string newHeaderName;
                    do
                    {
                        newHeaderName = string.Format("{0}-{1}", headerName, counter);
                        counter++;
                    }
                    while (existingHeaderNames.Contains(newHeaderName));

                    headerName = newHeaderName;
                }

                existingHeaderNames.Add(headerName);

                html = string.Format("<h2 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h2>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

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
                string headerName = "hdr" + RegexSafe.Replace(match.Groups[1].Value, "[^A-Za-z0-9]", "_", RegexOptions.Singleline);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                
                if (existingHeaderNames.Contains(headerName))
                {
                    int counter = 1;
                    string newHeaderName;
                    do
                    {
                        newHeaderName = string.Format("{0}-{1}", headerName, counter);
                        counter++;
                    }
                    while (existingHeaderNames.Contains(newHeaderName));

                    headerName = newHeaderName;
                }

                existingHeaderNames.Add(headerName);

                html = string.Format("<h3 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h3>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

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
                string headerName = "hdr" + RegexSafe.Replace(match.Groups[1].Value, "[^A-Za-z0-9]", "_", RegexOptions.Singleline);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                
                if (existingHeaderNames.Contains(headerName))
                {
                    int counter = 1;
                    string newHeaderName;
                    do
                    {
                        newHeaderName = string.Format("{0}-{1}", headerName, counter);
                        counter++;
                    }
                    while (existingHeaderNames.Contains(newHeaderName));

                    headerName = newHeaderName;
                }

                existingHeaderNames.Add(headerName);

                html = string.Format("<h4 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h4>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

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
                string headerName = "hdr" + RegexSafe.Replace(match.Groups[1].Value, "[^A-Za-z0-9]", "_", RegexOptions.Singleline);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                
                if (existingHeaderNames.Contains(headerName))
                {
                    int counter = 1;
                    string newHeaderName;
                    do
                    {
                        newHeaderName = string.Format("{0}-{1}", headerName, counter);
                        counter++;
                    }
                    while (existingHeaderNames.Contains(newHeaderName));

                    headerName = newHeaderName;
                }

                existingHeaderNames.Add(headerName);

                html = string.Format("<h5 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h5>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

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
                string headerName = "hdr" + RegexSafe.Replace(match.Groups[1].Value, "[^A-Za-z0-9]", "_", RegexOptions.Singleline);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                
                if (existingHeaderNames.Contains(headerName))
                {
                    int counter = 1;
                    string newHeaderName;
                    do
                    {
                        newHeaderName = string.Format("{0}-{1}", headerName, counter);
                        counter++;
                    }
                    while (existingHeaderNames.Contains(newHeaderName));

                    headerName = newHeaderName;
                }

                existingHeaderNames.Add(headerName);

                html = string.Format("<h6 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h6>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

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
                string headerName = "hdr" + RegexSafe.Replace(match.Groups[1].Value, "[^A-Za-z0-9]", "_", RegexOptions.Singleline);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                
                if (existingHeaderNames.Contains(headerName))
                {
                    int counter = 1;
                    string newHeaderName;
                    do
                    {
                        newHeaderName = string.Format("{0}-{1}", headerName, counter);
                        counter++;
                    }
                    while (existingHeaderNames.Contains(newHeaderName));

                    headerName = newHeaderName;
                }

                existingHeaderNames.Add(headerName);

                html = string.Format("<h3 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h3>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

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
                string headerName = "hdr" + RegexSafe.Replace(match.Groups[1].Value, "[^A-Za-z0-9]", "_", RegexOptions.Singleline);
                string headerText = Engine.ToHTML(this, database, browser, url, match.Groups[1].Value, out remarkupParserOutput, false);
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                
                if (existingHeaderNames.Contains(headerName))
                {
                    int counter = 1;
                    string newHeaderName;
                    do
                    {
                        newHeaderName = string.Format("{0}-{1}", headerName, counter);
                        counter++;
                    }
                    while (existingHeaderNames.Contains(newHeaderName));

                    headerName = newHeaderName;
                }

                existingHeaderNames.Add(headerName);

                html = string.Format("<h2 class='remarkup-header'><a name='{0}' style='padding-top: 80px;'></a>{1} </h2>", headerName, FormatHeaderText(headerText));
                remarkup = remarkup.Substring(match.Length);

                Length = match.Length;

                return true;
            }
            return false;
        }
    }
}
