using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Phabricator.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Remarkup parser for code block defined by a line starting with 2 spaces
    /// </summary>
    public class RuleCodeBlockBy2WhiteSpaces : RuleCodeBlock
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

            if (RuleStartOnNewLine)
            {
                Match match = RegexSafe.Match(remarkup, "^(  +(((lang=([^,\n]*))|(name=([^,\n]*))|(lines=([^,\r\n]*))|(counterexample)) *,? *)*)?((  [^\r\n]*|\r?\n)+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (match.Success == false) return false;
                if (match.Value.StartsWith("  ") == false) return false;

                string counterexample = match.Groups[10].Value.Trim('\r').ToLower();
                string codeBlock = match.Groups[11].Value.Trim('\r', '\n');
                string codeBlockName = match.Groups[7].Value.Trim('\r');
                // string codeBlockLines = match.Groups[9].Value.Trim('\r');
                string language = match.Groups[5].Value.Trim('\r');

                if (string.IsNullOrEmpty(codeBlockName) == false)
                {
                    codeBlockName = string.Format("<div class='remarkup-code-header hljs " + counterexample + "'>{0}</div>", HttpUtility.HtmlEncode(codeBlockName));
                }

                string encodedCodeBlock = "";
                foreach (string line in codeBlock.Split('\n').Select(c => c.Trim('\r')))
                {
                    if (line.StartsWith("  "))
                        encodedCodeBlock += System.Web.HttpUtility.HtmlEncode(line.Substring("  ".Length)) + "\n";
                    else
                        encodedCodeBlock += System.Web.HttpUtility.HtmlEncode(line) + "\n";
                }
                encodedCodeBlock = RegexSafe.Replace(encodedCodeBlock, "\n*$", "");  // remove newlines at the end

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

                        if (existingAccount.Parameters.ClipboardCopyForCodeBlock)
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

            return false;
        }
    }
}
