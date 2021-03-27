namespace Phabrico.Http.Response
{
    /// <summary>
    /// Represents a basic successful HTTP response
    /// </summary>
    public class HttpFound : HttpMessage
    {
        /// <summary>
        /// Initializes a HttpFound object
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="url"></param>
        public HttpFound(Http.Server httpServer, Browser browser, string url) : base(httpServer, browser, url)
        {
            HttpStatusCode = 200;
            HttpStatusMessage = "OK";
        }
    }
}
