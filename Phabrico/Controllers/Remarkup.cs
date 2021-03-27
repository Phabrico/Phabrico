using Newtonsoft.Json;

using Phabrico.Http;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;
using Phabrico.Parsers.Remarkup;
using System;

namespace Phabrico.Controllers
{
    /// <summary>
    /// Represents the controller being used for the Remarkup-functionality in Phabrico
    /// </summary>
    public class Remarkup : Controller
    {
        /// <summary>
        /// This method is fired as soon as some Remarkup content should be converted to HTML.
        /// This can be in the Maniphest or Phriction screens (read and edit mode)
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="parameters"></param>
        [UrlController(URL = "/remarkup")]
        public void HttpPostTranslateToHTML(Http.Server httpServer, Browser browser, string[] parameters)
        {
            using (Storage.Database database = new Storage.Database(null))
            {
                Storage.Account accountStorage = new Storage.Account();
                SessionManager.Token token = SessionManager.GetToken(browser);
                UInt64[] publicXorCipher = accountStorage.GetPublicXorCipher(database, token);

                // unmask encryption key
                EncryptionKey = Encryption.XorString(EncryptionKey, publicXorCipher);
            }

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                string remarkupData = browser.Session.FormVariables["data"];
                string url = browser.Session.FormVariables["url"];

                string relativeUrl = System.Text.RegularExpressions.Regex.Match(url, "(https?://[^/]*)(/.*)\\?.*").Groups[2].Value;
                if (relativeUrl.StartsWith("/w/"))
                {
                    // if URL is a Phriction URL, lose the /w/ URL prefix
                    relativeUrl = relativeUrl.Substring("/w/".Length);
                }

                RemarkupParserOutput remarkupParserOutput;
                string htmlData = ConvertRemarkupToHTML(database, relativeUrl, remarkupData, out remarkupParserOutput, true);

                string result = JsonConvert.SerializeObject(new { html = htmlData });

                JsonMessage jsonMessage = new JsonMessage(result);
                jsonMessage.Send(browser);
            }
        }

        /// <summary>
        /// This method is fired when the user clicks on the Remarkup Syntax help button in the Phriction and Maniphest edit screens
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="htmlViewPage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/remarkup/syntax", HtmlViewPageOptions = Http.Response.HtmlViewPage.ContentOptions.UseLocalTreeView | HtmlViewPage.ContentOptions.HideHeader)]
        public void HttpGetRemarkupSyntaxHelp(Http.Server httpServer, Browser browser, ref HtmlViewPage htmlViewPage, string[] parameters, string parameterActions)
        {
            using (Storage.Database database = new Storage.Database(null))
            {
                Storage.Account accountStorage = new Storage.Account();
                SessionManager.Token token = SessionManager.GetToken(browser);
                UInt64[] publicXorCipher = accountStorage.GetPublicXorCipher(database, token);

                // unmask encryption key
                EncryptionKey = Encryption.XorString(EncryptionKey, publicXorCipher);
            }

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                RemarkupParserOutput remarkupParserOutput;
                string remarkupSyntaxLocalizedViewName = "RemarkupSyntax_" + browser.Session.Locale + ".remarkup";
                HtmlViewPage remarkupViewPage = new HtmlViewPage(httpServer, browser, false, remarkupSyntaxLocalizedViewName, null);
                string htmlData = ConvertRemarkupToHTML(database, remarkupViewPage.Url, remarkupViewPage.Content, out remarkupParserOutput, false);

                htmlViewPage = new HtmlViewPage(browser);
                htmlViewPage.SetContent(htmlData);
            }
        }
    }
}