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
    /// Rmarkup parser for links to Maniphest Tasks
    /// it will convert "{Txxx}" to a maniphest-task anchor link; the phriction anchor link will be corrected below into a maniphest link
    /// </summary>
    [RulePriority(-105)]
    [RuleXmlTag("TS")]
    public class RuleReferenceManiphestTask : RemarkupRule
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
            Match match = RegexSafe.Match(remarkup, @"^({?)T(-?[0-9]+)(}?)", RegexOptions.Singleline);
            if (match.Success == false) return false;

            bool useBeginCurlyBracket = string.IsNullOrEmpty(match.Groups[1].Value) == false;
            string taskIdentifier = match.Groups[2].Value;
            bool useEndCurlyBracket = string.IsNullOrEmpty(match.Groups[3].Value) == false;

            if (useBeginCurlyBracket != useEndCurlyBracket) return false;

            Storage.Maniphest maniphestStorage = new Storage.Maniphest();
            Storage.Stage stageStorage = new Storage.Stage();

            string tokenId = browser.GetCookie("token");
            if (tokenId != null)
            {
                SessionManager.Token token = SessionManager.GetToken(browser);
                string encryptionKey = token?.EncryptionKey;
                if (string.IsNullOrEmpty(encryptionKey) == false)
                {
                    Storage.Account accountStorage = new Storage.Account();
                        string taskID = Int32.Parse(taskIdentifier).ToString();

                        Phabricator.Data.Maniphest maniphestTask = stageStorage.Get<Phabricator.Data.Maniphest>(database, browser.Session.Locale)
                                                                               .FirstOrDefault(stagedTask => stagedTask.ID.Equals(taskID));
                        if (maniphestTask == null)
                        {
                            maniphestTask = maniphestStorage.Get(database, taskID, browser.Session.Locale);
                            if (maniphestTask == null) return false;
                        }

                        LinkedPhabricatorObjects.Add(maniphestTask);

                    if (useBeginCurlyBracket)
                    {
                        html = string.Format("<a class='maniphest-link {0}' href='/maniphest/T{1}/'>T{1}: {2}</a>",
                                        maniphestTask.IsOpen ? "" : "closed",
                                        maniphestTask.ID,
                                        System.Web.HttpUtility.HtmlEncode(maniphestTask.Name));
                        remarkup = remarkup.Substring(match.Length);
                    }
                    else
                    {
                        html = string.Format("<a class='maniphest-link {0}' href='/maniphest/T{1}/'>T{1}</a>",
                                        maniphestTask.IsOpen ? "" : "closed",
                                        maniphestTask.ID);
                        remarkup = remarkup.Substring(match.Length);
                    }

                    Length = match.Length;

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
        internal override string ConvertXmlToRemarkup(Database database, Browser browser, string innerText, Dictionary<string, string> attributes)
        {
            return innerText;
        }
    }
}
