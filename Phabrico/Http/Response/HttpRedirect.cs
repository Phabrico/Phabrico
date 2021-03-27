namespace Phabrico.Http.Response
{
    /// <summary>
    /// Represents the HTTP response to make the browser redirect to another url (HTTP code 303)
    /// </summary>
    public class HttpRedirect : HttpMessage
    {
        /// <summary>
        /// Initializes a new HttpRedirect object
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="url"></param>
        public HttpRedirect(Http.Server httpServer, Browser browser, string url) :
            base(httpServer, browser, url)
        {
            HttpStatusCode = 303;
            HttpStatusMessage = "See Other";
        }

        /// <summary>
        /// Sends a sequence of bytes to the web browser
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="data"></param>
        public override void Send(Browser browser, byte[] data = null)
        {
            browser.Response.RedirectLocation = Url;

            base.Send(browser, data);
        }
    }
}
