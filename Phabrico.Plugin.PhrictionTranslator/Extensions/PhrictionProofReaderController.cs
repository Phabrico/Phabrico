using Newtonsoft.Json;
using Phabrico.Controllers;
using Phabrico.Http;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;
using Phabrico.Parsers.Remarkup;
using System.Linq;

namespace Phabrico.Plugin.Extensions
{
    public class PhrictionProofReaderController : PluginController
    {
        [UrlController(URL = "/proofread", HtmlViewPageOptions = HtmlViewPage.ContentOptions.HideGlobalTreeView)]
        public void HttpGetLoadParameters(Http.Server httpServer, ref HtmlViewPage viewPage, string[] parameters, string parameterActions)
        {
            string documentPath = string.Join("/", parameters.TakeWhile(parameter => parameter.StartsWith("?") == false));

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // retrieve master version
                Storage.Phriction phrictionStorage = new Storage.Phriction();
                Phabricator.Data.Phriction masterDocument = phrictionStorage.Get(database, documentPath, Language.NotApplicable, false);
                if (masterDocument == null)
                {
                    // ?!?
                    return;
                }

                RemarkupParserOutput remarkupParserOutput;
                string masterDocumentHtml = ConvertRemarkupToHTML(database, masterDocument.Path, masterDocument.Content, out remarkupParserOutput, true);

                // retrieve translated version
                Storage.Content content = new Storage.Content(database);
                Storage.Content.Translation translatedDocument = content.GetTranslation(masterDocument.Token, browser.Session.Locale);
                string translatedDocumentHtml = ConvertRemarkupToHTML(database, masterDocument.Path, translatedDocument.TranslatedRemarkup, out remarkupParserOutput, true);

                // create resulting view
                viewPage = new HtmlViewPage(httpServer, browser, true, "ProofReader", parameters);
                viewPage.SetText("PHRICTION-DOCUMENT-TOKEN", masterDocument.Token, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                viewPage.SetText("ORIGINAL-TITLE", masterDocument.Name, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                viewPage.SetText("ORIGINAL-REMARKUP", masterDocument.Content, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                viewPage.SetText("ORIGINAL-HTML", masterDocumentHtml, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue | HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                viewPage.SetText("TRANSLATED-TITLE", translatedDocument.TranslatedTitle, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                viewPage.SetText("TRANSLATED-REMARKUP", translatedDocument.TranslatedRemarkup, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                viewPage.SetText("TRANSLATED-HTML", translatedDocumentHtml, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue | HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
            }
        }

        [UrlController(URL = "/proofedit")]
        public JsonMessage HttpPostProofEditingDocument(Http.Server httpServer, string[] parameters)
        {
            string jsonData;
            try
            {
                string documentToken = browser.Session.FormVariables[browser.Request.RawUrl]["token"];
                string remarkup = browser.Session.FormVariables[browser.Request.RawUrl]["remarkup"];
                string documentPath;

                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    Storage.Phriction phrictionStorage = new Storage.Phriction();
                    Phabricator.Data.Phriction phrictionDocument = phrictionStorage.Get(database, documentToken, Language.NotApplicable);
                    documentPath = phrictionDocument.Path;
                    Storage.Content content = new Storage.Content(database);
                    Storage.Content.Translation translation = content.GetTranslation(documentToken, browser.Session.Locale);
                    content.AddTranslation(documentToken, browser.Session.Locale, translation.TranslatedTitle, remarkup);

                    string urlAlias = "/phriction/" + documentPath.Substring("/w/".Length);
                    httpServer.InvalidateNonStaticCache(EncryptionKey, documentPath);
                    httpServer.InvalidateNonStaticCache(EncryptionKey, urlAlias);
                }

                jsonData = JsonConvert.SerializeObject(new
                {
                    Status = "Redirect",
                    URL = "w/" + documentPath
                });
            }
            catch (System.Exception exception)
            {
                jsonData = JsonConvert.SerializeObject(new
                {
                    Status = "NOK",
                    Message = exception.Message
                });
            }

            return new JsonMessage(jsonData);
        }

        [UrlController(URL = "/PhrictionProofReader")]
        public JsonMessage HttpPostProofReadingDocument(Http.Server httpServer, string[] parameters)
        {
            string jsonData = JsonConvert.SerializeObject(new
            {
                Status = "Redirect",
                URL = "proofread/" + PhrictionData.Path
            });

            return new JsonMessage(jsonData);
        }
    }
}