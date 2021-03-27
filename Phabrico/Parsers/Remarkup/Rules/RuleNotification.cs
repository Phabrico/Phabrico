using System;
using System.Text.RegularExpressions;

using Phabrico.Http;
using Phabrico.Miscellaneous;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Remarkup parser for IMPORTANT, NOTE and WARNING text blocks
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
    public class RuleNotification : RemarkupRule
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

            if (RuleStartOnNewLine == false) return false;

            RemarkupParserOutput remarkupParserOutput;
            Match match = RegexSafe.Match(remarkup, @"^(IMPORTANT|NOTE|WARNING):(\r?\n)?(.+?(?=\n\r?\n|$))", RegexOptions.Singleline);
            if (match.Success)
            {
                string notificationText;
                string notificationType = match.Groups[1].Value.ToLower();


                if (NotificationTextShouldBeTranslated(browser))
                {
                    notificationText = Locale.TranslateText("Notification." + notificationType.ToUpper(), browser.Session.Locale);
                }
                else
                {
                    notificationText = notificationType.ToUpper();
                }

                remarkup = remarkup.Substring(match.Length);
                html = string.Format("<div class='remarkup-{0}'><span class='remarkup-note-word'>{1}:</span> {2}</div>", notificationType, notificationText, Engine.ToHTML(this, database, browser, url, match.Groups[3].Value.Trim(' ', '\r'), out remarkupParserOutput, false));
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);

                Length = match.Length;

                return true;
            }

            match = RegexSafe.Match(remarkup, @"^\((IMPORTANT|NOTE|WARNING)\)(\r?\n(\r|$))?(.+?(?=\n(\r?\n|$)))", RegexOptions.Singleline);
            if (match.Success)
            {
                string notificationType = match.Groups[1].Value.ToLower();
                string content = match.Groups[4].Value.Trim(' ', '\r');

                remarkup = remarkup.Substring(match.Length);
                html = string.Format("<div class='remarkup-{0}'>{1}</div>", notificationType, Engine.ToHTML(this, database, browser, url, content, out remarkupParserOutput, false));
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);

                Length = match.Length;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if the configuration parameter for translating Notification Titles is set to true
        /// </summary>
        /// <param name="browser"></param>
        /// <returns></returns>
        private bool NotificationTextShouldBeTranslated(Browser browser)
        {
            Storage.Account accountStorage = new Storage.Account();

            SessionManager.Token token = SessionManager.GetToken(browser);
            string encryptionKey = token?.EncryptionKey;

            if (string.IsNullOrEmpty(encryptionKey) == false)
            {
                using (Storage.Database database = new Storage.Database(null))
                {
                    UInt64[] publicXorCipher = accountStorage.GetPublicXorCipher(database, token);

                    // unmask encryption key
                    encryptionKey = Encryption.XorString(encryptionKey, publicXorCipher);
                }

                using (Storage.Database database = new Storage.Database(encryptionKey))
                {
                    // unmask private encryption key
                    if (token.PrivateEncryptionKey != null)
                    {
                        UInt64[] privateXorCipher = accountStorage.GetPrivateXorCipher(database, token);
                        database.PrivateEncryptionKey = Encryption.XorString(token.PrivateEncryptionKey, privateXorCipher);
                    }

                    Phabricator.Data.Account account = accountStorage.Get(database, SessionManager.GetToken(browser));
                    return account.Parameters.UITranslation;
                }
            }

            return false;
        }
    }
}
