using Phabrico.Http;
using Phabrico.Miscellaneous;
using System;
using System.IO;

namespace Phabrico.Controllers
{
    /// <summary>
    /// Represents the controller being used for presenting the Diagram-objects in Phabrico
    /// </summary>
    public class Diagram : Controller
    {
        /// <summary>
        /// This method is fired when a diagram is referenced in a Phriction document or Maniphest task
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="fileObjectResponse"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/diagram/data")]
        public void HttpGetDiagramContent(Http.Server httpServer, ref Http.Response.File diagramObjectResponse, string[] parameters, string parameterActions)
        {
            int diagramId;
            bool isTranslatedObject = false;

            if (parameters.Length == 1)
            {
                string firstParameter = parameters[0];

                if (RegexSafe.IsMatch(firstParameter, "^tran[0-9]+", System.Text.RegularExpressions.RegexOptions.Singleline))
                {
                    isTranslatedObject = true;
                    firstParameter = firstParameter.Substring("tran".Length);
                }

                if (Int32.TryParse(firstParameter, out diagramId))
                {
                    Storage.Diagram diagramStorage = new Storage.Diagram();
                    Storage.Stage stageStorage = new Storage.Stage();

                    SessionManager.Token token = SessionManager.GetToken(browser);
                    if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");

                    using (Storage.Database database = new Storage.Database(EncryptionKey))
                    {
                        Phabricator.Data.Diagram diagram = null;

                        if (isTranslatedObject)
                        {
                            Storage.Content content = new Storage.Content(database);
                            string diagramToken = string.Format("PHID-OBJECT-{0}", diagramId.ToString().PadLeft(18, '0'));
                            Storage.Content.Translation translation = content.GetTranslation(diagramToken, browser.Session.Locale);
                            if (translation != null)
                            {
                                Newtonsoft.Json.Linq.JObject diagramObjectInfo = Newtonsoft.Json.JsonConvert.DeserializeObject(translation.TranslatedRemarkup) as Newtonsoft.Json.Linq.JObject;
                                if (diagramObjectInfo != null)
                                {
                                    diagram = new Phabricator.Data.Diagram();
                                    diagram.Token = diagramToken;

                                    string base64EncodedData = (string)diagramObjectInfo["Data"];
                                    byte[] buffer = new byte[(int)(base64EncodedData.Length * 0.8)];
                                    using (MemoryStream ms = new MemoryStream(buffer))
                                    using (Phabrico.Parsers.Base64.Base64EIDOStream base64EIDOStream = new Parsers.Base64.Base64EIDOStream(base64EncodedData))
                                    {
                                        base64EIDOStream.CopyTo(ms);
                                        Array.Resize(ref buffer, (int)base64EIDOStream.Length);
                                    }

                                    diagram.Data = buffer;
                                    diagram.Size = buffer.Length;
                                    diagram.FileName = (string)diagramObjectInfo["FileName"];
                                }
                            }
                        }
                        else
                        {
                            diagram = diagramStorage.GetByID(database, diagramId, false);
                        }

                        if (diagram == null)
                        {
                            diagram = stageStorage.Get<Phabricator.Data.Diagram>(database, browser.Session.Locale, Phabricator.Data.Diagram.Prefix, diagramId, true);
                        }

                        if (diagram != null)
                        {
                            diagramObjectResponse = new Http.Response.File(diagram.DataStream, "image/drawio", diagram.FileName, true);
                            diagramObjectResponse.EnableBrowserCache = false;  // diagram drawing can be edited and should not be cached by browser
                            diagramObjectResponse.IsAttachment = true;
                        }
                    }
                }
            }
        }
    }
}