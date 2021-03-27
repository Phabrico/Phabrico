using System;
using System.Text.RegularExpressions;

using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Phabricator.Data;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Rmarkup parser for links to Users
    /// it will convert "@User" to a CSS-styled "@User"
    /// </summary>
    public class RuleReferenceUser : RemarkupRule
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
            if (RuleStartAfterWhiteSpace || RuleStartOnNewLine)
            {
                Match match = RegexSafe.Match(remarkup, @"^@([^\r\n\t ]+[ \t]*)", RegexOptions.Singleline);
                if (match.Success == false) return false;

                Match matchUserName = RegexSafe.Match(match.Groups[1].Value, "[a-z0-9._-]*[a-z0-9_-]", RegexOptions.IgnoreCase);
                if (matchUserName.Success == false) return false;

                string tokenId = browser.GetCookie("token");
                if (tokenId != null)
                {
                    SessionManager.Token token = SessionManager.GetToken(browser);
                    string encryptionKey = token?.EncryptionKey;

                    if (string.IsNullOrEmpty(encryptionKey) == false)
                    {
                        Storage.Account accountStorage = new Storage.Account();
                        Storage.User userStorage = new Storage.User();
                        User user = userStorage.Get(database, matchUserName.Value);
                        if (user == null) return false;

                        LinkedPhabricatorObjects.Add(user);

                        html = string.Format("<a class='user-reference' href='/user/info/{0}/'>@{0}</a>",
                                        user.UserName);
                        remarkup = remarkup.Substring(match.Length);

                        Length = match.Length;

                        return true;
                    }
                }
            }

            return false;
        }
    }
}
