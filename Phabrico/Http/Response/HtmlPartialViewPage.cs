namespace Phabrico.Http.Response
{
    /// <summary>
    /// Represents the HTTP response to form a partial HTML page
    /// </summary>
    public class HtmlPartialViewPage : HtmlViewPage
    {
        /// <summary>
        /// Content of the partial view
        /// </summary>
        public string TemplateContent { get; private set; }

        /// <summary>
        /// The current index position in the parent view where the next partial view should be inserted to
        /// </summary>
        public int Position { get; private set; }

        /// <summary>
        /// Initializes a new HTTP object which identifies a partial HTML Page
        /// </summary>
        /// <param name="browser"></param>
        private HtmlPartialViewPage(Browser browser)
            : base(browser)
        {
        }

        /// <summary>
        /// Initializes a new HTTP object which identifies a partial HTML Page
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="templateName">Name of the partial view</param>
        /// <param name="templateContent">The content of the template partial view</param>
        /// <param name="content">The content of to be merged into the template partial view</param>
        /// <param name="positionInParent">The current index position in the parent view where the next partial view should be inserted to</param>
        public HtmlPartialViewPage(Browser browser, string templateName, string templateContent, string content, int positionInParent)
            : base(browser)
        {
            this.Content = content;
            this.TemplateContent = templateContent;
            this.Position = positionInParent;
        }
    }
}
