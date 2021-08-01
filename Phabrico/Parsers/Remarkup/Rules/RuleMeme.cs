using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Phabrico.Http;
using Phabrico.Miscellaneous;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Rmarkup parser for Meme objects
    /// </summary>
    public class RuleMeme : RemarkupRule
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
                Match match = RegexSafe.Match(remarkup, @"^{meme,([^}]*)}", RegexOptions.Singleline);
                if (match.Success == false) return false;

                Dictionary<string, string> memeOptions = match.Groups[1]
                                                              .Value
                                                              .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                              .ToDictionary(key => key.Split('=')
                                                                                      .FirstOrDefault()
                                                                                      .Trim()
                                                                                      .ToLower(),
                                                                          value => value.Contains('=') ? value.Split('=')[1] : "");

                KeyValuePair<string,string> macroNameProperty = memeOptions.FirstOrDefault(parameter => parameter.Key.Equals("src", StringComparison.OrdinalIgnoreCase));
                if (macroNameProperty.Key == null) return false;

                Storage.File fileStorage = new Storage.File();
                string tokenId = browser.GetCookie("token");
                if (tokenId != null)
                {
                    SessionManager.Token token = SessionManager.GetToken(browser);
                    string encryptionKey = token?.EncryptionKey;

                    if (string.IsNullOrEmpty(encryptionKey) == false)
                    {
                        Storage.Account accountStorage = new Storage.Account();
                        Phabricator.Data.File fileObject = fileStorage.Get(database, macroNameProperty.Value);
                        if (fileObject == null) return false;

                        string textAbove = null, textBelow = null;
                        memeOptions.TryGetValue("above", out textAbove);
                        memeOptions.TryGetValue("below", out textBelow);

                        LinkedPhabricatorObjects.Add(fileObject);

                        html = "<div>";
                        if (string.IsNullOrWhiteSpace(textAbove) == false)
                        {
                            html += string.Format("<span class='meme-above' style='width:{0}px'>{1}</span>", fileObject.ImagePropertyPixelWidth, textAbove);
                        }
                        html += string.Format(@"<img alt='{0}' src='file/data/{1}/'>", fileObject.FileName.Replace("'", ""), fileObject.ID);
                        if (string.IsNullOrWhiteSpace(textBelow) == false)
                        {
                            int positionTextBelow = -38;
                            positionTextBelow -= textBelow.Count(ch => ch == '\n') * 18;
                            html += string.Format("<span class='meme-below' style='width:{0}px; margin-top:{1}px;'>{2}</span>", fileObject.ImagePropertyPixelWidth, positionTextBelow, textBelow);
                        }
                        html += "<div>";
                        remarkup = "\r\n" + remarkup.Substring(match.Length);

                        Length = match.Length;

                        return true;
                    }
                }
            }

            return false;
        }
    }
}
