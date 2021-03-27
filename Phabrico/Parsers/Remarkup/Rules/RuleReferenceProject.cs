﻿using System;
using System.Text.RegularExpressions;

using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Phabricator.Data;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Rmarkup parser for links to Projects
    /// it will convert "#Project" to a CSS-styled Project tag
    /// </summary>
    public class RuleReferenceProject : RemarkupRule
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
                Match match = RegexSafe.Match(remarkup, @"^#([^\r\n\t ]+[ \t]*)", RegexOptions.Singleline);
                if (match.Success == false) return false;

                Match matchProjectName = RegexSafe.Match(match.Groups[1].Value, "[a-z0-9._-]*[a-z0-9_-]", RegexOptions.IgnoreCase);
                if (matchProjectName.Success == false) return false;

                string tokenId = browser.GetCookie("token");
                if (tokenId != null)
                {
                    SessionManager.Token token = SessionManager.GetToken(browser);
                    string encryptionKey = token?.EncryptionKey;

                    if (string.IsNullOrEmpty(encryptionKey) == false)
                    {
                        Storage.Account accountStorage = new Storage.Account();
                        Storage.Project projectStorage = new Storage.Project();
                        Project project = projectStorage.Get(database, matchProjectName.Value);
                        if (project == null) return false;

                        LinkedPhabricatorObjects.Add(project);

                        string rgbColor = "rgb(0, 128, 255)";
                        if (project != null && string.IsNullOrWhiteSpace(project.Color) == false)
                        {
                            rgbColor = project.Color;
                        }

                        string style = string.Format("background: {0}; color: {1}; border-color: {1}",
                                            rgbColor,
                                            ColorFunctionality.WhiteOrBlackTextOnBackground(rgbColor));

                        html = string.Format("<a class='project-reference phui-icon-view phui-font-fa fa-briefcase' href='/project/info/{0}/' style='{1}'>{2}</a>",
                                        project.InternalName,
                                        style,
                                        project.Name);
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
