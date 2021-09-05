using Newtonsoft.Json;
using Phabrico.Http;
using Phabrico.Http.Response;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phabrico.Controllers
{
    /// <summary>
    /// Represents the controller being used for the File-objects screen in Phabrico
    /// </summary>
    public class File : Controller
    {
        /// <summary>
        /// Model for table rows in the client backend
        /// </summary>
        public class JsonRecordData
        {
            /// <summary>
            /// Represents the 'File Name' column
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Represents the 'File object' column
            /// </summary>
            public int ID { get; set; }

            /// <summary>
            /// Represents the 'Number of references' column
            /// </summary>
            public int NumberOfReferences { get; set; }

            /// <summary>
            /// Represents the 'Size' column
            /// </summary>
            public int Size { get; set; }

            /// <summary>
            /// Represents the 'Type' column
            /// </summary>
            public string Type { get; set; }
        }

        /// <summary>
        /// Model for table rows in the client backend
        /// </summary>
        public class JsonRecordReferenceData
        {
            /// <summary>
            /// Represents the 'Content' column
            /// </summary>
            public string Title { get; set; }

            /// <summary>
            /// Represents the Fontawesome icon in the 'Content' column
            /// </summary>
            public string Type { get; set; }

            /// <summary>
            /// Link to the modified document or task
            /// </summary>
            public string URL { get; set; }
        }

        /// <summary>
        /// This method is fired when the user clicks on the 'File Object' id in the File-objects screen
        /// or when a file is referenced in a Phriction document or Maniphest task
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="fileObject"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/file/data")]
        public void HttpGetFileContent(Http.Server httpServer, Browser browser, ref Http.Response.File fileObject, string[] parameters, string parameterActions)
        {
            int fileId;
            if (parameters.Length == 1 &&
                Int32.TryParse(parameters[0], out fileId))
            {
                Storage.File fileStorage = new Storage.File();
                Storage.Stage stageStorage = new Storage.Stage();

                SessionManager.Token token = SessionManager.GetToken(browser);
                if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");

                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    Phabricator.Data.File file = fileStorage.GetByID(database, fileId, false);
                    if (file == null)
                    {
                        file = stageStorage.Get<Phabricator.Data.File>(database, Phabricator.Data.File.Prefix, fileId, true);
                    }

                    bool keepFilename = false;
                    if (file != null)
                    {
                        fileObject = new Http.Response.File(file.DataStream, file.ContentType, file.FileName, file.FontAwesomeIcon != null);

                        if (file.ContentType.Equals("image/drawio"))
                        {
                            fileObject.EnableBrowserCache = false;  // diagram drawing can be edited and should not be cached by browser
                            keepFilename = true;
                        }

                        if (keepFilename)
                        {
                            fileObject.IsAttachment = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// This method is fired when the user clicks on the 'File Object' id in the File-objects screen
        /// or when a file is referenced in a Phriction document or Maniphest task
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="fileObject"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/file/reference")]
        public Http.Response.HttpMessage HttpGetFileReferences(Http.Server httpServer, Browser browser, ref HtmlViewPage htmlViewPage, string[] parameters, string parameterActions)
        {
            int fileId;
            if (parameters.Length == 1 &&
                Int32.TryParse(parameters[0], out fileId))
            {
                Storage.File fileStorage = new Storage.File();
                Storage.Stage stageStorage = new Storage.Stage();

                SessionManager.Token token = SessionManager.GetToken(browser);
                if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");

                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    // set private encryption key
                    database.PrivateEncryptionKey = token.PrivateEncryptionKey;

                    Phabricator.Data.File file = fileStorage.GetByID(database, fileId, true);
                    if (file == null)
                    {
                        file = stageStorage.Get<Phabricator.Data.File>(database, Phabricator.Data.File.Prefix, fileId, false);
                    }

                    if (file != null)
                    {
                        Phabricator.Data.PhabricatorObject[] dependentObjects = database.GetDependentObjects(file.Token).ToArray();
                        if (dependentObjects.Length == 1)
                        {
                            Phabricator.Data.Phriction phrictionDocument = dependentObjects.First() as Phabricator.Data.Phriction;
                            if (phrictionDocument != null)
                            {
                                return new Http.Response.HttpRedirect(httpServer, browser, Http.Server.RootPath + "w/" + phrictionDocument.Path);
                            }

                            Phabricator.Data.Maniphest maniphestTask = dependentObjects.First() as Phabricator.Data.Maniphest;
                            if (maniphestTask != null)
                            {
                                return new Http.Response.HttpRedirect(httpServer, browser, Http.Server.RootPath + "maniphest/T" + maniphestTask.ID);
                            }
                        }

                        htmlViewPage = new Http.Response.HtmlViewPage(httpServer, browser, true, "FileReferences", parameters);
                        htmlViewPage.SetText("FILEID", file.ID.ToString());
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// This method is fired via javascript when the FileReferences detail screen is opened
        /// It will load all objects which have a reference to a given file and convert them in to a JSON array.
        /// This JSON array will be shown as a HTML table
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="resultHttpMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/file/references/search")]
        public void HttpGetPopulateFileReferenceTableData(Http.Server httpServer, Browser browser, ref HttpMessage resultHttpMessage, string[] parameters, string parameterActions)
        {
            List<JsonRecordReferenceData> tableRows = new List<JsonRecordReferenceData>();

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException("/file/references/search", "session expired");

            int fileId;
            if (parameters.Length == 1 &&
                Int32.TryParse(parameters[0], out fileId))
            {
                Storage.File fileStorage = new Storage.File();
                Storage.Stage stageStorage = new Storage.Stage();

                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    Phabricator.Data.File file = fileStorage.GetByID(database, fileId, true);
                    if (file == null)
                    {
                        file = stageStorage.Get<Phabricator.Data.File>(database, Phabricator.Data.File.Prefix, fileId, false);
                    }

                    if (file != null)
                    {
                        foreach (Phabricator.Data.PhabricatorObject dependentObject in database.GetDependentObjects(file.Token).ToArray())
                        {
                            Phabricator.Data.Phriction phrictionDocument = dependentObject as Phabricator.Data.Phriction;
                            if (phrictionDocument != null)
                            {
                                Storage.Phriction phrictionStorage = new Storage.Phriction();
                                string translatedCrumbs = "";
                                string internalCrumbs = "";
                                foreach (string slug in phrictionDocument.Path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    internalCrumbs += slug + "/";

                                    Phabricator.Data.Phriction crumbPhrictionReference = phrictionStorage.Get(database, internalCrumbs);
                                    if (crumbPhrictionReference != null)  // can be null when parents are not downloaded in commander-export -> these should be ignored
                                    {
                                        translatedCrumbs += " > " + crumbPhrictionReference?.Name ?? ConvertPhabricatorUrlPartToDescription(slug);
                                    }
                                }

                                JsonRecordReferenceData jsonRecordReferenceData = new JsonRecordReferenceData();
                                jsonRecordReferenceData.Title = translatedCrumbs.Substring(" > ".Length);
                                jsonRecordReferenceData.URL = Http.Server.RootPath + "w/" + phrictionDocument.Path;
                                jsonRecordReferenceData.Type = "fa-book";
                                tableRows.Add(jsonRecordReferenceData);
                                continue;
                            }

                            Phabricator.Data.Maniphest maniphestTask = dependentObject as Phabricator.Data.Maniphest;
                            if (maniphestTask != null)
                            {
                                JsonRecordReferenceData jsonRecordReferenceData = new JsonRecordReferenceData();
                                jsonRecordReferenceData.Title = maniphestTask.Name;
                                jsonRecordReferenceData.URL = Http.Server.RootPath + "maniphest/T" + maniphestTask.ID;
                                jsonRecordReferenceData.Type = "fa-anchor";
                                tableRows.Add(jsonRecordReferenceData);
                                continue;
                            }
                        }
                    }
                }
            }

            string jsonData = JsonConvert.SerializeObject(tableRows);
            resultHttpMessage = new JsonMessage(jsonData);
        }

        /// <summary>
        /// This method will be fired once for each upload.
        /// It will request a new File ID for storage.
        /// After the web browser has processed this answer, it will start executing the HttpPostUploadChunk method
        /// where this new File ID is sent with
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/file/getIDForNewFile")]
        public void HttpPostIDForNewFile(Http.Server httpServer, Browser browser, string[] parameters)
        {
            if (httpServer.Customization.HideFiles) throw new Phabrico.Exception.HttpNotFound("/file/getIDForNewFile");

            string jsonData;
            Storage.File fileStorage = new Storage.File();

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                jsonData = JsonConvert.SerializeObject(new { ID = fileStorage.GetNewID(database) });
            }

            JsonMessage jsonMessage = new JsonMessage(jsonData);
            jsonMessage.Send(browser);
        }

        /// <summary>
        /// This method is fired from the File Objects screen to fill the table.
        /// It's also executed when the search filter is changed
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/file/query")]
        public void HttpGetPopulateTableData(Http.Server httpServer, Browser browser, ref JsonMessage jsonMessage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.HideFiles) throw new Phabrico.Exception.HttpNotFound("/file/query");

            List<JsonRecordData> tableRows = new List<JsonRecordData>();

            Storage.File fileStorage = new Storage.File();
            if (fileStorage != null)
            {
                SessionManager.Token token = SessionManager.GetToken(browser);
                if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");

                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    IEnumerable<Phabricator.Data.File> files = fileStorage.GetReferenceInfo(database).OrderBy(file => file.FileName);
                    if (parameters.Any())
                    {
                        string filter = "";
                        string orderBy = System.Web.HttpUtility.UrlDecode(parameters[0]);
                        if (parameters.Length > 1)
                        {
                            filter = System.Web.HttpUtility.UrlDecode(parameters[1]);
                            if (filter.StartsWith("F"))
                            {
                                filter = filter.Substring(1);
                            }
                        }

                        int numericFilterText;
                        if (Int32.TryParse(filter, out numericFilterText) == false)
                        {
                            numericFilterText = Int32.MinValue;
                        }

                        files = files.Where(file => file.FileName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 
                                                 || file.ContentType.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                                                 || file.ID == numericFilterText
                                           );

                        switch (orderBy)
                        {
                            case "ID":
                                files = files.OrderBy(o => o.ID);
                                break;

                            case "ID-":
                                files = files.OrderByDescending(o => o.ID);
                                break;

                            case "Name":
                                files = files.OrderBy(o => o.FileName);
                                break;

                            case "Name-":
                                files = files.OrderByDescending(o => o.FileName);
                                break;

                            case "NumberOfReferences":
                                files = files.OrderBy(o => o.NumberOfReferences);
                                break;

                            case "NumberOfReferences-":
                                files = files.OrderByDescending(o => o.NumberOfReferences);
                                break;

                            case "Size":
                                files = files.OrderBy(o => o.Size);
                                break;

                            case "Size-":
                                files = files.OrderByDescending(o => o.Size);
                                break;

                            case "Type":
                                files = files.OrderBy(o => o.ContentType);
                                break;

                            case "Type-":
                                files = files.OrderByDescending(o => o.ContentType);
                                break;

                            default:
                                files = files.OrderBy(o => o.FileName);
                                break;
                        }
                    }

                    foreach (Phabricator.Data.File fileData in files)
                    {
                        JsonRecordData record = new JsonRecordData();

                        record.ID = fileData.ID;
                        record.Name = fileData.FileName;
                        record.Size = fileData.Size;
                        record.Type = fileData.ContentType;
                        record.NumberOfReferences = fileData.NumberOfReferences;

                        tableRows.Add(record);
                    }
                }
            }

            string jsonData = JsonConvert.SerializeObject(tableRows);
            jsonMessage = new JsonMessage(jsonData);
        }

        /// <summary>
        /// This method is executed once or more during a file upload to Phabrico.
        /// A file is uploaded in chunks. Each chunk upload will trigger this method.
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="parameters"></param>
        [UrlController(URL = "/file/uploadChunk")]
        public void HttpPostUploadChunk(Http.Server httpServer, Browser browser, string[] parameters)
        {
            if (httpServer.Customization.HideFiles) throw new Phabrico.Exception.HttpNotFound("/file/uploadChunk");

            lock (ReentrancyLock)
            {
                int fileID = Int32.Parse(parameters[0]);
                int nbrChunks = Int32.Parse(parameters[1]);
                int chunkID = Int32.Parse(parameters[2]);
                string fileName = System.Web.HttpUtility.UrlDecode( parameters[3] );

                // correct invalid characters
                fileName = fileName.Replace(':', '_')
                                   .Replace('\\', '_')
                                   .Replace('/', '_')
                                   .Replace('<', '_')
                                   .Replace('>', '_')
                                   .Replace('"', '\'')
                                   .Replace('|', '_')
                                   .Replace('?', '_')
                                   .Replace('*', '_');

                SessionManager.Token token = SessionManager.GetToken(browser);
                if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");

                Storage.File fileStorage = new Storage.File();
                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    Phabricator.Data.File.Chunk fileChunk = new Phabricator.Data.File.Chunk();
                    fileChunk.FileID = fileID;
                    fileChunk.ChunkID = chunkID;
                    fileChunk.NbrChunks = nbrChunks;
                    fileChunk.Data = browser.Session.OctetStreamData;
                    fileChunk.DateModified = DateTimeOffset.UtcNow;

                    fileStorage.AddChunk(database, fileName, fileChunk);
                }
            }
        }
    }
}
