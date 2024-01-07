using System.Text;
using System.Web;

namespace Phabrico.Http.Response
{
    /// <summary>
    /// Represents the HTTP response to make the browser redirect to another url (HTTP code 303)
    /// </summary>
    public class HttpRedirect : HttpMessage
    {
        private bool javascriptRedirection;

        /// <summary>
        /// Initializes a new HttpRedirect object
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="url"></param>
        /// <param name="viaJavascript"></param>
        public HttpRedirect(Http.Server httpServer, Browser browser, string url, bool viaJavascript = false) :
            base(httpServer, browser, url)
        {
            javascriptRedirection = viaJavascript;
            if (javascriptRedirection)
            {
                HttpStatusCode = 200;
                HttpStatusMessage = "OK";
                Content = string.Format("<html><script>window.location.replace(\"{0}\");</script></html>", url.Replace("\"", "%22"));
            }
            else
            {
                HttpStatusCode = 303;
                HttpStatusMessage = "See Other";
            }
        }

        /// <summary>
        /// Sends a sequence of bytes to the web browser
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="data"></param>
        public override void Send(Browser browser, byte[] data = null)
        {
            if (javascriptRedirection == false)
            {
                browser.Response.RedirectLocation = Url;
            }
            else
            {
                data = UTF8Encoding.UTF8.GetBytes(Content);
            }

            base.Send(browser, data);
        }
    }
}
