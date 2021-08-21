namespace Phabrico.Http.Response
{
    /// <summary>
    /// Represents a HTTP response for a HTTP request which contained an unknown CSRF code
    /// </summary>
    public class InvalidCSRF : HttpMessage
    {
        /// <summary>
        /// Initializes a InvalidCSRF object
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="url"></param>
        public InvalidCSRF(Server httpServer, Browser browser, string url) : base(httpServer, browser, url)
        {
            HttpStatusCode = 440;
            HttpStatusMessage = "Session expired";
        }
    }
}