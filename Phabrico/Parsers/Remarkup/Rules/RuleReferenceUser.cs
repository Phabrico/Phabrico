using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Phabricator.Data;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Rmarkup parser for links to Users
    /// it will convert "@User" to a CSS-styled "@User"
    /// </summary>
    [RuleXmlTag("US")]
    public class RuleReferenceUser : RemarkupRule
    {
        public string UserName { get; private set; }
        public string UserToken { get; private set; }

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

                string encryptionKey = browser.Token?.EncryptionKey;

                if (string.IsNullOrEmpty(encryptionKey) == false)
                {
                    Storage.Account accountStorage = new Storage.Account();
                    Storage.User userStorage = new Storage.User();
                    User user = userStorage.Get(database, matchUserName.Value, browser.Session.Locale);
                    if (user == null) return false;

                    LinkedPhabricatorObjects.Add(user);

                    if (browser.HttpServer.Customization.HideManiphest || browser.HttpServer.Customization.HideUsers)
                    {
                        html = string.Format("<span class='user-reference'>{0}</span>",
                                        System.Web.HttpUtility.HtmlEncode(user.RealName));
                    }
                    else
                    {
                        html = string.Format("<a class='user-reference' href='user/info/{0}/'>@{0}</a>",
                                        System.Web.HttpUtility.HtmlEncode(user.UserName));
                    }

                    Length = match.Length;
                    if (match.Value.EndsWith(" ") || match.Value.EndsWith("\t"))
                    {
                        Length--;
                    }

                    remarkup = remarkup.Substring(Length);

                    UserName = user.UserName;
                    UserToken = user.Token;

                    return true;
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
            return innerText;
        }

        /// <summary>
        /// Deep copy clone
        /// </summary>
        /// <param name="originalRemarkupRule"></param>
        public override void Clone(RemarkupRule originalRemarkupRule)
        {
            RuleReferenceUser originalRuleReferenceUser = originalRemarkupRule as RuleReferenceUser;
            UserName = originalRuleReferenceUser.UserName;
            UserToken = originalRuleReferenceUser.UserToken;
        }
    }
}
