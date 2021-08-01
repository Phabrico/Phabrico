﻿using System;
using System.Text.RegularExpressions;

using Phabrico.Http;
using Phabrico.Miscellaneous;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Rmarkup parser for Macro objects
    /// </summary>
    [RulePriority(-110)]
    public class RuleMacro : RemarkupRule
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
                Match match = RegexSafe.Match(remarkup, @"^ *([a-z0-9:_-][a-z0-9:_-][a-z0-9:_-]+) *[\r\n][\r\n]?", RegexOptions.Singleline);
                if (match.Success == false) return false;

                string macroName = match.Groups[1].Value;

                Storage.File fileStorage = new Storage.File();

                string tokenId = browser.GetCookie("token");
                if (tokenId != null)
                {
                    SessionManager.Token token = SessionManager.GetToken(browser);
                    string encryptionKey = token?.EncryptionKey;

                    if (string.IsNullOrEmpty(encryptionKey) == false)
                    {
                        Storage.Account accountStorage = new Storage.Account();
                            Phabricator.Data.File fileObject = fileStorage.Get(database, macroName);
                        if (fileObject != null)
                        {
                            LinkedPhabricatorObjects.Add(fileObject);

                            html = string.Format(@"<img alt='{0}' src='file/data/{1}/'>", fileObject.FileName.Replace("'", ""), fileObject.ID);
                            remarkup = "\r\n" + remarkup.Substring(match.Length);

                            Length = match.Length;

                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
