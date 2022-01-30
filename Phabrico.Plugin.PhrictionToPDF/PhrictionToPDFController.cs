using Newtonsoft.Json;
using NReco.PdfGenerator;
using Phabrico.Controllers;
using Phabrico.Http;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Phabrico.Plugin
{
    /// <summary>
    /// Represents the controller for all the PhrictionToPDF functionalities
    /// </summary>
    public class PhrictionToPDFController : PluginController
    {
        private readonly static object _synchronizationObject = new object();

        /// <summary>
        /// internal counter for creating a unique cache key (see cachedFileData)
        /// </summary>
        static int cacheCounter = 0;

        /// <summary>
        /// The NReco PDFGenerator will convert HTML to PDF.
        /// For linked images in the HTML, new HTTP requests are made from the NReco library itself.
        /// Because all data in the Phabrico database is encrypted, all linked images need to be temporary decoded and cached into this dictionary.
        /// When the NReco library creates a HTTP request to an image, the image data will be retrieved from this dictionary (see HttpGetCachedDecodedFile)
        /// </summary>
        static readonly Dictionary<string,Http.Response.File> cachedFileData = new Dictionary<string, Http.Response.File>();

        /// <summary>
        /// Retrieves the decoded data of a referenced image in the Phriction document to be exported to PDF
        /// See also cachedFileData
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="fileObject"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/PhrictionToPDF/file/data", Unsecure = true)]
        public void HttpGetCachedDecodedFile(Http.Server httpServer, ref Http.Response.File fileObject, string[] parameters, string parameterActions)
        {
            string cacheKey = parameters.LastOrDefault();

            Http.Response.File result;
            lock (_synchronizationObject)
            {
                if (cachedFileData.TryGetValue(cacheKey, out result))
                {
                    fileObject = result;
                }
                else
                {
                    fileObject = null;
                }
            }
        }

        /// <summary>
        /// This method is fired when the user clicks on the 'Export to PDF' in the Phriction action pane.
        /// It will convert to Remarkup first to HTML.
        /// Afterwards it will modify some CSS stylesheets in the resulting HTML to hide some data or presenting it
        /// better for PDF.
        /// The modified generated HTML will then be exported to PDF by means of the NReco PDFGenerator library.
        /// 
        /// This method will also check if there are any underlying Phriction documents under the current Phriction document.
        /// If so, a 'Confirm' JSON request will be sent instead representing a question to the user if the underlying documents
        /// should also be exported to PDF.
        /// The response for this 'Confirm' JSON request is handled by the HttpPostExportToPDFConfirmation method
        /// After this HttpPostExportToPDFConfirmation reponse is handled in the browser, this method is executed again
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/PhrictionToPDF")]
        public JsonMessage HttpPostExportToPDF(Http.Server httpServer, string[] parameters)
        {
            string title = PhrictionData.Crumbs.Split('>').LastOrDefault().Trim();

            cacheCounter++;

            Phabrico.Storage.Phriction phrictionStorage = new Phabrico.Storage.Phriction();

            List<string> underlyingPhrictionTokens = new List<string>();
            using (Phabrico.Storage.Database database = new Phabrico.Storage.Database(EncryptionKey))
            {
                string jsonData;
                Phabricator.Data.Phriction phrictionDocument = phrictionStorage.Get(database, PhrictionData.Path, browser.Session.Locale);

                if (phrictionDocument != null)
                {
                    RetrieveUnderlyingPhrictionDocuments(database, phrictionDocument.Token, ref underlyingPhrictionTokens);
                }

                if (PhrictionData.IsPrepared == false)
                {
                    List<string> askParameters = new List<string>();

                    Model.PhrictionToPDFConfiguration phrictionToPDFConfiguration = Storage.PhrictionToPDFConfiguration.Load(database, null);
                    foreach (string headerFooterText in new string[] { phrictionToPDFConfiguration.HeaderData.Text1, phrictionToPDFConfiguration.HeaderData.Text2, phrictionToPDFConfiguration.HeaderData.Text3,
                                                                       phrictionToPDFConfiguration.FooterData.Text1, phrictionToPDFConfiguration.FooterData.Text2, phrictionToPDFConfiguration.FooterData.Text3
                                                                     })
                    {
                        foreach (Match askParameter in RegexSafe.Matches(headerFooterText, "{ASK ([^}]*)}"))
                        {
                            askParameters.Add(askParameter.Groups[1].Value);
                        }
                    }


                    string innerHTML = null;
                    if (askParameters.Any())
                    {
                        innerHTML = "<table><tbody>";

                        for (int p = 0; p < askParameters.Count; p++)
                        {
                            innerHTML += string.Format(
                                    @"<tr>
                                        <td>
                                            <label class='aphront-form-label' style='display:inline-block; width:10%; text-align: right; white-space: nowrap;' for='input-{0}'>{1}</label>
                                        </td>
                                        <td>
                                            <input id='input-{0}' name='input-{0}' style='margin-top: 4px; width: calc(90% - 8px);' type='text' autofocus=''>
                                        </td>
                                     </tr>",
                                    p + 1,
                                    HttpUtility.HtmlEncode(askParameters[p]));
                        }

                        innerHTML += "</tbody></table>";


                        jsonData = JsonConvert.SerializeObject(new
                        {
                            Status = "Prepare",
                            DialogTitle = Locale.TranslateText("Custom template parameters", browser.Session.Locale),
                            DialogHTML = innerHTML
                        });

                        return new JsonMessage(jsonData);
                    }
                }

                if (underlyingPhrictionTokens.Any() && PhrictionData.ConfirmState == ConfirmResponse.None)
                {
                    jsonData = JsonConvert.SerializeObject(new
                    {
                        Status = "Confirm",
                        Message = Locale.TranslateText("There are @@NBR-CHILD-DOCUMENTS@@ underlying documents. Would you like to export these as well ?", browser.Session.Locale)
                                        .Replace("@@NBR-CHILD-DOCUMENTS@@", underlyingPhrictionTokens.Count.ToString())
                    });

                    return new JsonMessage(jsonData);
                }
                else
                {
                    HtmlToPdfConverter htmlToPdf = new HtmlToPdfConverter();

                    // overwrite content again because we need to know the linked objects
                    Parsers.Remarkup.RemarkupParserOutput remarkupPerserOutput;
                    PhrictionData.Content = ConvertRemarkupToHTML(database, phrictionDocument?.Path, phrictionDocument?.Content, out remarkupPerserOutput, false);

                    // move headers 1 level lower
                    PhrictionData.Content = LowerHeaderLevels(PhrictionData.Content);

                    if (phrictionDocument != null)
                    {
                        // add title
                        PhrictionData.Content = "<h1>" + HttpUtility.HtmlEncode(phrictionDocument.Name) + "</h1>" + PhrictionData.Content;
                    }

                    // read content of all linked files and store them temporary in cache dictionary
                    Phabrico.Storage.File fileStorage = new Phabrico.Storage.File();
                    lock (_synchronizationObject)
                    {
                        foreach (Phabricator.Data.File linkedFile in remarkupPerserOutput.LinkedPhabricatorObjects.OfType<Phabricator.Data.File>())
                        {
                            string cacheKey = string.Format("{0}-{1}", cacheCounter, linkedFile.ID);
                            if (cachedFileData.ContainsKey(cacheKey)) continue;

                            Phabricator.Data.File linkedFileWithContent = fileStorage.Get(database, linkedFile.Token, browser.Session.Locale);
                            if (linkedFileWithContent == null)
                            {
                                // file not found in database ?!?
                                continue;
                            }

                            cachedFileData[cacheKey] = new Http.Response.File(linkedFileWithContent.DataStream, linkedFileWithContent.ContentType, linkedFileWithContent.FileName, true);
                            PhrictionData.Content = PhrictionData.Content.Replace("file/data/" + linkedFileWithContent.ID + "/",
                                                      httpServer.Address + "PhrictionToPDF/file/data/" + cacheKey + "/");
                        }
                    }

                    // do we also need to export the underlying documents ?
                    if (PhrictionData.ConfirmState == ConfirmResponse.Yes)
                    {
                        foreach (string underlyingPhrictionToken in underlyingPhrictionTokens)
                        {
                            Phabricator.Data.Phriction underlyingPhrictionDocument = phrictionStorage.Get(database, underlyingPhrictionToken, browser.Session.Locale);
                            if (underlyingPhrictionDocument != null)
                            {
                                if (string.IsNullOrWhiteSpace(underlyingPhrictionDocument.Content)) continue;

                                string html = ConvertRemarkupToHTML(database, underlyingPhrictionDocument.Path, underlyingPhrictionDocument.Content, out remarkupPerserOutput, false);
                                lock (_synchronizationObject)
                                {
                                    foreach (Phabricator.Data.File linkedFile in remarkupPerserOutput.LinkedPhabricatorObjects.OfType<Phabricator.Data.File>())
                                    {
                                        string cacheKey = string.Format("{0}-{1}", cacheCounter, linkedFile.ID);
                                        if (cachedFileData.ContainsKey(cacheKey)) continue;

                                        Phabricator.Data.File linkedFileWithContent = fileStorage.Get(database, linkedFile.Token, browser.Session.Locale);
                                        if (linkedFileWithContent == null)
                                        {
                                            // file not found in database ?!?
                                            continue;
                                        }

                                        cachedFileData[cacheKey] = new Http.Response.File(linkedFileWithContent.DataStream, linkedFileWithContent.ContentType, linkedFileWithContent.FileName, true);
                                        html = html.Replace("file/data/" + linkedFileWithContent.ID + "/",
                                                            httpServer.Address + "PhrictionToPDF/file/data/" + cacheKey + "/");
                                    }
                                }

                                // move headers 1 level lower
                                html = LowerHeaderLevels(html);

                                // add title
                                html = "<h1>" + HttpUtility.HtmlEncode(underlyingPhrictionDocument.Name) + "</h1>" + html;

                                PhrictionData.Content += "<div class='page-break'></div>";
                                PhrictionData.Content += string.Format("<div class='underlying-document-title'>{0}</div>", HttpUtility.HtmlEncode(underlyingPhrictionDocument.Name));
                                PhrictionData.Content += html;
                            }
                        }
                    }

                    // remove maximum auto-size parameters for images
                    PhrictionData.Content = PhrictionData.Content.Replace("width: 100%;max-width: max-content;", "");

                    // add a <br> tag before each image
                    PhrictionData.Content = PhrictionData.Content.Replace("<img ", "<br><img ");

                    // configure page layout
                    Model.PhrictionToPDFConfiguration phrictionToPDFConfiguration = Storage.PhrictionToPDFConfiguration.Load(database, phrictionDocument);
                    string pageHeaderHtml = phrictionToPDFConfiguration.PageHeaderHtml;
                    foreach (Match askParameter in RegexSafe.Matches(pageHeaderHtml, "{ASK ([^}]*)}").OfType<Match>().OrderByDescending(match => match.Index).ToArray())
                    {
                        string parameterName = askParameter.Groups[1].Value;
                        string parameterValue = browser.Session.FormVariables[browser.Request.RawUrl][parameterName];

                        if (Http.Server.UnitTesting)
                        {
                            // this is some code that is only executed during unit tests
                            if (parameterValue is null)
                            {
                                // dirty easy check if ASK parameter was correctly implemented
                                // (otherwise we need to convert the generated PDF to text and search for the ASK parameter in this text -> too complex)
                                throw new ArgumentException("Unit test failed: unable to retrieve ASK parameter value");
                            }
                        }

                        pageHeaderHtml = pageHeaderHtml.Replace(askParameter.Value, parameterValue);
                    }
                    htmlToPdf.PageHeaderHtml = pageHeaderHtml;

                    string pageFooterHtml = phrictionToPDFConfiguration.PageFooterHtml;
                    foreach (Match askParameter in RegexSafe.Matches(pageFooterHtml, "{ASK ([^}]*)}").OfType<Match>().OrderByDescending(match => match.Index).ToArray())
                    {
                        string parameterName = askParameter.Groups[1].Value;
                        string parameterValue = browser.Session.FormVariables[browser.Request.RawUrl][parameterName];
                        pageFooterHtml = pageFooterHtml.Replace(askParameter.Value, parameterValue);
                    }

                    htmlToPdf.PageFooterHtml = pageFooterHtml;

                    // convert HTML to PDF
                    PhrictionData.Content = MergeStyleSheets(title, PhrictionData.Content);
                    byte[] pdfBytes = htmlToPdf.GeneratePdf(PhrictionData.Content);
                    string pdfBase64 = Convert.ToBase64String(pdfBytes);

                    // clear cache
                    lock (_synchronizationObject)
                    {
                        string cacheKeyPrefix = string.Format("{0}-", cacheCounter);
                        foreach (string cacheKey in cachedFileData.Keys
                                                                  .Where(key => key.StartsWith(cacheKeyPrefix))
                                                                  .ToArray()
                                )
                        {
                            cachedFileData.Remove(cacheKey);
                        }
                    }

                    // return result
                    jsonData = JsonConvert.SerializeObject(new
                    {
                        Status = "Finished",
                        Base64Data = pdfBase64,
                        FileName = title + ".pdf",
                        ContentType = "application/pdf"
                    });

                    return new JsonMessage(jsonData);
                }
            }
        }

        /// <summary>
        /// This method is fired when the user confirms or declines to export underlying Phriction documents to PDF
        /// See also HttpPostExportToPDF
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/PhrictionToPDF/confirm")]
        public JsonMessage HttpPostExportToPDFConfirmation(Http.Server httpServer, string[] parameters)
        {
            string formVariablesUrl = browser.Request.RawUrl.Substring(0, browser.Request.RawUrl.Length - "/confirm".Length);
            DictionarySafe<string, string> formVariables = browser.Session.FormVariables[formVariablesUrl];

            string content = formVariables["content"];
            string path = formVariables["path"];

            Phabrico.Storage.Phriction phrictionStorage = new Phabrico.Storage.Phriction();

            List<string> underlyingPhrictionTokens = new List<string>();
            using (Phabrico.Storage.Database database = new Phabrico.Storage.Database(EncryptionKey))
            {
                Phabricator.Data.Phriction phrictionDocument = phrictionStorage.Get(database, path, browser.Session.Locale);

                if (phrictionDocument != null)
                {
                    RetrieveUnderlyingPhrictionDocuments(database, phrictionDocument.Token, ref underlyingPhrictionTokens);
                }

                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append(content);

                foreach (string underlyingPhrictionToken in underlyingPhrictionTokens)
                {
                    Parsers.Remarkup.RemarkupParserOutput remarkupPerserOutput;
                    phrictionDocument = phrictionStorage.Get(database, underlyingPhrictionToken, browser.Session.Locale);
                    ConvertRemarkupToHTML(database, phrictionDocument.Path, phrictionDocument.Content, out remarkupPerserOutput, false);
                }
            }

            string jsonData;
            if (underlyingPhrictionTokens.Any())
            {
                jsonData = JsonConvert.SerializeObject(new
                {
                    Status = "Confirm",
                    Message = Locale.TranslateText("There are @@NBR-CHILD-DOCUMENTS@@ underlying documents. Would you like to export these as well ?", browser.Session.Locale)
                });
            }
            else
            {
                jsonData = JsonConvert.SerializeObject(new
                {
                    Status = "Finished"
                });
            }

            return new JsonMessage(jsonData);
        }

        /// <summary>
        /// This method is fired when a PhrictionToPDF parameter is changed in the configuration screen
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/PhrictionToPDF/configuration/save")]
        public JsonMessage HttpPostSaveConfiguration(Http.Server httpServer, string[] parameters)
        {
            string headerLayout = browser.Session.FormVariables["/PhrictionToPDF/configuration/save/"]["headerLayout"];
            string footerLayout = browser.Session.FormVariables["/PhrictionToPDF/configuration/save/"]["footerLayout"];

            Model.PhrictionToPDFConfiguration configuration = new Model.PhrictionToPDFConfiguration(null);
            configuration.PageHeaderJson = headerLayout;
            configuration.PageFooterJson = footerLayout;

            using (Phabrico.Storage.Database database = new Phabrico.Storage.Database(null))
            {
                Storage.PhrictionToPDFConfiguration.Save(database, configuration);
            }
            string jsonData = JsonConvert.SerializeObject(new
            {
                Status = "OK"
            });

            return new JsonMessage(jsonData);
        }

        /// <summary>
        /// Is executed after GetConfigurationViewPage and fills in all the data in the plugin tab in the configuration screen
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="configurationTabContent"></param>
        public override void LoadConfigurationParameters(PluginBase plugin, HtmlPartialViewPage configurationTabContent)
        {
            using (Phabrico.Storage.Database database = new Phabrico.Storage.Database(null))
            {
                Model.PhrictionToPDFConfiguration phrictionToPDFConfiguration = Storage.PhrictionToPDFConfiguration.Load(database, null);

                configurationTabContent.SetText("HEADER-FONT-NAME", phrictionToPDFConfiguration.HeaderData.Font);
                configurationTabContent.SetText("HEADER-FONT-SIZE", phrictionToPDFConfiguration.HeaderData.FontSize + "px");
                configurationTabContent.SetText("HEADER-SIZE1", phrictionToPDFConfiguration.HeaderData.Size1, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                configurationTabContent.SetText("HEADER-TEXT1", phrictionToPDFConfiguration.HeaderData.Text1, HtmlViewPage.ArgumentOptions.NoHtmlEncoding | HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                configurationTabContent.SetText("HEADER-ALIGN1", phrictionToPDFConfiguration.HeaderData.Align1, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                configurationTabContent.SetText("HEADER-SIZE2", phrictionToPDFConfiguration.HeaderData.Size2, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                configurationTabContent.SetText("HEADER-TEXT2", phrictionToPDFConfiguration.HeaderData.Text2, HtmlViewPage.ArgumentOptions.NoHtmlEncoding | HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                configurationTabContent.SetText("HEADER-ALIGN2", phrictionToPDFConfiguration.HeaderData.Align2, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                configurationTabContent.SetText("HEADER-TEXT3", phrictionToPDFConfiguration.HeaderData.Text3, HtmlViewPage.ArgumentOptions.NoHtmlEncoding | HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                configurationTabContent.SetText("HEADER-ALIGN3", phrictionToPDFConfiguration.HeaderData.Align3, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);

                configurationTabContent.SetText("FOOTER-FONT-NAME", phrictionToPDFConfiguration.FooterData.Font);
                configurationTabContent.SetText("FOOTER-FONT-SIZE", phrictionToPDFConfiguration.FooterData.FontSize + "px");
                configurationTabContent.SetText("FOOTER-SIZE1", phrictionToPDFConfiguration.FooterData.Size1, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                configurationTabContent.SetText("FOOTER-TEXT1", phrictionToPDFConfiguration.FooterData.Text1, HtmlViewPage.ArgumentOptions.NoHtmlEncoding | HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                configurationTabContent.SetText("FOOTER-ALIGN1", phrictionToPDFConfiguration.FooterData.Align1, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                configurationTabContent.SetText("FOOTER-SIZE2", phrictionToPDFConfiguration.FooterData.Size2, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                configurationTabContent.SetText("FOOTER-TEXT2", phrictionToPDFConfiguration.FooterData.Text2, HtmlViewPage.ArgumentOptions.NoHtmlEncoding | HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                configurationTabContent.SetText("FOOTER-ALIGN2", phrictionToPDFConfiguration.FooterData.Align2, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                configurationTabContent.SetText("FOOTER-TEXT3", phrictionToPDFConfiguration.FooterData.Text3, HtmlViewPage.ArgumentOptions.NoHtmlEncoding | HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                configurationTabContent.SetText("FOOTER-ALIGN3", phrictionToPDFConfiguration.FooterData.Align3, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
            }
        }

        /// <summary>
        /// This method will increase all H HTML tags
        /// E.g. H1 becomes H2
        /// </summary>
        /// <param name="originalHTML"></param>
        /// <returns></returns>
        private string LowerHeaderLevels(string originalHTML)
        {
            string result = originalHTML;

            Match[] headerTags = RegexSafe.Matches(originalHTML, "</?h([1-6])")
                                          .OfType<Match>()
                                          .OrderByDescending(match => match.Index)
                                          .ToArray();
            foreach (Match headerTag in headerTags)
            {
                int headerLevel = Int32.Parse(headerTag.Groups[1].Value);
                result = result.Substring(0, headerTag.Groups[1].Index)
                       + (headerLevel + 1)
                       + result.Substring(headerTag.Groups[1].Index + 1);
            }

            return result;
        }

        /// <summary>
        /// Merges some PDF-friendly CSS with some HTML content
        /// </summary>
        /// <param name="title">Title to be shown in PDF document</param>
        /// <param name="content">HTML content to be restyled</param>
        /// <returns>PDF-friendly HTML</returns>
        private string MergeStyleSheets(string title, string content)
        {
            title = HttpUtility.HtmlEncode(title);

            return string.Format(@"<html lang='en'>
                        <head>
                            <meta content='text/html;charset=utf-8' http-equiv='Content-Type'>
                            <meta name='viewport' content='width=device-width, initial-scale=1'>
                            <meta name='google' content='notranslate'>
                            <title>{0}</title>
                            <style>
                                .phui-font-fa::before {{
                                    content: '\200B';
                                }}

                                .page-break {{
                                    display: block;
                                    page-break-before: always;
                                }}

                                .underlying-document-title {{
                                    font-weight: bold;
                                    text-decoration: underline;
                                    font-size: 2.5em;
                                    margin-bottom: .5em;
                                }}

                                body {{
                                    border-collapse: collapse;
                                    font-family: sans-serif;
                                    font-size: 14px;
                                    direction: ltr;
                                    text-align: left;
                                    unicode-bidi: embed;
                                    -webkit-text-size-adjust: none;
                                }}

                                table {{
                                    border-collapse: collapse;
                                }}

                                th, td {{
                                    border: solid 1px black;
                                    padding: 3px 6px;
                                }}

                                th {{
                                    background: #ddd;
                                }}

                                a {{
                                    color: #04f;
                                }}

                                button {{
                                    display: none;
                                }}
                            </style>
                        </head>
                        <body class='phriction' data-theme='light'>{1}</body></html>",
                        title,
                        content);
        }

        /// <summary>
        /// Downloads recursively all underlying Phriction documents for a given Phriction document
        /// </summary>
        /// <param name="database">Phabrico database</param>
        /// <param name="phrictionToken">Token of Phriction document to be analyzed</param>
        /// <param name="underlyingPhrictionTokens">Collection of tokens of all underlying Phriction documents</param>
        private void RetrieveUnderlyingPhrictionDocuments(Database database, string phrictionToken, ref List<string> underlyingPhrictionTokens)
        {
            foreach (string childToken in database.GetUnderlyingTokens(phrictionToken, "WIKI", browser))
            {
                if (underlyingPhrictionTokens.Contains(childToken)) continue;
                underlyingPhrictionTokens.Add(childToken);

                RetrieveUnderlyingPhrictionDocuments(database, childToken, ref underlyingPhrictionTokens);
            }
        }
    }
}
