using System.Text;

namespace Phabrico.Http.Response
{
    /// <summary>
    /// Represents the HTTP response to identify the browser that the given URL was incorrect (HTTP code 404)
    /// </summary>
    public class HttpNotFound : HtmlPage
    {
        /// <summary>
        /// Initializes a new HttpNotFound object
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="url"></param>
        public HttpNotFound(Http.Server httpServer, Browser browser, string url) : 
            base(httpServer, browser, url)
        {
            HttpStatusCode = 404;
            HttpStatusMessage = "Not Found";
        }

        /// <summary>
        /// Sends a sequence of bytes to the web browser
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="data"></param>
        public override void Send(Browser browser, byte[] data = null)
        {
            Theme = "light";

            string tokenId = browser.GetCookie("token");
            string encryptionKey = null;
            if (tokenId != null)
            {
                encryptionKey = SessionManager.GetToken(browser)?.EncryptionKey;

                if (encryptionKey != null)
                {
                    using (Storage.Database database = new Storage.Database(encryptionKey))
                    {
                        Theme = database.ApplicationTheme;
                    }
                }
            }

            string phabricatorUrl = "";
            if (encryptionKey != null)
            {
                Storage.Account accountStorage = new Storage.Account();
                SessionManager.Token token = SessionManager.GetToken(browser);
                using (Storage.Database database = new Storage.Database(null))
                {
                    System.UInt64[] publicXorCipher = accountStorage.GetPublicXorCipher(database, token);

                    // unmask encryption key
                    encryptionKey = Miscellaneous.Encryption.XorString(token.EncryptionKey, publicXorCipher);
                }

                // try to form URL on Phabricator server
                using (Storage.Database database = new Storage.Database(encryptionKey))
                {
                    Phabricator.Data.Account accountData = accountStorage.Get(database, token);
                    if (accountData != null)
                    {
                        if (Url.StartsWith("/maniphest/"))
                        {
                            phabricatorUrl = accountData.PhabricatorUrl + "/" + Url.Substring("/maniphest/".Length);
                        }
                        else
                        {
                            phabricatorUrl = accountData.PhabricatorUrl + "/" + Url;
                        }

                        phabricatorUrl = phabricatorUrl.Substring(0, "http://".Length)
                                       + phabricatorUrl.Substring("http://".Length).Replace("//", "/");
                        phabricatorUrl = phabricatorUrl.Split('?')[0];
                        phabricatorUrl = phabricatorUrl.TrimEnd('/');
                    }
                }
            }

            // return HTML page
            HtmlViewPage notFound = new HtmlViewPage(browser);
            string html = notFound.GetViewData("HttpNotFound")
                                  .Replace("@@THEME@@", Theme)
                                  .Replace("@@INVALID-LOCAL-URL@@", Url)
                                  .Replace("@@PHABRICATOR-URL@@", phabricatorUrl);

            data = UTF8Encoding.UTF8.GetBytes(html);
            base.Send(browser, data);
        }
    }
}
