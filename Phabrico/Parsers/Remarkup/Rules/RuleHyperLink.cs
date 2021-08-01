﻿using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

using Phabrico.Http;
using Phabrico.Miscellaneous;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Remarkup parser for hyperlinks
    /// </summary>
    [RulePriority(60)]
    public class RuleHyperLink : RemarkupRule
    {
        private bool bannedLinkedPhrictionDocument;
        private bool inexistantLinkedPhrictionDocument;
        private Phabricator.Data.Phriction linkedPhrictionDocument;

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

            bannedLinkedPhrictionDocument = false;
            inexistantLinkedPhrictionDocument = false;
            linkedPhrictionDocument = null;

            try
            {
                Match match = RegexSafe.Match(remarkup, @"^(https?|ftp)://[A-Za-z0-9._~:/?#[\]@!$&'()*,;%=+-]+", RegexOptions.Singleline);
                if (match.Success)
                {
                    string urlHyperlink = match.Value;

                    // url should not end with some specific characters
                    Match matchInvalidEndCharacters = RegexSafe.Match(urlHyperlink, "[,;!:.?]+$", RegexOptions.Singleline);
                    if (matchInvalidEndCharacters.Success)
                    {
                        urlHyperlink = urlHyperlink.Substring(0, matchInvalidEndCharacters.Index);
                    }

                    string urlHyperlinkText = "";
                    if (InvalidUrl(browser, url, ref urlHyperlink, ref urlHyperlinkText))
                    {
                        html = HttpUtility.HtmlEncode(urlHyperlink);
                    }
                    else
                    {
                        html = string.Format("<a class='phriction-link' href='{0}'>{0}</a>", urlHyperlink);
                    }

                    remarkup = remarkup.Substring(urlHyperlink.Length);

                    Length = match.Length;

                    return true;
                }

                match = RegexSafe.Match(remarkup, @"^\[([^]]+)\]\(( *(https?|ftp)://[^)]+) *\)", RegexOptions.Singleline);
                if (match.Success)
                {
                    remarkup = remarkup.Substring(match.Length);
                    html = string.Format("<a class='phriction-link' href='{1}'>{0}</a>", match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim());

                    Length = match.Length;

                    return true;
                }

                match = RegexSafe.Match(remarkup, @"^\[([^]]+)\]\(( *mailto:[^)]+) *\)", RegexOptions.Singleline);
                if (match.Success)
                {
                    remarkup = remarkup.Substring(match.Length);
                    html = string.Format("<a class='email-link' href='{1}'>{0}</a>", match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim());

                    Length = match.Length;

                    return true;
                }

                match = RegexSafe.Match(remarkup, @"^((?=.{0,64}@.{0,255}$)(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*|""(?:[\x01-\x08\x0b\x0c\x0e-\x1f\x21\x23-\x5b\x5d-\x7f]|\\[\x01-\x09\x0b\x0c\x0e-\x7f])*""))@(?:(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?|\[(?:(?:(2(5[0-5]|[0-4][0-9])|1[0-9][0-9]|[1-9]?[0-9]))\.){3}(?:(2(5[0-5]|[0-4][0-9])|1[0-9][0-9]|[1-9]?[0-9])|[a-z0-9-]*[a-z0-9]:(?:[\x01-\x08\x0b\x0c\x0e-\x1f\x21-\x5a\x53-\x7f]|\\[\x01-\x09\x0b\x0c\x0e-\x7f])+)\])", RegexOptions.Singleline);
                if (match.Success)
                {
                    remarkup = remarkup.Substring(match.Length);
                    html = string.Format("<a class='email-link' href='mailto:{0}'>{0}</a>", match.Value);

                    Length = match.Length;

                    return true;
                }

                match = RegexSafe.Match(remarkup, @"^\[([^]]+)\]\(( *tel:[^)]+) *\)", RegexOptions.Singleline);
                if (match.Success)
                {
                    remarkup = remarkup.Substring(match.Length);
                    html = string.Format("<a class='phone-link' href='{1}'>{0}</a>", match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim());

                    Length = match.Length;

                    return true;
                }

                match = RegexSafe.Match(remarkup, @"^<((https?|ftp)://[A-Za-z0-9._~:/?#[\]@!$&'()*,;%=+]+)>", RegexOptions.Singleline);
                if (match.Success)
                {
                    string hyperlink = match.Groups[1].Value;
                    remarkup = remarkup.Substring(match.Length);

                    string hyperlinkText = "";
                    if (InvalidUrl(browser, url, ref hyperlink, ref hyperlinkText))
                    {
                        html = HttpUtility.HtmlEncode(match.Value);
                    }
                    else
                    {
                        html = string.Format("<a class='phriction-link' href='{0}'>{0}</a>", hyperlink);
                    }

                    Length = match.Length;

                    return true;
                }

                match = RegexSafe.Match(remarkup, @"^\[\[ *(.+?(?= *\]\])) *\]\]", RegexOptions.Singleline);
                if (match.Success)
                {
                    string[] urlHyperlinkParts = match.Groups[1].Value.Split('|');
                    string urlHyperlink, urlHyperlinkText;
                    if (urlHyperlinkParts.Length > 1)
                    {
                        urlHyperlink = urlHyperlinkParts[0].Trim();
                        urlHyperlinkText = string.Join("|", urlHyperlinkParts.Skip(1)).Trim();
                    }
                    else
                    {
                        urlHyperlink = urlHyperlinkParts[0].Trim();
                        urlHyperlinkText = null;
                    }

                    if (urlHyperlink.StartsWith("mailto:"))
                    {
                        urlHyperlink = urlHyperlink.Substring("mailto:".Length);
                        if (urlHyperlinkText == null) urlHyperlinkText = urlHyperlink;

                        html = string.Format("<a class='email-link' href='mailto:{0}'>{1}</a>", urlHyperlink, urlHyperlinkText);
                    }
                    else
                    if (urlHyperlink.StartsWith("tel:"))
                    {
                        urlHyperlink = urlHyperlink.Substring("tel:".Length);
                        if (urlHyperlinkText == null) urlHyperlinkText = urlHyperlink;

                        html = string.Format("<a class='phone-link' href='tel:{0}'>{1}</a>", urlHyperlink, urlHyperlinkText);
                    }
                    else
                    if (urlHyperlink.StartsWith("ftp://"))
                    {
                        urlHyperlink = urlHyperlink.Substring("ftp://".Length);
                        if (urlHyperlinkText == null) urlHyperlinkText = urlHyperlink;

                        html = string.Format("<a class='phriction-link' href='{0}'>{1}</a>", urlHyperlink, urlHyperlinkText);
                    }
                    else
                    {
                        if (urlHyperlink.StartsWith("."))
                        {
                            string absoluteUrl = url.Replace("//", "/");
                            while (true)
                            {
                                if (urlHyperlink.StartsWith("./"))
                                {
                                    urlHyperlink = urlHyperlink.Substring(2);
                                    if (urlHyperlinkText != null && urlHyperlinkText.StartsWith("./"))
                                    {
                                        urlHyperlinkText = urlHyperlinkText.Substring(2);
                                    }
                                    continue;
                                }
                                else
                                if (urlHyperlink.StartsWith("../"))
                                {
                                    string[] absoluteUrlParts = absoluteUrl.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                                    absoluteUrl = string.Join("/", absoluteUrlParts.Take(absoluteUrlParts.Any() ? absoluteUrlParts.Length - 1 : 0));
                                    absoluteUrl = absoluteUrl + "/";

                                    urlHyperlink = urlHyperlink.Substring(3);
                                    if (urlHyperlinkParts.Length != 2 && urlHyperlinkText != null && urlHyperlinkText.StartsWith("../"))
                                    {
                                        urlHyperlinkText = urlHyperlinkText.Substring(3);
                                    }
                                    continue;
                                }
                                else
                                {
                                    absoluteUrl = "/" + absoluteUrl + urlHyperlink;
                                    absoluteUrl = absoluteUrl.Replace("//", "/");
                                    break;
                                }
                            }

                            urlHyperlink = absoluteUrl;
                        }

                        if (urlHyperlink.StartsWith("http://") == false && urlHyperlink.StartsWith("https://") == false)
                        {
                            string encryptionKey = browser.Token?.EncryptionKey;
                            if (string.IsNullOrEmpty(encryptionKey) == false)
                            {
                                Storage.Account accountStorage = new Storage.Account();
                                Phabrico.Storage.Phriction phrictionStorage = new Storage.Phriction();

                                if (urlHyperlink.StartsWith("/")) urlHyperlink = urlHyperlink.Substring(1);
                                string linkedDocument = urlHyperlink.Split('#')[0];

                                if (linkedDocument.StartsWith("w/")) linkedDocument = linkedDocument.Substring("w/".Length - 1);
                                else
                                if (linkedDocument.StartsWith("phriction/")) linkedDocument = linkedDocument.Substring("phriction/".Length - 1);

                                // check if we have an hyperlink, wrongly formatted as a Windows file path, but somehow allowed by Phabricator
                                // e.g. [[ Q:\blabla\document.docx | My document ]]
                                if (RegexSafe.IsMatch(linkedDocument, @"[A-Za-z]:\.*", RegexOptions.None))
                                {
                                    linkedDocument = linkedDocument.Substring(3)
                                                                   .Replace("\\", "_")
                                                                   .ToLower();
                                    urlHyperlink = linkedDocument;
                                }

                                // replace invalid characters in url by underscores
                                linkedDocument = RegexSafe.Replace(linkedDocument, "[ ?]", "_");

                                // trim linkedDocument
                                linkedDocument = linkedDocument.TrimEnd('_');

                                linkedPhrictionDocument = phrictionStorage.Get(database, linkedDocument);
                                if (linkedPhrictionDocument == null)
                                {
                                    Storage.Stage stageStorage = new Storage.Stage();
                                    linkedPhrictionDocument = stageStorage.Get<Phabricator.Data.Phriction>(database).FirstOrDefault(doc => doc.Path.Equals(linkedDocument));
                                }

                                if (linkedPhrictionDocument != null)
                                {
                                    LinkedPhabricatorObjects.Add(linkedPhrictionDocument);
                                    urlHyperlink = "w/" + urlHyperlink;

                                    if (urlHyperlinkText == null)
                                    {
                                        urlHyperlinkText = linkedPhrictionDocument.Name;
                                    }
                                }

                                if (linkedPhrictionDocument == null)
                                {
                                    // check if linked document was banned
                                    Storage.BannedObject bannedObjectStorage = new Storage.BannedObject();
                                    bannedLinkedPhrictionDocument = bannedObjectStorage.Exists(database, urlHyperlink, ref urlHyperlinkText);
                                }

                                // in case linked document has no title, correct generated title
                                if (urlHyperlinkText == null)
                                {
                                    urlHyperlinkText = urlHyperlink;
                                    while (urlHyperlinkText.EndsWith("/"))
                                    {
                                        urlHyperlinkText = urlHyperlinkText.Substring(0, urlHyperlinkText.Length - 1);
                                    }

                                    urlHyperlinkText = urlHyperlinkText.Split('/').LastOrDefault();
                                }
                            }
                        }

                        if (InvalidUrl(browser, url, ref urlHyperlink, ref urlHyperlinkText))
                        {
                            if (urlHyperlinkText == null)
                            {
                                urlHyperlinkText = urlHyperlink;
                            }

                            html = GenerateInvalidUrlError(database, url, urlHyperlink, urlHyperlinkText);
                        }
                        else
                        {
                            if (urlHyperlinkText == null)
                            {
                                urlHyperlinkText = urlHyperlink;
                            }

                            if (bannedLinkedPhrictionDocument)
                            {
                                string absolutePathToPhabricator = "";
                                SessionManager.Token token = SessionManager.GetToken(browser);
                                string encryptionKey = token?.EncryptionKey;
                                if (string.IsNullOrEmpty(encryptionKey) == false)
                                {
                                    Storage.Account accountStorage = new Storage.Account();
                                    Phabricator.Data.Account accountData = accountStorage.Get(database, token);
                                    absolutePathToPhabricator = accountData.PhabricatorUrl.TrimEnd('/') + "/" + urlHyperlink;
                                }

                                html = string.Format("<a class='phriction-link banned' href='{0}' title=\"{1}\">{2}</a>",
                                                absolutePathToPhabricator,
                                                Locale.TranslateText("This Phriction document or Maniphest task wasn't downloaded from the Phabricator server because it was subscribed by one or more disallowed projects or users.", browser.Session.Locale),
                                                urlHyperlinkText);
                            }
                            else
                            if (inexistantLinkedPhrictionDocument)
                            {
                                html = string.Format("<a class='phriction-link banned' href='{0}'>{1}</a>", urlHyperlink, urlHyperlinkText);
                            }
                            else
                            {
                                html = string.Format("<a class='phriction-link' href='{0}'>{1}</a>", urlHyperlink, urlHyperlinkText);
                            }
                        }
                    }

                    remarkup = remarkup.Substring(match.Length);

                    Length = match.Length;
                    return true;
                }

                return false;
            }
            finally
            {
                // check if hyperlink was found
                if (html.Any())
                {
                    // check if href contains a fixed path to the Phabricator server
                    Match hrefAbsolutePath = RegexSafe.Match(html, "href='(https?://[^']*)'", RegexOptions.None);
                    if (hrefAbsolutePath.Success)
                    {
                        Http.SessionManager.Token token = SessionManager.GetToken(browser);
                        string encryptionKey = token?.EncryptionKey;

                        Storage.Account accountStorage = new Storage.Account();
                        Phabricator.Data.Account accountData = accountStorage.WhoAmI(database);
                        if (accountData == null) throw new Exception.AuthorizationException();

                        if (hrefAbsolutePath.Groups[1].Value.StartsWith(accountData.PhabricatorUrl, StringComparison.OrdinalIgnoreCase))
                        {
                            // Phabricator url found
                            string localPath = hrefAbsolutePath.Groups[1].Value.Substring(accountData.PhabricatorUrl.Length).TrimEnd('/');

                            // check if url points to wiki document
                            if (localPath.StartsWith("w/"))
                            {
                                // convert absolute url to relative url
                                html = html.Substring(0, hrefAbsolutePath.Groups[1].Index)
                                     + html.Substring(hrefAbsolutePath.Groups[1].Index + accountData.PhabricatorUrl.Length);
                            }
                            else
                            // check if url points to maniphest task
                            if (RegexSafe.IsMatch(localPath, "/T[0-9]+(/.*)?", RegexOptions.None))
                            {
                                // convert absolute url to relative url
                                html = html.Substring(0, hrefAbsolutePath.Groups[1].Index)
                                     + "maniphest"
                                     + html.Substring(hrefAbsolutePath.Groups[1].Index + accountData.PhabricatorUrl.Length);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Formats a non-existing URL into a "banned" URL
        /// </summary>
        /// <param name="database">reference to Phabrico database</param>
        /// <param name="urlOwner">URL where invalid URL is found in</param>
        /// <param name="urlHyperlink">non-existing relative URL</param>
        /// <param name="urlHyperlinkText">Hyperlink text</param>
        /// <returns>CSS-formatted banned hyperlink tag</returns>
        private string GenerateInvalidUrlError(Storage.Database database, string urlOwner, string urlHyperlink, string urlHyperlinkText)
        {
            if (urlOwner.Trim('/').Any())
            {
                database.MarkUrlAsInvalid(this, urlOwner, urlHyperlink);
            }

            // in case url starts with "/w/" or "w/", remove prefix
            urlHyperlink = RegexSafe.Replace(urlHyperlink, "^/?w/", "");

            bool invalidUrl = RegexSafe.IsMatch(urlHyperlink, "[\r\n\\\\]", RegexOptions.Singleline);
            if (invalidUrl)
            {
                return string.Format("<a class=\"phriction-link banned\" href=\"w/{0}\">{1}</a>",
                    urlHyperlink.Replace("\r", "")         // hide newlines in url
                                .Replace("\n", ""),        // hide newlines in url
                    urlHyperlinkText);
            }
            else
            {
                return string.Format("<a class=\"phriction-link banned\" href=\"w/{0}?title={1}\">{2}</a>",
                    urlHyperlink.Replace("\r", "")         // hide newlines in url
                                .Replace("\n", ""),        // hide newlines in url
                    HttpUtility.UrlPathEncode(urlHyperlinkText),
                    urlHyperlinkText);
            }
        }

        /// <summary>
        /// VErifies if a given URL is valid
        /// </summary>
        /// <param name="browser">Reference to browser</param>
        /// <param name="currentUrl">URL from where the relative URL to be validated origins from</param>
        /// <param name="urlHyperlink">Relative URL to be validated</param>
        /// <param name="urlHyperlinkText">Reference text to URL</param>
        /// <returns>True if not valid</returns>
        private bool InvalidUrl(Browser browser, string currentUrl, ref string urlHyperlink, ref string urlHyperlinkText)
        {
            if (bannedLinkedPhrictionDocument) return false;

            bool invalidUrl = RegexSafe.IsMatch(urlHyperlink, "[\r\n\\\\]", RegexOptions.Singleline);
            if (invalidUrl) return true;

            if (RegexSafe.IsMatch(urlHyperlink, "^https?://", RegexOptions.Singleline))
            {
                invalidUrl = Uri.IsWellFormedUriString(urlHyperlink, UriKind.Absolute) == false;
                return invalidUrl;
            }

            if (RegexSafe.IsMatch(urlHyperlink, "^tel:", RegexOptions.Singleline))
            {
                string phoneNumberHyperlink = urlHyperlink.Substring("tel:".Length).Trim();
                bool success = RegexSafe.IsMatch(phoneNumberHyperlink, @"
                    ^                                           # start of string
                    (
                        (                                       # 
                            ([+]?[0-9]+)                        # country code formatted as e.g. 0032 or +32
                            |                                   # 
                            (\(([+]?[0-9]+)\))                  # country code formatted as e.g. (0032) or (+32)
                        )                                       # 
                        \s*
                        [.-/]?                                  # dot, dash or slash after country code
                        \s*
                    )?
                        \s*
                    (\(([0-9]+)\))?                             # region code
                        \s*
                    [0-9.\x2D\x20]+                             # local phone number (can consist of numbers, dot, dash and/or space)
                    [0-9]                                       # last character of local phone number should be a number
                    $                                           # end of string.
                ", RegexOptions.IgnorePatternWhitespace);

                return (success == false);
            }

            if (RegexSafe.IsMatch(urlHyperlink, "^mailto:", RegexOptions.Singleline))
            {
                invalidUrl = new EmailAddressAttribute().IsValid(urlHyperlink.Substring("mailto:".Length).Trim()) == false;
                if (invalidUrl) return true;
            }

            string localHyperlink = urlHyperlink;
            if (localHyperlink.StartsWith("/"))
            {
                localHyperlink = localHyperlink.Substring(1);
            }

            if (urlHyperlinkText == null && linkedPhrictionDocument != null)
            {
                urlHyperlinkText = linkedPhrictionDocument.Name;
            }

            return linkedPhrictionDocument == null
                && localHyperlink.StartsWith("mailto:") == false
                && localHyperlink.StartsWith("tel:") == false
                && localHyperlink.StartsWith("ftp://") == false;
        }
    }
}
