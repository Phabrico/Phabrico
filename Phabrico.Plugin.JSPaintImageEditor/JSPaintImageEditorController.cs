using Newtonsoft.Json;
using Phabrico.Controllers;
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
    /// Represents the controller for all the JSPaintImageEditor functionalities
    /// </summary>
    public class JSPaintImageEditorController : PluginController
    {

        /// <summary>
        /// This method is fired when the user opens the Diagram screen (from the Phabrico navigator or via a Remarkup-editor)
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="httpFound"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/JSPaintImageEditor", ServerCache = false, HtmlViewPageOptions = HtmlViewPage.ContentOptions.HideGlobalTreeView)]
        public void HttpGetImageEditorScreen(Http.Server httpServer, ref HtmlViewPage  htmlViewPage, string[] parameters, string parameterActions)
        {
            Phabricator.Data.File fileObject = null;
            Storage.Account accountStorage = new Storage.Account();
            Storage.File fileStorage = new Storage.File();
            string imageFileName;
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
                        imageFileName = Locale.TranslateText("(New)", browser.Session.Locale);
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

                            imageFileName = string.Format("F{0}{1}",
                                isTranslatedObject ? "TRAN" : "",
                                fileObjectId);

                            if (fileObject != null && fileObject.FileType == Phabricator.Data.File.FileStyle.Image && fileObject.ContentType.Equals("image/drawio") == false)
                            {
                                // use loaded XML/PNG content from fileobject as initial template for IFrame content
                                string base64Data = fileObject.DataStream.ReadEncodedBlock(0, (int)fileObject.DataStream.LengthEncodedData);
                                htmlViewPage.SetText("IMG-SRC-BASE64", "data:image/png;base64," + base64Data, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                                htmlViewPage.SetText("IMAGE-FILE-NAME", imageFileName);
                                return;
                            }
                        }
                    }
                }
            }

            htmlViewPage = null;
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
        [UrlController(URL = "/JSPaint", HtmlViewPageOptions = HtmlViewPage.ContentOptions.IFrame)]
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
            string resourceName = string.Format("Phabrico.Plugin.JSPaint.{0}", string.Join(".", parameters.Take(parameters.Length - 1)))
                                        .Replace("-", "_")
                                + "."
                                + parameters.LastOrDefault();
            resourceName = resourceName.Split('?')
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
                httpFound = ReadResourceContent(assembly, httpServer, "JSPaint", url, resourceName);
            }
            else
            {
                httpFound = null;
            }

            return httpFound;
        }

        /// <summary>
        /// This method is fired when the user clicks on Save
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/JSPaintImageEditor/save")]
        public JsonMessage HttpPostSaveImage(Http.Server httpServer, string[] parameters)
        {
            try
            {
                const string base64Prefix = "data:image/png;base64,";

                string saveData = browser.Session.FormVariables[browser.Request.RawUrl]["data"];
                string fileID = browser.Session.FormVariables[browser.Request.RawUrl]["fileID"];
                string jsonData;

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

                            int numericFileID;
                            if (Int32.TryParse(fileID, out numericFileID))
                            {
                                Storage.File fileStorage = new Storage.File();
                                Phabricator.Data.File originalFile = fileStorage.GetByID(database, numericFileID, true);

                                file = new Phabricator.Data.File();
                                file.Data = buffer;
                                file.TemplateFileName = originalFile.FileName;

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
                                IEnumerable<Phabricator.Data.PhabricatorObject> referrers = database.GetDependentObjects(originalFile.Token, Language.NotApplicable);

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
                                            Match matchReferencedFileObject = RegexSafe.Match(referencedFileObject.Text, "{F(-?[0-9]*)", RegexOptions.None);
                                            if (matchReferencedFileObject.Success)
                                            {
                                                if (matchReferencedFileObject.Groups[1].Value.Equals(originalFileID) == false) continue;

                                                phrictionDocument.Content = phrictionDocument.Content.Substring(0, referencedFileObject.Start)
                                                                          + "{F" + file.ID
                                                                          + phrictionDocument.Content.Substring(referencedFileObject.Start + matchReferencedFileObject.Length);
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

                                jsonData = JsonConvert.SerializeObject(new
                                {
                                    Status = "OK",
                                    FileToken = file.ID
                                });

                                return new JsonMessage(jsonData);
                            }
                        }
                    }
                }

                jsonData = JsonConvert.SerializeObject(new
                {
                    Status = "OK",
                    FileID = ""
                });

                return new JsonMessage(jsonData);
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
