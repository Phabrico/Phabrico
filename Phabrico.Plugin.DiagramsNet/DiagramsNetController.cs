using Newtonsoft.Json;
using Phabrico.Controllers;
using Phabrico.Http;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;
using Phabrico.Parsers.Remarkup;
using Phabrico.Parsers.Remarkup.Rules;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Phabrico.Plugin
{
    /// <summary>
    /// Represents the controller for all the DiagramsNet functionalities
    /// </summary>
    public class DiagramsNetController : PluginController
    {
        /// <summary>
        /// This method is fired when the user selects 'Diagram -> Help -> Keyboard Shortcuts'
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="htmlViewPage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/diagrams.net/help", HtmlViewPageOptions = HtmlViewPage.ContentOptions.HideGlobalTreeView | HtmlViewPage.ContentOptions.HideHeader)]
        public void HttpGetHelpShortcuts(Http.Server httpServer, ref HtmlViewPage htmlViewPage, string[] parameters, string parameterActions)
        {
            htmlViewPage = new HtmlViewPage(httpServer, browser, true, "HelpShortcuts", parameters);
        }

        /// <summary>
        /// This method is fired when the user selects 'Diagram -> Help -> Quick Start Video'
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="htmlViewPage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/diagrams.net/quickstart", HtmlViewPageOptions = HtmlViewPage.ContentOptions.HideGlobalTreeView | HtmlViewPage.ContentOptions.HideHeader)]
        public void HttpGetHelpQuickStartDiagramsNet(Http.Server httpServer, ref HtmlViewPage htmlViewPage, string[] parameters, string parameterActions)
        {
            htmlViewPage = new HtmlViewPage(httpServer, browser, true, "HelpQuickStartDiagramsNet", parameters);
        }

        /// <summary>
        /// This method is fired when the user opens the Diagram screen (from the Phabrico navigator or via a Remarkup-editor)
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="httpFound"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/diagrams.net", ServerCache = false, HtmlViewPageOptions = HtmlViewPage.ContentOptions.HideGlobalTreeView)]
        public void HttpGetDiagramsScreen(Http.Server httpServer, ref HttpFound httpFound, string[] parameters, string parameterActions)
        {
            Phabricator.Data.File fileObject = null;
            Storage.Account accountStorage = new Storage.Account();
            Storage.File fileStorage = new Storage.File();
            string diagramName;
            string theme;
            bool openedFromRemarkupEditor = false;
            Phabricator.Data.Account.DarkenImageStyle darkenImageStyle;

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                if (parameters.Any())
                {
                    if (parameters.FirstOrDefault().Equals("new"))
                    {
                        // diagramsnet is opened from a remarkup editor
                        openedFromRemarkupEditor = true;
                        diagramName = Locale.TranslateText("(New)", browser.Session.Locale);
                    }
                    else
                    {
                        // parameter is file object to be edited
                        string fileObjectName = parameters.FirstOrDefault();

                        // check if parameter is really a file object
                        Match matchFileObjectName = RegexSafe.Match(fileObjectName, "F(TRAN)?(-?[0-9]+)", RegexOptions.None);
                        if (matchFileObjectName.Success)
                        {
                            // open given file object
                            bool isTranslatedObject = matchFileObjectName.Groups[1].Success;
                            int fileObjectId = Int32.Parse(matchFileObjectName.Groups[2].Value);

                            if (isTranslatedObject)
                            {
                                string token = string.Format("PHID-OBJECT-{0}", fileObjectId.ToString().PadLeft(18, '0'));
                                Storage.Content content = new Content(database);
                                Storage.Content.Translation translation = content.GetTranslation(token, browser.Session.Locale);
                                if (translation != null)
                                {
                                    Newtonsoft.Json.Linq.JObject fileObjectInfo = Newtonsoft.Json.JsonConvert.DeserializeObject(translation.TranslatedRemarkup) as Newtonsoft.Json.Linq.JObject;
                                    if (fileObjectInfo != null)
                                    {
                                        fileObject = new Phabricator.Data.File();
                                        fileObject.ID = fileObjectId;
                                        fileObject.Token = token;

                                        string base64EncodedData = (string)fileObjectInfo["Data"];
                                        byte[] buffer = new byte[(int)(base64EncodedData.Length * 0.8)];
                                        using (MemoryStream ms = new MemoryStream(buffer))
                                        using (Phabrico.Parsers.Base64.Base64EIDOStream base64EIDOStream = new Parsers.Base64.Base64EIDOStream(base64EncodedData))
                                        {
                                            base64EIDOStream.CopyTo(ms);
                                            Array.Resize(ref buffer, (int)base64EIDOStream.Length);
                                        }

                                        fileObject.Data = buffer;
                                        fileObject.Size = buffer.Length;
                                        fileObject.Properties = (string)fileObjectInfo["Properties"];
                                        fileObject.FileName = (string)fileObjectInfo["FileName"];
                                    }
                                }
                            }
                            else
                            if (fileObjectName.StartsWith("F-"))
                            {
                                Storage.Stage stageStorage = new Storage.Stage();
                                fileObject = stageStorage.Get<Phabricator.Data.File>(database, browser.Session.Locale, Phabricator.Data.File.Prefix, fileObjectId, true);
                            }
                            else
                            {
                                fileObject = fileStorage.GetByID(database, fileObjectId, false);
                            }

                            diagramName = string.Format("F{0}{1}", 
                                isTranslatedObject ? "TRAN" : "",
                                fileObjectId);
                        }
                        else
                        {
                            // load plugin static data
                            string url = string.Join("/", parameters);
                            string resourceName = string.Format("Phabrico.Plugin.{0}", string.Join(".", parameters))
                                                        .Split('?')
                                                        .FirstOrDefault()
                                                        .TrimEnd('.');

                            Assembly assembly = Assembly.GetExecutingAssembly();
                            resourceName = assembly.GetManifestResourceNames()
                                                   .FirstOrDefault(name => name.Equals(resourceName, System.StringComparison.OrdinalIgnoreCase));
                            if (resourceName == null)
                            {
                                httpFound = new HttpNotFound(httpServer, browser, url);
                            }
                            else
                            {
                                httpFound = ReadResourceContent(assembly, httpServer, "", url, resourceName);
                            }
                            return;
                        }
                    }
                }
                else
                {
                    diagramName = Locale.TranslateText("(New)", browser.Session.Locale);
                }

                Phabricator.Data.Account accountData = accountStorage.WhoAmI(database, browser);
                theme = accountData.Theme;
                darkenImageStyle = accountData.Parameters.DarkenBrightImages;

                HtmlViewPage htmlViewPage = new HtmlViewPage(httpServer, browser, true, "DiagramEditor", parameters);

                if (fileObject == null)
                {
                    // use "empty" XML/PNG as initial template for IFrame content
                    htmlViewPage.SetText("IMG-SRC-BASE64", "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAACyHRFWHRteGZpbGUAJTNDbXhmaWxlJTIwaG9zdCUzRCUyMkVsZWN0cm9uJTIyJTIwbW9kaWZpZWQlM0QlMjIyMDIxLTAzLTA4VDE1JTNBNDElM0ExNy45NzZaJTIyJTIwYWdlbnQlM0QlMjI1LjAlMjAoV2luZG93cyUyME5UJTIwMTAuMCUzQiUyMFdpbjY0JTNCJTIweDY0KSUyMEFwcGxlV2ViS2l0JTJGNTM3LjM2JTIwKEtIVE1MJTJDJTIwbGlrZSUyMEdlY2tvKSUyMGRyYXcuaW8lMkYxMy42LjIlMjBDaHJvbWUlMkY4My4wLjQxMDMuMTIyJTIwRWxlY3Ryb24lMkY5LjIuMCUyMFNhZmFyaSUyRjUzNy4zNiUyMiUyMGV0YWclM0QlMjJWNng2Y0xyd2FEalRTZmphWnV6WCUyMiUyMHZlcnNpb24lM0QlMjIxMy42LjIlMjIlMjB0eXBlJTNEJTIyZGV2aWNlJTIyJTNFJTNDZGlhZ3JhbSUyMGlkJTNEJTIyWmh6TlJTbUxhbVhLdzB6enhJc0klMjIlMjBuYW1lJTNEJTIyUGFnZS0xJTIyJTNFZFpGQkU0SWdFSVYlMkZEWGVWY3V4c1ZwZE9Iam96c2drejZESklvJTJGWHIwd0V5eGpxeGZPODlGaFpDeTI0Nkc2YkZGVGtva2lWOEl2UklzaXhOaW54ZUZ2SjBwRWc5YUkzazNyU0NXcjRnSkQxOVNBNURaTFNJeWtvZHd3YjdIaG9iTVdZTWpySHRqaXJ1cWxrTEcxQTNURzNwVFhJclBNMzN1MVc0Z0d4RmFKM21CNmQwTExqOVV3YkJPSTVmaUZhRWxnYlJ1cXFiU2xETDlNSmdYTzcwUiUyRjNjekVCdmZ3VG1ZajE3M2tSZlJLczMlM0MlMkZkaWFncmFtJTNFJTNDJTJGbXhmaWxlJTNFZhx3AwAAAA1JREFUGFdj+P///38ACfsD/QVDRcoAAAAASUVORK5CYII=", HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                }
                else
                {
                    // use loaded XML/PNG content from fileobject as initial template for IFrame content
                    string base64Data = fileObject.DataStream.ReadEncodedBlock(0, (int)fileObject.DataStream.LengthEncodedData);
                    htmlViewPage.SetText("IMG-SRC-BASE64", "data:image/png;base64," + base64Data, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                }

                if (parameters.Any() || openedFromRemarkupEditor)
                {
                    htmlViewPage.SetText("HIDE-EXIT-BTN", "False", HtmlViewPage.ArgumentOptions.Default);
                }
                else
                {
                    htmlViewPage.SetText("HIDE-EXIT-BTN", "True", HtmlViewPage.ArgumentOptions.Default);
                }


                htmlViewPage.SetText("DIAGRAM-NAME", diagramName, HtmlViewPage.ArgumentOptions.Default);
                htmlViewPage.SetText("NEW-DIAGRAM-NAME", Locale.TranslateText("(New)", browser.Session.Locale), HtmlViewPage.ArgumentOptions.Default);
                htmlViewPage.SetText("LANGUAGE", browser.Session.Locale, HtmlViewPage.ArgumentOptions.Default);

                if (theme.Equals("dark"))
                {
                    switch (darkenImageStyle)
                    {
                        case Phabricator.Data.Account.DarkenImageStyle.Extreme:
                            htmlViewPage.SetText("IFRAME-FILTER", "invert(80.7%) sepia(0%) saturate(302%) hue-rotate(180deg) brightness(139%) contrast(100%)", HtmlViewPage.ArgumentOptions.Default);
                            break;

                        case Phabricator.Data.Account.DarkenImageStyle.Moderate:
                            htmlViewPage.SetText("IFRAME-FILTER", "brightness(60%) contrast(150%)", HtmlViewPage.ArgumentOptions.Default);
                            break;

                        case Phabricator.Data.Account.DarkenImageStyle.Disabled:
                        default:
                            htmlViewPage.SetText("IFRAME-FILTER", "none", HtmlViewPage.ArgumentOptions.Default);
                            break;
                    }
                }
                else
                {
                    htmlViewPage.SetText("IFRAME-FILTER", "none", HtmlViewPage.ArgumentOptions.Default);
                }

                if (fileObject == null || fileObject.ID > 0)
                {
                    htmlViewPage.SetText("SHOW-APPROVE-TRANSLATION-BTN", "False", HtmlViewPage.ArgumentOptions.Default);
                }
                else
                {
                    Content content = new Content(database);
                    Content.Translation translation = content.GetTranslation(fileObject.Token, browser.Session.Locale);
                    if (translation == null ||  translation.IsReviewed == true)
                    {
                        htmlViewPage.SetText("SHOW-APPROVE-TRANSLATION-BTN", "False", HtmlViewPage.ArgumentOptions.Default);
                    }
                    else
                    {
                        htmlViewPage.SetText("SHOW-APPROVE-TRANSLATION-BTN", "True", HtmlViewPage.ArgumentOptions.Default);
                        htmlViewPage.SetText("DIAGRAM-TOKEN", fileObject.Token, HtmlViewPage.ArgumentOptions.Default);

                        if (fileObject.DateModified == DateTimeOffset.MinValue)
                        {
                            // diagram was cloned from another existing (non-translated) diagram, but has not been modified yet
                            htmlViewPage.SetText("DISABLE-APPROVE-TRANSLATION-BTN", "True", HtmlViewPage.ArgumentOptions.Default);
                        }
                        else
                        {
                            htmlViewPage.SetText("DISABLE-APPROVE-TRANSLATION-BTN", "False", HtmlViewPage.ArgumentOptions.Default);
                        }
                    }
                }

                httpFound = htmlViewPage;
            }
        }

        /// <summary>
        /// The Diagrams.Net (formerly known as Draw.io) application is loaded in an IFRAME tag.
        /// The IFRAME will execute this method to load (and show) the Diagrams.Net application
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="httpFound"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        /// <returns></returns>
        [UrlController(URL = "/diagrams.net/webapp", HtmlViewPageOptions = HtmlViewPage.ContentOptions.IFrame)]
        public HttpMessage HttpGetDiagramsNetEditorIFrame(Http.Server httpServer, ref HttpFound httpFound, string[] parameters, string parameterActions)
        {
            if (parameters.TakeWhile(parameter => parameter.StartsWith("?") == false).Any() == false)
            {
                parameters = new string[] { "index.html" };
            }
            else
            {
                string firstParameterWithQuestionMark = parameters.FirstOrDefault(parameter => parameter.Contains('?'));
                if (firstParameterWithQuestionMark != null)
                {
                    int indexFirstParameterWithQuestionMark = Array.IndexOf(parameters, firstParameterWithQuestionMark);
                    parameters = parameters.Take(indexFirstParameterWithQuestionMark + 1).ToArray();
                }
            }

            string url = string.Join("/", parameters);
            string resourceName = string.Format("Phabrico.Plugin.webapp.{0}", string.Join(".", parameters))
                                        .Split('?')
                                        .FirstOrDefault()
                                        .TrimEnd('.');

            Assembly assembly = Assembly.GetExecutingAssembly();
            resourceName = assembly.GetManifestResourceNames()
                                   .FirstOrDefault(name => name.Equals(resourceName, System.StringComparison.OrdinalIgnoreCase));
            if (resourceName == null)
            {
                resourceName = string.Format("Phabrico.Plugin.{0}", string.Join(".", parameters))
                                     .Split('?')
                                     .FirstOrDefault()
                                     .TrimEnd('.');
                resourceName = assembly.GetManifestResourceNames()
                                       .FirstOrDefault(name => name.Equals(resourceName.TrimEnd('.'), System.StringComparison.OrdinalIgnoreCase));
            }

            if (resourceName != null)
            {
                httpFound = ReadResourceContent(assembly, httpServer, "webapp", url, resourceName);
                HtmlViewPage htmlViewPage = httpFound as HtmlViewPage;
                if (htmlViewPage != null)
                {
                    htmlViewPage.CssUrls = new string[] { "styles/dark.css" };
                }
            }

            return null;
        }

        /// <summary>
        /// This method is fired when the user clicks on the Save button
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/diagrams.net/save")]
        public JsonMessage HttpPostSaveFlowchart(Http.Server httpServer, string[] parameters)
        {
            try
            {
                const string base64Prefix = "data:image/png;base64,";
                
                string saveData = browser.Session.FormVariables[browser.Request.RawUrl]["data"];
                string fileID = browser.Session.FormVariables[browser.Request.RawUrl]["fileID"];
                bool isTranslation = false;
                bool.TryParse(browser.Session.FormVariables[browser.Request.RawUrl]["isTranslation"], out isTranslation);

                if (saveData.StartsWith(base64Prefix))
                {
                    string base64EncodedData = saveData.Substring(base64Prefix.Length);
                    byte[] buffer = new byte[(int)(base64EncodedData.Length * 0.8)];
                    using (MemoryStream ms = new MemoryStream(buffer))
                    using (Phabrico.Parsers.Base64.Base64EIDOStream base64EIDOStream = new Parsers.Base64.Base64EIDOStream(base64EncodedData))
                    {
                        base64EIDOStream.CopyTo(ms);
                        Array.Resize(ref buffer, (int)base64EIDOStream.Length);
                    }

                    Phabricator.Data.File file;
                    using (Storage.Database database = new Storage.Database(EncryptionKey))
                    {
                        Storage.Stage stageStorage = new Storage.Stage();
                        if (fileID.StartsWith("-"))
                        {
                            // negative fileID -> file is aleady staged
                            file = stageStorage.Get<Phabricator.Data.File>(database, browser.Session.Locale, Phabricator.Data.File.Prefix, Int32.Parse(fileID), true);

                            if (file == null)
                            {
                                throw new FileNotFoundException("File not found", string.Format("F{0}", fileID));
                            }

                            file.Data = buffer;
                            stageStorage.Edit(database, file, browser);
                        }
                        else
                        {
                            // positive fileID -> file is not staged yet
                            file = new Phabricator.Data.File();
                            file.Data = buffer;
                            file.TemplateFileName = "diagram ({0}).png";

                            using (MemoryStream memoryStream = new MemoryStream(file.Data))
                            {
                                using (Bitmap bitmap = new Bitmap(memoryStream))
                                {
                                    file.ImagePropertyPixelHeight = bitmap.Height;
                                    file.ImagePropertyPixelWidth = bitmap.Width;
                                }
                            }


                            stageStorage.Create(database, browser, file);

                            // search for all wiki/task objects in which the original file is referenced
                            int numericFileID;
                            if (Int32.TryParse(fileID, out numericFileID))  // fileID could be NaN (when creating a new diagram or when creating from remarkup editor)
                            {
                                Content content = new Content(database);
                                IEnumerable<Phabricator.Data.PhabricatorObject> referrers = new Phabricator.Data.PhabricatorObject[0];
                                Phabricator.Data.File originalFile = null;
                                if (isTranslation)
                                {
                                    string phidObjectToken = string.Format("PHID-OBJECT-{0}", fileID.PadLeft(18, '0'));
                                    Storage.Content.Translation translation = content.GetTranslation(phidObjectToken, browser.Session.Locale);
                                    if (translation != null)
                                    {
                                        Newtonsoft.Json.Linq.JObject fileObjectInfo = Newtonsoft.Json.JsonConvert.DeserializeObject(translation.TranslatedRemarkup) as Newtonsoft.Json.Linq.JObject;
                                        if (fileObjectInfo != null)
                                        {
                                            originalFile = new Phabricator.Data.File();
                                            originalFile.ID = Int32.Parse(fileID);

                                            referrers = database.GetDependentObjects(phidObjectToken, browser.Session.Locale);
                                        }
                                    }
                                }
                                else
                                {
                                    Storage.File fileStorage = new Storage.File();
                                    originalFile = fileStorage.GetByID(database, Int32.Parse(fileID), true);
                                    referrers = database.GetDependentObjects(originalFile.Token, Language.NotApplicable);
                                }

                                RemarkupEngine remarkupEngine = new RemarkupEngine();
                                foreach (Phabricator.Data.PhabricatorObject referrer in referrers)
                                {
                                    RemarkupParserOutput remarkupParserOutput;
                                    Phabricator.Data.Phriction phrictionDocument = referrer as Phabricator.Data.Phriction;
                                    Phabricator.Data.Maniphest maniphestTask = referrer as Phabricator.Data.Maniphest;

                                    // rename file object in referencing phriction document
                                    if (phrictionDocument != null)
                                    {
                                        remarkupEngine.ToHTML(null, database, browser, "/", phrictionDocument.Content, out remarkupParserOutput, false);
                                        List<RuleReferenceFile> referencedFileObjects = remarkupParserOutput.TokenList
                                                                                                            .OfType<RuleReferenceFile>()
                                                                                                            .ToList();
                                        referencedFileObjects.Reverse();

                                        string originalFileID = originalFile.ID.ToString();
                                        foreach (RuleReferenceFile referencedFileObject in referencedFileObjects)
                                        {
                                            Match matchReferencedFileObject;
                                            if (isTranslation)
                                            {
                                                matchReferencedFileObject = RegexSafe.Match(referencedFileObject.Text, "{FTRAN([0-9]*)", RegexOptions.None);
                                            }
                                            else
                                            {
                                                matchReferencedFileObject = RegexSafe.Match(referencedFileObject.Text, "{F(-?[0-9]*)", RegexOptions.None);
                                            }

                                            if (matchReferencedFileObject.Success)
                                            {
                                                if (matchReferencedFileObject.Groups[1].Value.Equals(originalFileID) == false) continue;

                                                phrictionDocument.Content = phrictionDocument.Content.Substring(0, referencedFileObject.Start)
                                                                          + "{F" + file.ID
                                                                          + phrictionDocument.Content.Substring(referencedFileObject.Start + matchReferencedFileObject.Length);

                                                if (isTranslation)
                                                {
                                                    content.DisapproveTranslation(phrictionDocument.Token, browser.Session.Locale);
                                                }
                                            }
                                        }

                                        // stage document
                                        stageStorage.Modify(database, phrictionDocument, browser);
                                    }

                                    // rename file object in referencing maniphest task
                                    if (maniphestTask != null)
                                    {
                                        remarkupEngine.ToHTML(null, database, browser, "/", maniphestTask.Description, out remarkupParserOutput, false);
                                        List<RuleReferenceFile> referencedFileObjects = remarkupParserOutput.TokenList
                                                                                                            .OfType<RuleReferenceFile>()
                                                                                                            .ToList();
                                        referencedFileObjects.Reverse();

                                        string originalFileID = originalFile.ID.ToString();
                                        foreach (RuleReferenceFile referencedFileObject in referencedFileObjects)
                                        {
                                            Match matchReferencedFileObject = RegexSafe.Match(referencedFileObject.Text, "{F(-?[0-9]*)", RegexOptions.None);
                                            if (matchReferencedFileObject.Success)
                                            {
                                                if (matchReferencedFileObject.Groups[1].Value.Equals(originalFileID) == false) continue;

                                                maniphestTask.Description = maniphestTask.Description.Substring(0, referencedFileObject.Start)
                                                                          + "{F" + file.ID
                                                                          + maniphestTask.Description.Substring(referencedFileObject.Start + matchReferencedFileObject.Length);
                                            }
                                        }

                                        // stage maniphest task
                                        stageStorage.Modify(database, maniphestTask, browser);
                                    }
                                }
                            }
                        }
                    }

                    string jsonData = JsonConvert.SerializeObject(new
                    {
                        Status = "OK",
                        FileToken = file.ID
                    });

                    return new JsonMessage(jsonData);
                }
                else
                {
                    string jsonData = JsonConvert.SerializeObject(new
                    {
                        Status = "OK",
                        FileID = ""
                    });

                    return new JsonMessage(jsonData);
                }
            }
            catch (System.Exception e)
            {
                string jsonData = JsonConvert.SerializeObject(new
                {
                    Status = "Error",
                    Error = e.Message
                });
                return new JsonMessage(jsonData);
            }
        }
    }
}
