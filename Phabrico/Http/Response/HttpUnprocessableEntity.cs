namespace Phabrico.Http.Response
{
    /// <summary>
    /// Represents the HTTP response to identify the browser that the origin POST request contained semantically erroneous data (HTTP code 422)
    /// </summary>
    public class HttpUnprocessableEntity : HtmlPage
    {
        /// <summary>
        /// Initializes a new HttpUnprocessableEntity object
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="url"></param>
        public HttpUnprocessableEntity(Http.Server httpServer, Browser browser, string url) : 
            base(httpServer, browser, url)
        {
            HttpStatusCode = 422;
            HttpStatusMessage = "Unprocessable entity";
        }        
    }
}
