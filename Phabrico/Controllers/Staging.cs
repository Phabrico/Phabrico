using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Phabrico.Http;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Phabrico.Controllers
{
    /// <summary>
    /// Represents the controller being used for the Offline-changes-functionality in Phabrico
    /// </summary>
    public class Staging : Controller
    {
        /// <summary>
        /// Model for table rows in the client backend
        /// </summary>
        public class JsonRecordData
        {
            public enum IssueType
            {
                /// <summary>
                /// No icon
                /// </summary>
                None,

                /// <summary>
                /// Phriction document has a path with a length between 100 and 115 characters
                /// Yellow Exclamation icon
                /// </summary>
                SlugIsLong,

                /// <summary>
                /// Phriction document has path with length of more than 114 characters
                /// Red Disallowed icon
                /// </summary>
                SlugIsTooLong,

                /// <summary>
                /// Phriction document was translated into the current selected language.
                /// This version of the Phriction document will never be uploaded
                /// Blue translation (comments) icon
                /// </summary>
                Translation
            }

            /// <summary>
            /// In case an issue is detected with staged data, an issue icon is shown at the beginning of the table record
            /// </summary>
            [JsonConverter(typeof(StringEnumConverter))]
            public IssueType Issue { get; set; }

            /// <summary>
            /// This is set to true when a document or a task has been modified in both Phabrico and Phabricator.
            /// If this is set to true, a blinking triangle icon is shown for the offline change record
            /// </summary>
            public bool MergeConflict { get; set; }

            /// <summary>
            /// If this is set to true, a frozen snow icon is shown for the offline change record to indicate
            /// that this change will not be uploaded to Phabricator for the next synchronization action.
            /// If this is set to false, a flame is shown
            /// </summary>
            public bool Frozen { get; set; }

            /// <summary>
            /// If this is set to true, the modified content points to a content translation for the current language
            /// If this is set to false, the modified content points to a real phabrico object (e.g. Phriction document, Maniphest task, ...)
            /// </summary>
            public bool IsTranslation { get; set; }

            /// <summary>
            /// Represents the 'Last modified at' column
            /// </summary>
            public string Timestamp { get; set; }

            /// <summary>
            /// Represents the 'Content' column
            /// </summary>
            public string Title { get; set; }

            /// <summary>
            /// Contains the token of the document or task that has been modified.
            /// This token is used as parameter for the action buttons
            /// </summary>
            public string Token { get; set; }

            /// <summary>
            /// Contains the internal name of the metadata that has been changed (e.g. the assignee of a task)
            /// </summary>
            public string TransactionModifier { get; set; }

            /// <summary>
            /// Represents the 'Modification' column in case the change was a metadata change
            /// </summary>
            public string TransactionText { get; set; }

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
        /// This controller method is a fired when the user opens the Offline Changes screen.
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="viewPage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/offline/changes")]
        public void HttpGetLoadParameters(Http.Server httpServer, ref HtmlViewPage viewPage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.HideOfflineChanges) throw new Phabrico.Exception.HttpNotFound("/offline/changes");

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // set private encryption key
                database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                Storage.Stage stage = new Storage.Stage();
                viewPage = new Http.Response.HtmlViewPage(httpServer, browser, true, "Staging", null);

                if (stage.Get(database, browser.Session.Locale).Any())
                {
                    viewPage.GetPartialView("DATA-VIEWPAGE");
                }
                else
                {
                    viewPage.RemovePartialView("DATA-VIEWPAGE");
                }

                viewPage.Merge();
            }
        }

        /// <summary>
        /// This method is fired via javascript when the Offline Changes screen is opened or when the search filter has been changed.
        /// It will load all (modified) items and convert them in to a JSON array.
        /// This JSON array will be shown as a HTML table
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/offline/changes/data")]
        public void HttpGetPopulateTableData(Http.Server httpServer, ref JsonMessage jsonMessage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.HideOfflineChanges) throw new Phabrico.Exception.HttpNotFound("/offline/changes/data");

            List<JsonRecordData> tableRows = new List<JsonRecordData>();

            Storage.Stage stageStorage = new Storage.Stage();
            if (stageStorage != null)
            {
                SessionManager.Token token = SessionManager.GetToken(browser);
                if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");

                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    // set private encryption key
                    database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                    Storage.Project projectStorage = new Storage.Project();
                    Storage.User userStorage = new Storage.User();

                    Content content = new Content(database);

                    List<Storage.Stage.Data> stagedRecords = stageStorage.Get(database, Language.NotApplicable)
                                                                         .ToList();
                    stagedRecords.AddRange(stageStorage.Get(database, browser.Session.Locale)
                                                       .Where(record => stagedRecords.Select(r => r.HeaderData)
                                                                                     .Contains(record.HeaderData) == false
                                                             )
                                          );
                    stagedRecords = stagedRecords.OrderByDescending(modification => modification.DateModified.ToUnixTimeSeconds())  // do not order by milliseconds
                                                 .ThenBy(modification => modification.Operation)
                                                 .ToList();

                    foreach (Storage.Stage.Data stageData in stagedRecords)
                    {
                        JsonRecordData record = new JsonRecordData();
                        string unknownToken = stageData.Token;

                        record.Title = "?!?";
                        record.URL = "";
                        record.Type = "";
                        record.TransactionModifier = "";
                        record.TransactionText = "";
                        record.Issue = JsonRecordData.IssueType.None;
                        record.IsTranslation = content.GetTranslation(stageData.Token, browser.Session.Locale) != null;  // might be overwritten later

                        if (stageData.Token.StartsWith("PHID-NEWTOKEN-"))
                        {
                            record.TransactionModifier = "new";
                        }

                        JObject deserializedObject = JsonConvert.DeserializeObject(stageData.HeaderData) as JObject;
                        if (deserializedObject != null)
                        {
                            unknownToken = (string)deserializedObject["TokenPrefix"];
                            if (unknownToken.StartsWith(Phabricator.Data.Transaction.Prefix))
                            {
                                unknownToken = (string)deserializedObject["Token"];
                                if (unknownToken.StartsWith(Phabricator.Data.Maniphest.Prefix))
                                {
                                    record.TransactionModifier = (string)deserializedObject["Type"];

                                    Storage.Maniphest maniphestStorage = new Storage.Maniphest();
                                    Phabricator.Data.Maniphest maniphest = maniphestStorage.Get(database, unknownToken, browser.Session.Locale);
                                    stageData.HeaderData = JsonConvert.SerializeObject(maniphest);

                                    Match inputTagsParameter = RegexSafe.Match(record.TransactionModifier, "^(project|subscriber)-[0-9]*$", RegexOptions.None);
                                    if (inputTagsParameter.Success)
                                    {
                                        string inputTagValue = maniphest.Transactions.FirstOrDefault(tran => tran.Type.Equals(record.TransactionModifier))?.NewValue;

                                        switch (inputTagsParameter.Groups[1].Value)
                                        {
                                            case "project":
                                                Phabricator.Data.Project project = projectStorage.Get(database, inputTagValue, browser.Session.Locale);
                                                record.TransactionText = Locale.TranslateText("Project @@PROJECT@@ tagged", browser.Session.Locale).Replace("@@PROJECT@@", project.Name);
                                                break;

                                            case "subscriber":
                                                Phabricator.Data.User subscriberUser = userStorage.Get(database, inputTagValue, browser.Session.Locale);
                                                if (subscriberUser != null)
                                                {
                                                    record.TransactionText = Locale.TranslateText("Subscriber @@SUBSCRIBER@@ added", browser.Session.Locale).Replace("@@SUBSCRIBER@@", subscriberUser.RealName);
                                                }
                                                else
                                                {
                                                    Phabricator.Data.Project subscriberProject = projectStorage.Get(database, inputTagValue, browser.Session.Locale);
                                                    record.TransactionText = Locale.TranslateText("Subscriber @@SUBSCRIBER@@ added", browser.Session.Locale).Replace("@@SUBSCRIBER@@", subscriberProject.Name);
                                                }
                                                break;

                                            default:
                                                break;
                                        }
                                    }
                                    else
                                    {
                                        record.TransactionText = Locale.TranslateText(string.Format("({0}{1} modified)", Char.ToUpper(record.TransactionModifier[0]), record.TransactionModifier.Substring(1)), browser.Session.Locale);
                                    }
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }

                        if (unknownToken.StartsWith(Phabricator.Data.Phriction.Prefix))
                        {
                            Phabricator.Data.Phriction document = JsonConvert.DeserializeObject<Phabricator.Data.Phriction>(stageData.HeaderData);
                            record.IsTranslation = document.Language.Equals(Language.NotApplicable) == false;

                            Storage.Phriction phrictionStorage = new Storage.Phriction();
                            string[] crumbs = document.Path.TrimEnd('/').Split('/');
                            string crumbDescriptions = "";
                            string currentPath = "";
                            foreach (string crumb in crumbs.Take(crumbs.Length - 1))
                            {
                                currentPath += crumb + "/";
                                Phabricator.Data.Phriction parentDocument = phrictionStorage.Get(database, currentPath, browser.Session.Locale);
                                if (parentDocument == null)
                                {
                                    string inexistantDocumentName = currentPath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                                    inexistantDocumentName = Char.ToUpper(inexistantDocumentName[0]) + inexistantDocumentName.Substring(1);
                                    crumbDescriptions += " > " + inexistantDocumentName;
                                }
                                else
                                {
                                    crumbDescriptions += " > " + parentDocument.Name;
                                }
                            }

                            if (string.IsNullOrEmpty(crumbDescriptions) == false)
                            {
                                crumbDescriptions = crumbDescriptions.Substring(" > ".Length)
                                                  + " > ";
                            }

                            crumbDescriptions += document.Name;

                            bool isNewDocument = document.Token.StartsWith("PHID-NEWTOKEN-");
                            if (isNewDocument && document.Path.Length > Phabricator.Data.Phriction.MaximumLengthSlug)
                            {
                                record.Issue = JsonRecordData.IssueType.SlugIsTooLong;
                            }
                            else
                            if (isNewDocument && document.Path.Length >= Phabricator.Data.Phriction.MaximumPreferredLengthSlug)
                            {
                                record.Issue = JsonRecordData.IssueType.SlugIsLong;
                            }
                            else
                            if (document.Language.Equals(Language.NotApplicable) == false)
                            {
                                record.Issue = JsonRecordData.IssueType.Translation;
                            }

                            record.Title = crumbDescriptions;
                            record.URL = "w/" + document.Path;
                            record.Type = "fa-book";
                        }
                        if (unknownToken.StartsWith(Phabricator.Data.Maniphest.Prefix))
                        {
                            Phabricator.Data.Maniphest task = JsonConvert.DeserializeObject<Phabricator.Data.Maniphest>(stageData.HeaderData);
                            record.Title = "T" + task.ID + ": " + task.Name;
                            record.URL = "maniphest/T" + task.ID + "/";
                            record.Type = "fa-anchor";
                        }
                        if (unknownToken.StartsWith(Phabricator.Data.File.Prefix))
                        {
                            Phabricator.Data.File file = JsonConvert.DeserializeObject<Phabricator.Data.File>(stageData.HeaderData);
                            record.IsTranslation = file.Language.Equals(Language.NotApplicable) == false;
                            if (record.IsTranslation)
                            {
                                record.Issue = JsonRecordData.IssueType.Translation;
                            }

                            record.Title = file.FileName;
                            record.Type = file.FontAwesomeIcon;

                            if (file.ContentType.Equals("image/drawio") && Http.Server.Plugins.Any(plugin => plugin.GetType().FullName.Equals("Phabrico.Plugin.DiagramsNet")))
                            {
                                record.URL = "diagrams.net/F" + file.ID + "/";
                            }
                            else
                            if (file.FileType == Phabricator.Data.File.FileStyle.Image && Http.Server.Plugins.Any(plugin => plugin.GetType().FullName.Equals("Phabrico.Plugin.JSPaintImageEditor")))
                            {
                                record.URL = "JSPaintImageEditor/F" + file.ID + "/";
                            }
                            else
                            {
                                record.URL = "file/data/" + file.ID + "/";
                            }
                        }

                        record.MergeConflict = stageData.MergeConflict;
                        record.Frozen = stageData.Frozen;
                        record.Token = stageData.Token;
                        record.Timestamp = FormatDateTimeOffset(stageData.DateModified, browser.Session.Locale);

                        tableRows.Add(record);
                    }

                    if (tableRows.Any(row => row.Issue == JsonRecordData.IssueType.SlugIsTooLong))
                    {
                        Http.Server.SendNotificationError("/offline/changes/notification", tableRows.Count.ToString());
                    }
                    else if (tableRows.Any(row => row.Issue == JsonRecordData.IssueType.SlugIsLong))
                    {
                        Http.Server.SendNotificationWarning("/offline/changes/notification", tableRows.Count.ToString());
                    }
                    else if (tableRows.Any())
                    {
                        Http.Server.SendNotificationInformation("/offline/changes/notification", tableRows.Count.ToString());
                    }
                    else
                    {
                        Http.Server.SendNotificationInformation("/offline/changes/notification", "");
                    }
                }
            }

            string jsonData = JsonConvert.SerializeObject(tableRows);
            jsonMessage = new JsonMessage(jsonData);
        }


        /// <summary>
        /// This method is fired via javascript when the navigator menu is visible.
        /// It is used to visualize the notification of the 'Offline changes' menuitem
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/offline/changes/count")]
        public void HttpGetCountUncommittedObjects(Http.Server httpServer, ref JsonMessage jsonMessage, string[] parameters, string parameterActions)
        {
            JsonRecordData.IssueType issueType = JsonRecordData.IssueType.None;
            int numberUncommittedObjects = 0;

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // set private encryption key
                database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                Storage.Stage stageStorage = new Storage.Stage();
                List<Storage.Stage.Data> uncommittedObjects = stageStorage.Get(database, Language.NotApplicable)
                                                              .ToList();
                uncommittedObjects.AddRange(stageStorage.Get(database, browser.Session.Locale)
                                            .Where(record => uncommittedObjects.Select(r => r.HeaderData)
                                                                          .Contains(record.HeaderData) == false
                                                  )
                                      );
                uncommittedObjects = uncommittedObjects.OrderByDescending(modification => modification.DateModified.ToUnixTimeSeconds())  // do not order by milliseconds
                                             .ThenBy(modification => modification.Operation)
                                             .ToList();

                Phabricator.Data.Phriction[] uncommittedPhrictionDocuments = stageStorage.Get<Phabricator.Data.Phriction>(database, Language.NotApplicable, false)
                                                                                         .Where(uncommittedObject => uncommittedObject != null)
                                                                                         .ToArray();

                numberUncommittedObjects = uncommittedObjects.Count;
                if (numberUncommittedObjects > 0)
                {
                    if (uncommittedPhrictionDocuments.Any(document => document.Path.Length >= Phabricator.Data.Phriction.MaximumLengthSlug))
                    {
                        issueType = JsonRecordData.IssueType.SlugIsTooLong;
                        Http.Server.SendNotificationError("/offline/changes/notification", numberUncommittedObjects.ToString());
                    }
                    else if (uncommittedPhrictionDocuments.Any(document => document.Path.Length >= Phabricator.Data.Phriction.MaximumPreferredLengthSlug))
                    {
                        issueType = JsonRecordData.IssueType.SlugIsLong;
                        Http.Server.SendNotificationWarning("/offline/changes/notification", numberUncommittedObjects.ToString());
                    }
                    else
                    {
                        Http.Server.SendNotificationInformation("/offline/changes/notification", numberUncommittedObjects.ToString());
                    }
                }
                else
                {
                    Http.Server.SendNotificationInformation("/offline/changes/notification", "");
                }
            }

            string jsonData = JsonConvert.SerializeObject(new {
                IssueType = Enum.GetName(typeof(JsonRecordData.IssueType), issueType),
                Count = numberUncommittedObjects
            });
            jsonMessage = new JsonMessage(jsonData);
        }

        /// <summary>
        /// This controller method is fired when the user clicks on the 'Freeze' button of an item in the Offline Changes overview
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        [UrlController(URL = "/offline/changes/freeze")]
        public void HttpPostFreeze(Http.Server httpServer, string[] parameters)
        {
            if (httpServer.Customization.HideOfflineChanges) throw new Phabrico.Exception.HttpNotFound("/offline/changes/freeze");

            Match input = RegexSafe.Match(browser.Session.FormVariables[browser.Request.RawUrl]["item"], @"^([^[]*)\[.*\]$", RegexOptions.None);
            string phabricatorObjectToken = input.Groups[1].Value;

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // set private encryption key
                database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                Storage.Stage stageStorage = new Storage.Stage();
                Phabricator.Data.Maniphest maniphestTask = stageStorage.Get<Phabricator.Data.Maniphest>(database, phabricatorObjectToken, browser.Session.Locale);
                Phabricator.Data.Phriction phrictionDocument = stageStorage.Get<Phabricator.Data.Phriction>(database, phabricatorObjectToken, browser.Session.Locale);
                Phabricator.Data.Transaction transactionPriorityChange = stageStorage.Get<Phabricator.Data.Transaction>(database, phabricatorObjectToken, "priority");
                Phabricator.Data.Transaction transactionStatusChange = stageStorage.Get<Phabricator.Data.Transaction>(database, phabricatorObjectToken, "status");
                Phabricator.Data.Transaction transactionOwnerChange = stageStorage.Get<Phabricator.Data.Transaction>(database, phabricatorObjectToken, "owner");
                Phabricator.Data.Transaction transactionCommentChange = stageStorage.Get<Phabricator.Data.Transaction>(database, phabricatorObjectToken, "comment");
                Phabricator.Data.Transaction transactionSubscriberChange = stageStorage.Get<Phabricator.Data.Transaction>(database, phabricatorObjectToken, "subscriber-0");
                Phabricator.Data.Transaction transactionProjectChange = stageStorage.Get<Phabricator.Data.Transaction>(database, phabricatorObjectToken, "project-0");
                List<int> referencedStagedFileIDs = new List<int>();
                Regex matchFileAttachments = new Regex("{F(-[0-9]+)[^}]*}");

                if (maniphestTask != null || transactionPriorityChange != null || transactionStatusChange != null || transactionOwnerChange != null || transactionCommentChange != null || transactionSubscriberChange != null || transactionProjectChange != null)
                {
                    if (maniphestTask == null)
                    {
                        Storage.Maniphest maniphestStorage = new Storage.Maniphest();
                        maniphestTask = maniphestStorage.Get(database, phabricatorObjectToken, browser.Session.Locale, true);
                    }

                    referencedStagedFileIDs.AddRange(matchFileAttachments.Matches(maniphestTask.Description)
                                                                         .OfType<Match>()
                                                                         .Select(match => Int32.Parse(match.Groups[1].Value))
                                                    );

                    // freeze task
                    stageStorage.Freeze(database, browser, maniphestTask.Token, true);
                }

                if (phrictionDocument != null)
                {
                    referencedStagedFileIDs.AddRange(matchFileAttachments.Matches(phrictionDocument.Content)
                                                                         .OfType<Match>()
                                                                         .Select(match => Int32.Parse(match.Groups[1].Value))
                                                    );

                    // freeze document
                    stageStorage.Freeze(database, browser, phrictionDocument.Token, true);
                }

                List<Phabricator.Data.File> stagedFiles = stageStorage.Get<Phabricator.Data.File>(database, browser.Session.Locale, false).ToList();
                Phabricator.Data.PhabricatorObject[] stagedObjects = stageStorage.Get<Phabricator.Data.Phriction>(database, browser.Session.Locale)
                                                                                    .OfType<Phabricator.Data.PhabricatorObject>()
                                                                                    .Concat(stageStorage.Get<Phabricator.Data.Maniphest>(database, browser.Session.Locale))
                                                                                    .ToArray();

                foreach (int referencedStagedFileID in referencedStagedFileIDs)
                {
                    foreach (Phabricator.Data.PhabricatorObject stagedObject in stagedObjects)
                    {
                        Phabricator.Data.Maniphest stagedManiphestTask = stagedObject as Phabricator.Data.Maniphest;
                        Phabricator.Data.Phriction stagedPhrictionDocument = stagedObject as Phabricator.Data.Phriction;

                        if (stagedManiphestTask != null)
                        {
                            if (matchFileAttachments.Matches(stagedManiphestTask.Description)
                                                                                .OfType<Match>()
                                                                                .Select(match => Int32.Parse(match.Groups[1].Value))
                                                                                .Contains(referencedStagedFileID)
                               )
                            {
                                // freeze (other) tasks where file is referenced in
                                stageStorage.Freeze(database, browser, stagedManiphestTask.Token, true);
                            }
                        }

                        if (stagedPhrictionDocument != null)
                        {
                            if (matchFileAttachments.Matches(stagedPhrictionDocument.Content)
                                                                                    .OfType<Match>()
                                                                                    .Select(match => Int32.Parse(match.Groups[1].Value))
                                                                                    .Contains(referencedStagedFileID)
                               )
                            {
                                // freeze (other) documents where file is referenced in
                                stageStorage.Freeze(database, browser, stagedPhrictionDocument.Token, true);
                            }
                        }
                    }

                    // freeze file
                    string fileToken = stagedFiles.FirstOrDefault(file => file.ID == referencedStagedFileID).Token;
                    stageStorage.Freeze(database, browser, fileToken, true);
                }
            }
        }

        /// <summary>
        /// This controller method is fired when the user clicks on the 'Unfreeze' button of an item in the Offline Changes overview
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        [UrlController(URL = "/offline/changes/unfreeze")]
        public void HttpPostUnfreeze(Http.Server httpServer, string[] parameters)
        {
            if (httpServer.Customization.HideOfflineChanges) throw new Phabrico.Exception.HttpNotFound("/offline/changes/unfreeze");

            Match input = RegexSafe.Match(browser.Session.FormVariables[browser.Request.RawUrl]["item"], @"^([^[]*)\[.*\]$", RegexOptions.None);
            string phabricatorObjectToken = input.Groups[1].Value;

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // set private encryption key
                database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                Storage.Stage stageStorage = new Storage.Stage();
                Phabricator.Data.Maniphest maniphestTask = stageStorage.Get<Phabricator.Data.Maniphest>(database, phabricatorObjectToken, browser.Session.Locale);
                Phabricator.Data.Phriction phrictionDocument = stageStorage.Get<Phabricator.Data.Phriction>(database, phabricatorObjectToken, browser.Session.Locale);
                Phabricator.Data.Transaction transactionPriorityChange = stageStorage.Get<Phabricator.Data.Transaction>(database, phabricatorObjectToken, "priority");
                Phabricator.Data.Transaction transactionStatusChange = stageStorage.Get<Phabricator.Data.Transaction>(database, phabricatorObjectToken, "status");
                Phabricator.Data.Transaction transactionOwnerChange = stageStorage.Get<Phabricator.Data.Transaction>(database, phabricatorObjectToken, "owner");
                Phabricator.Data.Transaction transactionCommentChange = stageStorage.Get<Phabricator.Data.Transaction>(database, phabricatorObjectToken, "comment");
                Phabricator.Data.Transaction transactionSubscriberChange = stageStorage.Get<Phabricator.Data.Transaction>(database, phabricatorObjectToken, "subscriber-0");
                Phabricator.Data.Transaction transactionProjectChange = stageStorage.Get<Phabricator.Data.Transaction>(database, phabricatorObjectToken, "project-0");
                List<int> referencedStagedFileIDs = new List<int>();
                Regex matchFileAttachments = new Regex("{F(-[0-9]+)[^}]*}");

                if (maniphestTask != null || transactionPriorityChange != null || transactionStatusChange != null || transactionOwnerChange != null || transactionCommentChange != null || transactionSubscriberChange != null || transactionProjectChange != null)
                {
                    if (maniphestTask == null)
                    {
                        Storage.Maniphest maniphestStorage = new Storage.Maniphest();
                        maniphestTask = maniphestStorage.Get(database, phabricatorObjectToken, browser.Session.Locale, true);
                    }

                    referencedStagedFileIDs.AddRange(matchFileAttachments.Matches(maniphestTask.Description)
                                                                         .OfType<Match>()
                                                                         .Select(match => Int32.Parse(match.Groups[1].Value))
                                                    );

                    // unfreeze task
                    stageStorage.Freeze(database, browser, maniphestTask.Token, false);
                }

                if (phrictionDocument != null)
                {
                    referencedStagedFileIDs.AddRange(matchFileAttachments.Matches(phrictionDocument.Content)
                                                                         .OfType<Match>()
                                                                         .Select(match => Int32.Parse(match.Groups[1].Value))
                                                    );

                    // unfreeze document
                    stageStorage.Freeze(database, browser, phrictionDocument.Token, false);
                }

                List<Phabricator.Data.File> stagedFiles = stageStorage.Get<Phabricator.Data.File>(database, browser.Session.Locale, false).ToList();
                Phabricator.Data.PhabricatorObject[] stagedObjects = stageStorage.Get<Phabricator.Data.Phriction>(database, browser.Session.Locale)
                                                                                    .OfType<Phabricator.Data.PhabricatorObject>()
                                                                                    .Concat(stageStorage.Get<Phabricator.Data.Maniphest>(database, browser.Session.Locale))
                                                                                    .ToArray();

                foreach (int referencedStagedFileID in referencedStagedFileIDs)
                {
                    foreach (Phabricator.Data.PhabricatorObject stagedObject in stagedObjects)
                    {
                        Phabricator.Data.Maniphest stagedManiphestTask = stagedObject as Phabricator.Data.Maniphest;
                        Phabricator.Data.Phriction stagedPhrictionDocument = stagedObject as Phabricator.Data.Phriction;

                        if (stagedManiphestTask != null)
                        {
                            if (matchFileAttachments.Matches(stagedManiphestTask.Description)
                                                                                .OfType<Match>()
                                                                                .Select(match => Int32.Parse(match.Groups[1].Value))
                                                                                .Contains(referencedStagedFileID)
                               )
                            {
                                // unfreeze (other) tasks where file is referenced in
                                stageStorage.Freeze(database, browser, stagedManiphestTask.Token, false);
                            }
                        }

                        if (stagedPhrictionDocument != null)
                        {
                            if (matchFileAttachments.Matches(stagedPhrictionDocument.Content)
                                                                                    .OfType<Match>()
                                                                                    .Select(match => Int32.Parse(match.Groups[1].Value))
                                                                                    .Contains(referencedStagedFileID)
                               )
                            {
                                // unfreeze (other) documents where file is referenced in
                                stageStorage.Freeze(database, browser, stagedPhrictionDocument.Token, false);
                            }
                        }
                    }

                    // freeze file
                    string fileToken = stagedFiles.FirstOrDefault(file => file.ID == referencedStagedFileID).Token;
                    stageStorage.Freeze(database, browser, fileToken, false);
                }
            }
        }

        /// <summary>
        /// When a slug (= a URL path to a Phriction document) for a staged Phriction document is too long
        /// to be stored in Phabricator, the Offline Changes will show a red or yellow icon in front of the title.
        /// A red icon means that the document can definitely not be stored in Phabricator.
        /// A yellow icon means that the document can still be stored in Phabricator, but that any new underlying documents
        /// may not.
        /// When the user clicks on the red or yellow icon, a dialog appears in which the user can rename the slug.
        /// This method will be executed when the user confirms the renewal action.
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/offline/changes/rename/slug")]
        public JsonMessage HttpPostRenamePhrictionSlug(Http.Server httpServer, string[] parameters)
        {
            string jsonData;
            if (httpServer.Customization.HideOfflineChanges && httpServer.Customization.HidePhrictionChanges) throw new Phabrico.Exception.HttpNotFound("/offline/changes/undo");

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");
            
            string oldSlug = browser.Session.FormVariables[browser.Request.RawUrl]["old"];
            string newSlug = browser.Session.FormVariables[browser.Request.RawUrl]["new"].TrimEnd('/') + "/";

            try
            {
                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    // set private encryption key
                    database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                    Storage.Stage stageStorage = new Storage.Stage();
                    Storage.Phriction phrictionStorage = new Storage.Phriction();
                    Phabricator.Data.Phriction existingPhrictionDocument = phrictionStorage.Get(database, newSlug, Language.NotApplicable, false);
                    if (existingPhrictionDocument == null)
                    {
                        Phabricator.Data.Phriction[] phrictionDocumentsToBeRenamed = stageStorage.Get<Phabricator.Data.Phriction>(database, Language.NotApplicable)
                                                                                                 .Where(wiki => wiki.Path.StartsWith(oldSlug))
                                                                                                 .ToArray();
                        foreach (Phabricator.Data.Phriction phrictionDocumentToBeRenamed in phrictionDocumentsToBeRenamed)
                        {
                            // prepend "\x01" to make sure we only replace the first part of the string (and not any duplicated tokens)
                            phrictionDocumentToBeRenamed.Path = ("\x01" + phrictionDocumentToBeRenamed.Path).Replace("\x01" + oldSlug, newSlug);
                            stageStorage.Modify(database, phrictionDocumentToBeRenamed, browser);
                        }

                        jsonData = JsonConvert.SerializeObject(new
                        {
                            Status = "OK"
                        });
                    }
                    else
                    {
                        jsonData = JsonConvert.SerializeObject(new
                        {
                            Status = "AlreadyExists"
                        });
                    }
                }
            }
            catch
            {
                jsonData = JsonConvert.SerializeObject(new
                {
                    Status = "NOK"
                });
            }

            return new JsonMessage(jsonData);
        }

        /// <summary>
        /// This controller method is fired when the user clicks on the 'Undo' button of an item in the Offline Changes overview
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        [UrlController(URL = "/offline/changes/undo")]
        public void HttpPostUndo(Http.Server httpServer, string[] parameters)
        {
            if (httpServer.Customization.HideOfflineChanges && httpServer.Customization.HidePhrictionChanges) throw new Phabrico.Exception.HttpNotFound("/offline/changes/undo");

            Match input = RegexSafe.Match(browser.Session.FormVariables[browser.Request.RawUrl]["item"], @"^([^[]*)\[(.*)\]$", RegexOptions.None);
            string phabricatorObjectToken = input.Groups[1].Value;
            string operation = input.Groups[2].Value;

            Language language = Language.NotApplicable;
            if (browser.Session.FormVariables[browser.Request.RawUrl].ContainsKey("use-local-language"))
            {
                language = browser.Session.Locale;
            }

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");

            if (phabricatorObjectToken.StartsWith("PHID-NEWTOKEN-"))
            {
                operation = "new";
            }

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // set private encryption key
                database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                Storage.Stage stageStorage = new Storage.Stage();
                Phabricator.Data.PhabricatorObject phabricatorObject = stageStorage.Get<Phabricator.Data.PhabricatorObject>(database, phabricatorObjectToken, browser.Session.Locale, operation);
                if (phabricatorObject != null)
                {
                    stageStorage.Remove(database, browser, phabricatorObject, language);
                    
                    Regex matchFileAttachments = new Regex("{F(-[0-9]+)[^}]*}");
                    List<int> unreferencedStagedFileIDs = new List<int>();
                    Phabricator.Data.Maniphest maniphestTask = phabricatorObject as Phabricator.Data.Maniphest;
                    Phabricator.Data.Phriction phrictionDocument = phabricatorObject as Phabricator.Data.Phriction;
                    Phabricator.Data.File fileObject = phabricatorObject as Phabricator.Data.File;

                    if (maniphestTask != null && maniphestTask.Description != null)
                    {
                        unreferencedStagedFileIDs.AddRange(matchFileAttachments.Matches(maniphestTask.Description)
                                                                               .OfType<Match>()
                                                                               .Select(match => Int32.Parse(match.Groups[1].Value))
                                                          );
                    }

                    if (phrictionDocument != null && phrictionDocument.Content != null)
                    {
                        unreferencedStagedFileIDs.AddRange(matchFileAttachments.Matches(phrictionDocument.Content)
                                                                               .OfType<Match>()
                                                                               .Select(match => Int32.Parse(match.Groups[1].Value))
                                                          );
                    }

                    if (maniphestTask != null || phrictionDocument != null)
                    {
                        foreach (Phabricator.Data.PhabricatorObject stagedObject in stageStorage.Get<Phabricator.Data.PhabricatorObject>(database, browser.Session.Locale))
                        {
                            maniphestTask = stagedObject as Phabricator.Data.Maniphest;
                            phrictionDocument = stagedObject as Phabricator.Data.Phriction;

                            if (maniphestTask != null)
                            {
                                unreferencedStagedFileIDs.RemoveAll(referencedFileID => matchFileAttachments.Matches(maniphestTask.Description)
                                                                                                            .OfType<Match>()
                                                                                                            .Select(match => Int32.Parse(match.Groups[1].Value))
                                                                                                            .Contains(referencedFileID)
                                                                   );
                            }

                            if (phrictionDocument != null)
                            {
                                unreferencedStagedFileIDs.RemoveAll(referencedFileID => matchFileAttachments.Matches(phrictionDocument.Content)
                                                                                                            .OfType<Match>()
                                                                                                            .Select(match => Int32.Parse(match.Groups[1].Value))
                                                                                                            .Contains(referencedFileID)
                                                                   );
                            }
                        }

                        foreach (int unreferencedStagedFileID in unreferencedStagedFileIDs)
                        {
                            Phabricator.Data.File unreferencedStageFile = stageStorage.Get<Phabricator.Data.File>(database, browser.Session.Locale, Phabricator.Data.File.Prefix, unreferencedStagedFileID, false);
                            if (unreferencedStageFile != null)
                            {
                                stageStorage.Remove(database, browser, unreferencedStageFile, language);
                            }
                        }
                    }

                    if (fileObject != null)
                    {
                        Storage.Maniphest maniphestStorage = new Storage.Maniphest();
                        Storage.Phriction phrictionStorage = new Storage.Phriction();
                        Storage.Stage stagStorage = new Storage.Stage();
                        Content content = new Content(database);
                        Phabricator.Data.File originalFile = null;
                        if (fileObject.OriginalID > 0)
                        {
                            Storage.File fileStorage = new Storage.File();
                            originalFile = fileStorage.GetByID(database, fileObject.OriginalID, true);
                        }
                        else
                        if (fileObject.OriginalID < 0)
                        {
                            originalFile = stageStorage.Get<Phabricator.Data.File>(database, Language.NotApplicable, Phabricator.Data.File.Prefix, fileObject.OriginalID, false);
                        }

                        if (originalFile != null)
                        {
                            foreach (Phabricator.Data.PhabricatorObject dependentObject in database.GetDependentObjects(originalFile.Token, browser.Session.Locale).ToArray())
                            {
                                maniphestTask = dependentObject as Phabricator.Data.Maniphest;
                                phrictionDocument = dependentObject as Phabricator.Data.Phriction;

                                if (maniphestTask != null)
                                {
                                    foreach (Match match in matchFileAttachments.Matches(maniphestTask.Description)
                                                                                .OfType<Match>()
                                                                                .Where(m => m.Groups[1].Value.Equals(fileObject.ID.ToString()))
                                                                                .OrderByDescending(m => m.Index)
                                                                                .ToArray()
                                            )
                                    {
                                        maniphestTask.Description = maniphestTask.Description.Substring(0, match.Groups[1].Index)
                                                                  + originalFile.ID
                                                                  + maniphestTask.Description.Substring(match.Groups[1].Index + match.Groups[1].Length);

                                        if (maniphestTask.Token.StartsWith("PHID-NEWTOKEN-"))
                                        {
                                            stageStorage.Modify(database, maniphestTask, browser);
                                        }
                                        else
                                        {
                                            maniphestStorage.Add(database, maniphestTask);
                                        }

                                        httpServer.InvalidateNonStaticCache("/maniphest/T" + maniphestTask.ID);
                                    }

                                    continue;
                                }

                                if (phrictionDocument != null)
                                {
                                    foreach (Match match in matchFileAttachments.Matches(phrictionDocument.Content)
                                                                                .OfType<Match>()
                                                                                .Where(m => m.Groups[1].Value.Equals(fileObject.ID.ToString()))
                                                                                .OrderByDescending(m => m.Index)
                                                                                .ToArray()
                                            )
                                    {
                                        phrictionDocument.Content = phrictionDocument.Content.Substring(0, match.Groups[1].Index)
                                                                  + originalFile.ID
                                                                  + phrictionDocument.Content.Substring(match.Groups[1].Index + match.Groups[1].Length);

                                        if (phrictionDocument.Token.StartsWith("PHID-NEWTOKEN-"))
                                        {
                                            stageStorage.Modify(database, phrictionDocument, browser);
                                        }
                                        else
                                        if (phabricatorObject.Language.Equals(Language.NotApplicable))
                                        {
                                            phrictionStorage.Add(database, phrictionDocument);
                                        }
                                        else
                                        {
                                            content.AddTranslation(phrictionDocument.Token, phabricatorObject.Language, phrictionDocument.Name, phrictionDocument.Content);
                                            stageStorage.Remove(database, browser, phabricatorObject, phabricatorObject.Language);
                                        }

                                        httpServer.InvalidateNonStaticCache(phrictionDocument.Path);
                                    }

                                    continue;
                                }
                            }
                        }
                        else
                        if (fileObject.OriginalID == 0)
                        {
                            foreach (Phabricator.Data.PhabricatorObject dependentObject in database.GetDependentObjects(fileObject.Token, browser.Session.Locale).ToArray())
                            {
                                maniphestTask = dependentObject as Phabricator.Data.Maniphest;
                                phrictionDocument = dependentObject as Phabricator.Data.Phriction;

                                if (maniphestTask != null)
                                {
                                    foreach (Match match in matchFileAttachments.Matches(maniphestTask.Description)
                                                                                .OfType<Match>()
                                                                                .Where(m => m.Groups[1].Value.Equals(fileObject.ID.ToString()))
                                                                                .OrderByDescending(m => m.Index)
                                                                                .ToArray()
                                            )
                                    {
                                        // remove file reference
                                        maniphestTask.Description = maniphestTask.Description.Substring(0, match.Index)
                                                                  + maniphestTask.Description.Substring(match.Index + match.Length);

                                        if (maniphestTask.Token.StartsWith("PHID-NEWTOKEN-"))
                                        {
                                            stageStorage.Modify(database, maniphestTask, browser);
                                        }
                                        else
                                        {
                                            maniphestStorage.Add(database, maniphestTask);
                                        }

                                        httpServer.InvalidateNonStaticCache("/maniphest/T" + maniphestTask.ID);
                                    }

                                    continue;
                                }

                                if (phrictionDocument != null)
                                {
                                    foreach (Match match in matchFileAttachments.Matches(phrictionDocument.Content)
                                                                                .OfType<Match>()
                                                                                .Where(m => m.Groups[1].Value.Equals(fileObject.ID.ToString()))
                                                                                .OrderByDescending(m => m.Index)
                                                                                .ToArray()
                                            )
                                    {
                                        // remove file reference
                                        phrictionDocument.Content = phrictionDocument.Content.Substring(0, match.Index)
                                                                  + phrictionDocument.Content.Substring(match.Index + match.Length);

                                        if (phrictionDocument.Token.StartsWith("PHID-NEWTOKEN-"))
                                        {
                                            stageStorage.Modify(database, phrictionDocument, browser);
                                        }
                                        else
                                        if (phabricatorObject.Language.Equals(Language.NotApplicable))
                                        {
                                            phrictionStorage.Add(database, phrictionDocument);
                                        }
                                        else
                                        {
                                            content.AddTranslation(phrictionDocument.Token, phabricatorObject.Language, phrictionDocument.Name, phrictionDocument.Content);
                                            stageStorage.Remove(database, browser, phabricatorObject, phabricatorObject.Language);
                                        }

                                        httpServer.InvalidateNonStaticCache(phrictionDocument.Path);
                                    }

                                    continue;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// This controller method is fired when the user clicks on the 'Save local version' button in the StagingDiff view (Offline changes screen)
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        [UrlController(URL = "/offline/changes/view", HtmlViewPageOptions = HtmlViewPage.ContentOptions.HideGlobalTreeView)]
        public void HttpPostSaveChanges(Http.Server httpServer, string[] parameters)
        {
            if (httpServer.Customization.HideOfflineChanges) throw new Phabrico.Exception.HttpNotFound("/offline/changes/view");

            if (parameters.Length >= 2)
            {
                if (parameters[1].StartsWith("?action=save"))
                {
                    string phabricatorObjectToken = parameters[0];
                    string newContent;

                    if (browser.Session.FormVariables[browser.Request.RawUrl].TryGetValue("newVersion", out newContent))
                    {
                        using (Storage.Database database = new Storage.Database(EncryptionKey))
                        {
                            // set private encryption key
                            database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                            Storage.Stage stageStorage = new Storage.Stage();

                            if (phabricatorObjectToken.StartsWith(Phabricator.Data.Phriction.Prefix))
                            {
                                Phabricator.Data.Phriction phrictionDocument = stageStorage.Get<Phabricator.Data.Phriction>(database, phabricatorObjectToken, browser.Session.Locale);
                                if (phrictionDocument != null)
                                {
                                    phrictionDocument.Content = newContent;

                                    stageStorage.Modify(database, phrictionDocument, browser);
                                }

                                return;
                            }

                            if (phabricatorObjectToken.StartsWith(Phabricator.Data.Maniphest.Prefix))
                            {
                                Phabricator.Data.Maniphest maniphestTask = stageStorage.Get<Phabricator.Data.Maniphest>(database, phabricatorObjectToken, browser.Session.Locale);
                                if (maniphestTask != null)
                                {
                                    maniphestTask.Description = newContent;

                                    stageStorage.Modify(database, maniphestTask, browser);
                                }

                                return;
                            }
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// This controller method is fired when the StagingDiff view is opened.
        /// This view is opened when the user clicks on the 'View Changes' button in the Offline Changes screeen
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="viewPage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/offline/changes/view", HtmlViewPageOptions = HtmlViewPage.ContentOptions.HideGlobalTreeView)]
        public void HttpGetViewChanges(Http.Server httpServer, ref HtmlViewPage viewPage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.HideOfflineChanges && httpServer.Customization.HidePhrictionChanges) throw new Phabrico.Exception.HttpNotFound("/offline/changes/view");
            if (parameters.Any() == false) throw new Phabrico.Exception.AccessDeniedException("/offline/changes/view", "invalid url");

            string title = "";
            string url = "";
            string originalText = "";
            string modifiedText = "";
            string phabricatorObjectToken = parameters[0];

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                Storage.Stage stageStorage = new Storage.Stage();
                Phabricator.Data.PhabricatorObject modifiedPhabricatorObject = stageStorage.Get<Phabricator.Data.PhabricatorObject>(database, phabricatorObjectToken, browser.Session.Locale);
                Phabricator.Data.Phriction modifiedPhrictionDocument = modifiedPhabricatorObject as Phabricator.Data.Phriction;
                Phabricator.Data.Maniphest modifiedManiphestTask = modifiedPhabricatorObject as Phabricator.Data.Maniphest;

                if (modifiedPhrictionDocument != null)
                {
                    Storage.Phriction phrictionStorage = new Storage.Phriction();
                    Phabricator.Data.Phriction originalPhrictionDocument = phrictionStorage.Get(database, phabricatorObjectToken, browser.Session.Locale, true);
                    if (originalPhrictionDocument == null)
                    {
                        // IMPOSSIBLE: should not happen
                        return;
                    }

                    Content content = new Content(database);
                    Content.Translation translation = content.GetTranslation(originalPhrictionDocument.Token, browser.Session.Locale);
                    if (translation == null || modifiedPhrictionDocument.Language.Equals(browser.Session.Locale) == false)
                    {
                        originalText = originalPhrictionDocument.Content;
                    }
                    else
                    {
                        originalText = translation.TranslatedRemarkup;
                    }

                    modifiedText = modifiedPhrictionDocument.Content;
                    title = modifiedPhrictionDocument.Name;
                    url = "w/" + modifiedPhrictionDocument.Path;
                }

                if (modifiedManiphestTask != null)
                {
                    Storage.Maniphest maniphestStorage = new Storage.Maniphest();
                    Phabricator.Data.Maniphest originalManiphestTask = maniphestStorage.Get(database, phabricatorObjectToken, browser.Session.Locale);
                    if (originalManiphestTask == null)
                    {
                        // IMPOSSIBLE: should not happen
                        return;
                    }

                    originalText = originalManiphestTask.Description;
                    modifiedText = modifiedManiphestTask.Description;
                    title = modifiedManiphestTask.Name;
                    url = "maniphest/T" + modifiedManiphestTask.ID + "/";
                }
            }

            if (originalText == null || modifiedText == null) throw new Phabrico.Exception.AccessDeniedException("/offline/changes/view", "invalid url");
            Diff.GenerateDiffLeftRight(ref originalText, ref modifiedText, false, browser.Session.Locale);

            viewPage = new Http.Response.HtmlViewPage(httpServer, browser, true, "StagingDiff", null);
            viewPage.SetText("ITEM", title);
            viewPage.SetText("URL", url);
            viewPage.SetText("CONTENT-LEFT", originalText, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
            viewPage.SetText("CONTENT-RIGHT", modifiedText, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
        }
    }
}
