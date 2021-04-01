using System.Text;

namespace Phabrico.Http.Response
{
    /// <summary>
    /// Represents the HTTP response to identify the browser that something very bad occurred (HTTP 500)
    /// </summary>
    public class HttpInternalServerError : HtmlPage
    {
        /// <summary>
        /// Initializes a new HttpInternalServerError object
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="url"></param>
        public HttpInternalServerError(Http.Server httpServer, Browser browser, string url) :
            base(httpServer, browser, url)
        {
            HttpStatusCode = 500;
            HttpStatusMessage = "Internal server error";
        }

        /// <summary>
        /// Sends a sequence of bytes to the web browser
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="data"></param>
        public override void Send(Browser browser, byte[] data = null)
        {
            base.Send(browser, data);
        }
    }
}
