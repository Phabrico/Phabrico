﻿using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Phabricator.Data;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Remarkup parser for code block defined by 3 backticks
    /// </summary>
    [RuleXmlTag("BT")]
    public class RuleCodeBlockBy3BackTicks : RuleCodeBlock
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
            Match match = RegexSafe.Match(remarkup, "^```(((((\\s*lang=([^\n,]*),?)|(\\s*name=([^\n,]*),?)|(\\s*lines=([^\n,]*),?)|(\\s*counterexample\\s*,?))*)?)\r?\n?(.+?(?=(```|$)))(```|$))?( *\r?\n|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (match.Success == false) return false;

            string counterexample = match.Groups[11].Value.Trim(' ', '\r', '\n').ToLower();
            string codeBlock = match.Groups[12].Value;
            string codeBlockName = match.Groups[8].Value.Trim(' ', '\r', '\n');
            // string codeBlockLines = match.Groups[10].Value.Trim('\r');
            string language = match.Groups[6].Value.Trim(' ', '\r', '\n');

            if (string.IsNullOrEmpty(codeBlockName) == false)
            {
                codeBlockName = string.Format("<div class='remarkup-code-header hljs " + counterexample + "'>{0}</div>", HttpUtility.HtmlEncode(codeBlockName));
            }

            string encodedCodeBlock = System.Web.HttpUtility.HtmlEncode(codeBlock);
            encodedCodeBlock = RegexSafe.Replace(encodedCodeBlock, "[\r\n]*$", "");  // remove newlines at the end (in case they exist)

            html = codeBlockName + "<div class='codeblock'>";

            string tokenId = browser.GetCookie("token");
            if (tokenId != null)
            {
                SessionManager.Token token = SessionManager.GetToken(browser);
                string encryptionKey = token?.EncryptionKey;

                if (string.IsNullOrEmpty(encryptionKey) == false)
                {
                    Storage.Account accountStorage = new Storage.Account();
                    Account existingAccount = accountStorage.Get(database, SessionManager.GetToken(browser));

                    if (existingAccount != null && existingAccount.Parameters.ClipboardCopyForCodeBlock)
                    {
                        html += "<button class='codeblock copy'>" + Locale.TranslateText("CodeBlock.Copy", browser.Session.Locale) + "</button>";
                    }
                }
            }

            html += "<pre>";

            if (string.IsNullOrEmpty(language) == false)
            {
                string langCode = GetHighlightJsLanguage(language);

                html += "<code class='" + langCode + " " + counterexample + "'>" + encodedCodeBlock + "</code>";
            }
            else
            {
                html += "<code class='plaintext " + counterexample + "'>" + encodedCodeBlock + "</code>";
            }

            html += "</pre></div>";

            remarkup = remarkup.Substring(match.Length);

            Length = match.Length;

            return true;
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
            return innerText;
        }
    }
}
