using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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
    [RuleXmlTag("NT")]
    public class RuleNotification : RemarkupRule
    {
        public enum NotificationStyle
        {
            Important,
            Note,
            Warning
        }

        public NotificationStyle Style { get; private set; }
        public bool HideNotificationPrefix { get; private set; }

        /// <summary>
        /// Deeper cloner
        /// </summary>
        /// <param name="originalRemarkupRule"></param>
        public override RemarkupRule Clone()
        {
            RuleNotification copy = base.Clone() as RuleNotification;
            if (copy != null)
            {
                copy.Style = Style;
                copy.HideNotificationPrefix = HideNotificationPrefix;
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

            if (RuleStartOnNewLine == false) return false;

            RemarkupParserOutput remarkupParserOutput;
            Match match = RegexSafe.Match(remarkup, @"^(IMPORTANT|NOTE|WARNING):(\r?\n)?(.+?(?=\n\r?\n|$))", RegexOptions.Singleline);
            if (match.Success)
            {
                string notificationText;
                string notificationType = match.Groups[1].Value.ToLower();

                HideNotificationPrefix = false;

                switch (notificationType)
                {
                    case "important":
                        Style = NotificationStyle.Important;
                        break;

                    case "note":
                        Style = NotificationStyle.Note;
                        break;

                    default:
                        Style = NotificationStyle.Warning;
                        break;
                }

                if (NotificationTextShouldBeTranslated(database, browser))
                {
                    notificationText = Locale.TranslateText("Notification." + notificationType.ToUpper(), browser.Session.Locale);
                }
                else
                {
                    notificationText = notificationType.ToUpper();
                }

                remarkup = remarkup.Substring(match.Length);
                html = string.Format("<div class='remarkup-notification {0}'><span class='remarkup-note-word'>{1}:</span> {2}</div>", 
                    notificationType, 
                    notificationText, 
                    Engine.ToHTML(this, database, browser, url, match.Groups[3].Value.Trim(' ', '\r'), out remarkupParserOutput, false, PhabricatorObjectToken)
                );
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                ChildTokenList.AddRange(remarkupParserOutput.TokenList);

                Length = match.Length;

                return true;
            }

            match = RegexSafe.Match(remarkup, @"^\((IMPORTANT|NOTE|WARNING)\)(\r?\n(\r|$))?(.+?(?=\n(\r?\n|$)))", RegexOptions.Singleline);
            if (match.Success)
            {
                string notificationType = match.Groups[1].Value.ToLower();
                string content = match.Groups[4].Value.Trim(' ', '\r');

                HideNotificationPrefix = true;

                switch (notificationType)
                {
                    case "important":
                        Style = NotificationStyle.Important;
                        break;

                    case "note":
                        Style = NotificationStyle.Note;
                        break;

                    default:
                        Style = NotificationStyle.Warning;
                        break;
                }

                remarkup = remarkup.Substring(match.Length);
                html = string.Format("<div class='remarkup-notification {0}'>{1}</div>", notificationType, Engine.ToHTML(this, database, browser, url, content, out remarkupParserOutput, false, PhabricatorObjectToken));
                LinkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);

                ChildTokenList.AddRange(remarkupParserOutput.TokenList);
                Length = match.Length;

                return true;
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
            bool showPrefix = attributes["p"] == "1";
            string command;
            if (attributes.TryGetValue("t", out command))
            {
                if (command == "w")
                {
                    command = "WARNING";
                }
                else
                if (command == "i")
                {
                    command = "IMPORTANT";
                }
            }
            else
            {
                command = "NOTE";
            }

            if (showPrefix)
            {
                return command + ": " + innerText;
            }
            else
            {
                return "(" + command + ") " + innerText;
            }
        }

        /// <summary>
        /// Returns true if the configuration parameter for translating Notification Titles is set to true
        /// </summary>
        /// <param name="browser"></param>
        /// <returns></returns>
        private bool NotificationTextShouldBeTranslated(Storage.Database database, Browser browser)
        {
            Storage.Account accountStorage = new Storage.Account();
            
            Phabricator.Data.Account account = accountStorage.WhoAmI(database, browser);
            if (account == null)
            {
                return false;
            }
            else
            {
                return account.Parameters.UITranslation;
            }
        }
    }
}
