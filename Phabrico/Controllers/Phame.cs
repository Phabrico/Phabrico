using Phabrico.Http;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;
using Phabrico.Parsers.Remarkup;
using System;
using System.Linq;
using System.Web;

namespace Phabrico.Controllers
{
    /// <summary>
    /// Represents the controller being used for the Phame-functionality in Phabrico
    /// </summary>
    public class Phame : Controller
    {
        /// <summary>
        /// This URL is fired when browsing through Phriction documents
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="viewPage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/phame", HtmlViewPageOptions = HtmlViewPage.ContentOptions.NoFormatting, ServerCache = false)]
        public void HttpGetLoadPosts(Http.Server httpServer, Browser browser, ref HtmlViewPage viewPage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.HidePhame) throw new Phabrico.Exception.HttpNotFound("/phame");
            
            string blogName = HttpUtility.UrlDecode(parameters.FirstOrDefault() ?? "");

            Storage.PhamePost phamePostStorage = new Storage.PhamePost();
            Storage.User userStorage = new Storage.User();

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // set private encryption key
                database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                viewPage.SetText("BLOG-TITLE", blogName);

                foreach (Phabricator.Data.PhamePost phamePost in phamePostStorage.Get(database).OrderByDescending(post => post.DateModified))
                {
                    if (string.IsNullOrWhiteSpace(blogName) == false && phamePost.Blog.Equals(blogName, StringComparison.OrdinalIgnoreCase) == false) continue;

                    // show only the first lines in the Phame overview
                    RemarkupParserOutput remarkupParserOutput;
                    string[] firstLines = RegexSafe.Replace(phamePost.Content, "{F[^}]*}", "")
                                                   .Split(new char[] { '\n' })
                                                   .ToArray();
                    int nbrLines = 0;
                    int lineIndex = 0;
                    foreach (string line in firstLines.ToArray())
                    {
                        lineIndex++;
                        if (string.IsNullOrWhiteSpace(line) == false) nbrLines++;
                        if (nbrLines == 3)
                        {
                            firstLines = firstLines.Take(lineIndex).ToArray();
                            break;
                        }
                    }

                    string shortedContent = string.Join( "\n", firstLines );
                    string formattedDocumentContent = ConvertRemarkupToHTML(database, "/", shortedContent, out remarkupParserOutput, false);

                    string authorName = "";
                    Phabricator.Data.User author = userStorage.Get(database, phamePost.Author);
                    if (author != null) authorName = author.UserName;

                    HtmlPartialViewPage blogPost = viewPage.GetPartialView("BLOG-POST");
                    blogPost.SetText("BLOG-POST-TITLE", phamePost.Title);
                    blogPost.SetText("BLOG-POST-CONTENT", formattedDocumentContent, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue | HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                    blogPost.SetText("BLOG-POST-ID", phamePost.ID);
                    blogPost.SetText("AUTHOR", authorName);
                    blogPost.SetText("DATE-MODIFIED", FormatDateTimeOffset(phamePost.DateModified, browser.Language), HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                }
            }
        }

        [UrlController(URL = "/phame/post/view/", HtmlViewPageOptions = HtmlViewPage.ContentOptions.NoFormatting)]
        public void HttpGetLoadPostData(Http.Server httpServer, Browser browser, ref HtmlViewPage viewPage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.HidePhame) throw new Phabrico.Exception.HttpNotFound("/phame");
 
            Storage.PhamePost phamePostStorage = new Storage.PhamePost();
            Storage.User userStorage = new Storage.User();
            
            string blogPostID = parameters.FirstOrDefault();

            viewPage = new HtmlViewPage(httpServer, browser, true, "PhamePost", parameters);

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // set private encryption key
                database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                Phabricator.Data.PhamePost phamePost = phamePostStorage.Get(database, blogPostID);
                if (phamePost == null) throw new Phabrico.Exception.HttpNotFound("/phame/post/view/" + blogPostID);

                string authorName = "";
                Phabricator.Data.User author = userStorage.Get(database, phamePost.Author);
                if (author != null) authorName = author.UserName;

                RemarkupParserOutput remarkupParserOutput;
                string formattedDocumentContent = ConvertRemarkupToHTML(database, "/", phamePost.Content, out remarkupParserOutput, false);

                viewPage.SetText("BLOG-POST-TITLE", phamePost.Title);
                viewPage.SetText("BLOG-POST-CONTENT", formattedDocumentContent, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue | HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                viewPage.SetText("AUTHOR", authorName);
                viewPage.SetText("DATE-MODIFIED", FormatDateTimeOffset(phamePost.DateModified, browser.Language), HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
            }
       }

        [UrlController(URL = "/phame/post/", HtmlViewPageOptions = HtmlViewPage.ContentOptions.Default)]
        public void HttpGetLoadPostReference(Http.Server httpServer, Browser browser, ref HtmlViewPage viewPage, string[] parameters, string parameterActions)
        {
            HttpGetLoadPostData(httpServer, browser, ref viewPage, parameters, parameterActions);
        }
   }
}