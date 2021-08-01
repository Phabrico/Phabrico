using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Phabrico.Http;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;

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
        /// <param name="browser"></param>
        /// <param name="viewPage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/offline/changes")]
        public void HttpGetLoadParameters(Http.Server httpServer, Browser browser, ref HtmlViewPage viewPage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.HideOfflineChanges) throw new Phabrico.Exception.HttpNotFound();

            SessionManager.Token token = SessionManager.GetToken(browser);

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                Storage.Stage stage = new Storage.Stage();
                HtmlPartialViewPage dataViewPage = null;
                viewPage = new Http.Response.HtmlViewPage(httpServer, browser, true, "Staging", null);

                foreach (Storage.Stage.Data stageData in stage.Get(database).OrderByDescending(modification => modification.DateModified))
                {
                    string unknownToken = stageData.Token;

                    if (unknownToken.StartsWith("PHID-NEWTOKEN"))
                    {
                        JObject deserializedObject = JsonConvert.DeserializeObject(stageData.HeaderData) as JObject;
                        if (deserializedObject != null)
                        {
                            unknownToken = (string)deserializedObject["TokenPrefix"];
                        }
                    }

                    if (dataViewPage == null)
                    {
                        dataViewPage = viewPage.GetPartialView("DATA-VIEWPAGE");
                    }
                    break;
                }

                if (dataViewPage == null)
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
        /// <param name="browser"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/offline/changes/data")]
        public void HttpGetPopulateTableData(Http.Server httpServer, Browser browser, ref JsonMessage jsonMessage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.HideOfflineChanges) throw new Phabrico.Exception.HttpNotFound();

            List<JsonRecordData> tableRows = new List<JsonRecordData>();

            Storage.Stage stage = new Storage.Stage();
            if (stage != null)
            {
                SessionManager.Token token = SessionManager.GetToken(browser);

                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    Storage.Project projectStorage = new Storage.Project();
                    Storage.User userStorage = new Storage.User();

                    foreach (Storage.Stage.Data stageData in stage.Get(database)
                                                                  .OrderByDescending(modification => modification.DateModified.ToUnixTimeSeconds())  // do not order by milliseconds
                                                                  .ThenBy(modification => modification.Operation))
                    {
                        JsonRecordData record = new JsonRecordData();
                        string unknownToken = stageData.Token;

                        record.Title = "?!?";
                        record.URL = "";
                        record.Type = "";
                        record.TransactionModifier = "";
                        record.TransactionText = "";

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
                                    Phabricator.Data.Maniphest maniphest = maniphestStorage.Get(database, unknownToken);
                                    stageData.HeaderData = JsonConvert.SerializeObject(maniphest);

                                    Match inputTagsParameter = RegexSafe.Match(record.TransactionModifier, "^(project|subscriber)-[0-9]*$", RegexOptions.None);
                                    if (inputTagsParameter.Success)
                                    {
                                        string inputTagValue = maniphest.Transactions.FirstOrDefault(tran => tran.Type.Equals(record.TransactionModifier))?.NewValue;

                                        switch (inputTagsParameter.Groups[1].Value)
                                        {
                                            case "project":
                                                Phabricator.Data.Project project = projectStorage.Get(database, inputTagValue);
                                                record.TransactionText = Locale.TranslateText("Project @@PROJECT@@ tagged", browser.Session.Locale).Replace("@@PROJECT@@", project.Name);
                                                break;

                                            case "subscriber":
                                                Phabricator.Data.User subscriberUser = userStorage.Get(database, inputTagValue);
                                                if (subscriberUser != null)
                                                {
                                                    record.TransactionText = Locale.TranslateText("Subscriber @@SUBSCRIBER@@ added", browser.Session.Locale).Replace("@@SUBSCRIBER@@", subscriberUser.RealName);
                                                }
                                                else
                                                {
                                                    Phabricator.Data.Project subscriberProject = projectStorage.Get(database, inputTagValue);
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

                            Storage.Phriction phrictionStorage = new Storage.Phriction();
                            string[] crumbs = document.Path.TrimEnd('/').Split('/');
                            string crumbDescriptions = "";
                            string currentPath = "";
                            foreach (string crumb in crumbs.Take(crumbs.Length - 1))
                            {
                                currentPath += crumb + "/";
                                Phabricator.Data.Phriction parentDocument = phrictionStorage.Get(database, currentPath);
                                crumbDescriptions += " > " + parentDocument.Name;
                            }

                            if (string.IsNullOrEmpty(crumbDescriptions) == false)
                            {
                                crumbDescriptions = crumbDescriptions.Substring(" > ".Length)
                                                  + " > ";
                            }

                            crumbDescriptions += document.Name;

                            record.Title = crumbDescriptions;
                            record.URL = "w/" + document.Path;
                            record.Type = "fa-book";
                        }
                        if (unknownToken.StartsWith(Phabricator.Data.Maniphest.Prefix))
                        {
                            Phabricator.Data.Maniphest task = JsonConvert.DeserializeObject<Phabricator.Data.Maniphest>(stageData.HeaderData);
                            record.Title = "T" + task.ID.ToString() + ": " + task.Name;
                            record.URL = "maniphest/T" + task.ID.ToString() + "/";
                            record.Type = "fa-anchor";
                        }
                        if (unknownToken.StartsWith(Phabricator.Data.File.Prefix))
                        {
                            Phabricator.Data.File file = JsonConvert.DeserializeObject<Phabricator.Data.File>(stageData.HeaderData);
                            
                            record.Title = file.FileName;
                            record.Type = file.FontAwesomeIcon;

                            if (file.ContentType.Equals("image/drawio") && Http.Server.Plugins.Any(plugin => plugin.GetType().FullName.Equals("Phabrico.Plugin.DiagramsNet")))
                            {
                                record.URL = "diagrams.net/F" + file.ID + "/";
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
                }
            }

            string jsonData = JsonConvert.SerializeObject(tableRows);
            jsonMessage = new JsonMessage(jsonData);
        }
        
        /// <summary>
        /// This controller method is fired when the user clicks on the 'Freeze' button of an item in the Offline Changes overview
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="parameters"></param>
        [UrlController(URL = "/offline/changes/freeze")]
        public void HttpPostFreeze(Http.Server httpServer, Browser browser, string[] parameters)
        {
            if (httpServer.Customization.HideOfflineChanges) throw new Phabrico.Exception.HttpNotFound();

            Match input = RegexSafe.Match(browser.Session.FormVariables["item"], @"^([^[]*)\[.*\]$", RegexOptions.None);
            string phabricatorObjectToken = input.Groups[1].Value;
            SessionManager.Token token = SessionManager.GetToken(browser);

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                Storage.Stage stageStorage = new Storage.Stage();
                Phabricator.Data.Maniphest maniphestTask = stageStorage.Get<Phabricator.Data.Maniphest>(database, phabricatorObjectToken);
                Phabricator.Data.Phriction phrictionDocument = stageStorage.Get<Phabricator.Data.Phriction>(database, phabricatorObjectToken);
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
                        maniphestTask = maniphestStorage.Get(database, phabricatorObjectToken, true);
                    }

                    referencedStagedFileIDs.AddRange(matchFileAttachments.Matches(maniphestTask.Description)
                                                                         .OfType<Match>()
                                                                         .Select(match => Int32.Parse(match.Groups[1].Value))
                                                    );

                    // freeze task
                    stageStorage.Freeze(database, maniphestTask.Token, true);
                }

                if (phrictionDocument != null)
                {
                    referencedStagedFileIDs.AddRange(matchFileAttachments.Matches(phrictionDocument.Content)
                                                                         .OfType<Match>()
                                                                         .Select(match => Int32.Parse(match.Groups[1].Value))
                                                    );

                    // freeze document
                    stageStorage.Freeze(database, phrictionDocument.Token, true);
                }

                List<Phabricator.Data.File> stagedFiles = stageStorage.Get<Phabricator.Data.File>(database, false).ToList();
                Phabricator.Data.PhabricatorObject[] stagedObjects = stageStorage.Get<Phabricator.Data.Phriction>(database)
                                                                                    .OfType<Phabricator.Data.PhabricatorObject>()
                                                                                    .Concat(stageStorage.Get<Phabricator.Data.Maniphest>(database))
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
                                stageStorage.Freeze(database, stagedManiphestTask.Token, true);
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
                                stageStorage.Freeze(database, stagedPhrictionDocument.Token, true);
                            }
                        }
                    }

                    // freeze file
                    string fileToken = stagedFiles.FirstOrDefault(file => file.ID == referencedStagedFileID).Token;
                    stageStorage.Freeze(database, fileToken, true);
                }
            }
        }

        /// <summary>
        /// This controller method is fired when the user clicks on the 'Unfreeze' button of an item in the Offline Changes overview
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="parameters"></param>
        [UrlController(URL = "/offline/changes/unfreeze")]
        public void HttpPostUnfreeze(Http.Server httpServer, Browser browser, string[] parameters)
        {
            if (httpServer.Customization.HideOfflineChanges) throw new Phabrico.Exception.HttpNotFound();

            Match input = RegexSafe.Match(browser.Session.FormVariables["item"], @"^([^[]*)\[.*\]$", RegexOptions.None);
            string phabricatorObjectToken = input.Groups[1].Value;
            SessionManager.Token token = SessionManager.GetToken(browser);

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                Storage.Stage stageStorage = new Storage.Stage();
                Phabricator.Data.Maniphest maniphestTask = stageStorage.Get<Phabricator.Data.Maniphest>(database, phabricatorObjectToken);
                Phabricator.Data.Phriction phrictionDocument = stageStorage.Get<Phabricator.Data.Phriction>(database, phabricatorObjectToken);
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
                        maniphestTask = maniphestStorage.Get(database, phabricatorObjectToken, true);
                    }

                    referencedStagedFileIDs.AddRange(matchFileAttachments.Matches(maniphestTask.Description)
                                                                         .OfType<Match>()
                                                                         .Select(match => Int32.Parse(match.Groups[1].Value))
                                                    );

                    // unfreeze task
                    stageStorage.Freeze(database, maniphestTask.Token, false);
                }

                if (phrictionDocument != null)
                {
                    referencedStagedFileIDs.AddRange(matchFileAttachments.Matches(phrictionDocument.Content)
                                                                         .OfType<Match>()
                                                                         .Select(match => Int32.Parse(match.Groups[1].Value))
                                                    );

                    // unfreeze document
                    stageStorage.Freeze(database, phrictionDocument.Token, false);
                }

                List<Phabricator.Data.File> stagedFiles = stageStorage.Get<Phabricator.Data.File>(database, false).ToList();
                Phabricator.Data.PhabricatorObject[] stagedObjects = stageStorage.Get<Phabricator.Data.Phriction>(database)
                                                                                    .OfType<Phabricator.Data.PhabricatorObject>()
                                                                                    .Concat(stageStorage.Get<Phabricator.Data.Maniphest>(database))
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
                                stageStorage.Freeze(database, stagedManiphestTask.Token, false);
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
                                stageStorage.Freeze(database, stagedPhrictionDocument.Token, false);
                            }
                        }
                    }

                    // freeze file
                    string fileToken = stagedFiles.FirstOrDefault(file => file.ID == referencedStagedFileID).Token;
                    stageStorage.Freeze(database, fileToken, false);
                }
            }
        }

        /// <summary>
        /// This controller method is fired when the user clicks on the 'Undo' button of an item in the Offline Changes overview
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="parameters"></param>
        [UrlController(URL = "/offline/changes/undo")]
        public void HttpPostUndo(Http.Server httpServer, Browser browser, string[] parameters)
        {
            if (httpServer.Customization.HideOfflineChanges && httpServer.Customization.HidePhrictionChanges) throw new Phabrico.Exception.HttpNotFound();

            Match input = RegexSafe.Match(browser.Session.FormVariables["item"], @"^([^[]*)\[(.*)\]$", RegexOptions.None);
            string phabricatorObjectToken = input.Groups[1].Value;
            string operation = input.Groups[2].Value;
            SessionManager.Token token = SessionManager.GetToken(browser);

            if (phabricatorObjectToken.StartsWith("PHID-NEWTOKEN-"))
            {
                operation = "new";
            }

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                Storage.Stage stageStorage = new Storage.Stage();
                Phabricator.Data.PhabricatorObject phabricatorObject = stageStorage.Get<Phabricator.Data.PhabricatorObject>(database, phabricatorObjectToken, operation);
                if (phabricatorObject != null)
                {
                    stageStorage.Remove(browser, database, phabricatorObject);
                    
                    Regex matchFileAttachments = new Regex("{F(-[0-9]+)[^}]*}");
                    List<int> unreferencedStagedFileIDs = new List<int>();
                    Phabricator.Data.Maniphest maniphestTask = phabricatorObject as Phabricator.Data.Maniphest;
                    Phabricator.Data.Phriction phrictionDocument = phabricatorObject as Phabricator.Data.Phriction;

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

                    foreach (Phabricator.Data.PhabricatorObject stagedObject in stageStorage.Get<Phabricator.Data.PhabricatorObject>(database))
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
                        Phabricator.Data.File unreferencedStageFile = stageStorage.Get<Phabricator.Data.File>(database, Phabricator.Data.File.Prefix, unreferencedStagedFileID, false);
                        if (unreferencedStageFile != null)
                        {
                            stageStorage.Remove(browser, database, unreferencedStageFile);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// This controller method is fired when the user clicks on the 'Save local version' button in the StagingDiff view (Offline changes screen)
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="parameters"></param>
        [UrlController(URL = "/offline/changes/view", HtmlViewPageOptions = HtmlViewPage.ContentOptions.HideGlobalTreeView)]
        public void HttpPostSaveChanges(Http.Server httpServer, Browser browser, string[] parameters)
        {
            if (httpServer.Customization.HideOfflineChanges) throw new Phabrico.Exception.HttpNotFound();

            if (parameters.Length >= 2)
            {
                if (parameters[1].StartsWith("?action=save"))
                {
                    string phabricatorObjectToken = parameters[0];
                    string newContent;

                    if (browser.Session.FormVariables.TryGetValue("newVersion", out newContent))
                    {
                        using (Storage.Database database = new Storage.Database(EncryptionKey))
                        {
                            Storage.Stage stageStorage = new Storage.Stage();

                            if (phabricatorObjectToken.StartsWith(Phabricator.Data.Phriction.Prefix))
                            {
                                Phabricator.Data.Phriction phrictionDocument = stageStorage.Get<Phabricator.Data.Phriction>(database, phabricatorObjectToken);
                                if (phrictionDocument != null)
                                {
                                    phrictionDocument.Content = newContent;

                                    stageStorage.Modify(database, phrictionDocument);
                                }

                                return;
                            }

                            if (phabricatorObjectToken.StartsWith(Phabricator.Data.Maniphest.Prefix))
                            {
                                Phabricator.Data.Maniphest maniphestTask = stageStorage.Get<Phabricator.Data.Maniphest>(database, phabricatorObjectToken);
                                if (maniphestTask != null)
                                {
                                    maniphestTask.Description = newContent;

                                    stageStorage.Modify(database, maniphestTask);
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
        /// <param name="browser"></param>
        /// <param name="viewPage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/offline/changes/view", HtmlViewPageOptions = HtmlViewPage.ContentOptions.HideGlobalTreeView)]
        public void HttpGetViewChanges(Http.Server httpServer, Browser browser, ref HtmlViewPage viewPage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.HideOfflineChanges && httpServer.Customization.HidePhrictionChanges) throw new Phabrico.Exception.HttpNotFound();

            string title = "";
            string url = "";
            string originalText = "";
            string modifiedText = "";
            string phabricatorObjectToken = parameters[0];

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                Storage.Stage stageStorage = new Storage.Stage();
                Phabricator.Data.PhabricatorObject modifiedPhabricatorObject = stageStorage.Get<Phabricator.Data.PhabricatorObject>(database, phabricatorObjectToken);
                Phabricator.Data.Phriction modifiedPhrictionDocument = modifiedPhabricatorObject as Phabricator.Data.Phriction;
                Phabricator.Data.Maniphest modifiedManiphestTask = modifiedPhabricatorObject as Phabricator.Data.Maniphest;

                if (modifiedPhrictionDocument != null)
                {
                    Storage.Phriction phrictionStorage = new Storage.Phriction();
                    Phabricator.Data.Phriction originalPhrictionDocument = phrictionStorage.Get(database, phabricatorObjectToken);
                    if (originalPhrictionDocument == null)
                    {
                        // IMPOSSIBLE: should not happen
                        return;
                    }

                    originalText = originalPhrictionDocument.Content;
                    modifiedText = modifiedPhrictionDocument.Content;
                    title = modifiedPhrictionDocument.Name;
                    url = "w/" + modifiedPhrictionDocument.Path;
                }

                if (modifiedManiphestTask != null)
                {
                    Storage.Maniphest maniphestStorage = new Storage.Maniphest();
                    Phabricator.Data.Maniphest originalManiphestTask = maniphestStorage.Get(database, phabricatorObjectToken);
                    if (originalManiphestTask == null)
                    {
                        // IMPOSSIBLE: should not happen
                        return;
                    }

                    originalText = originalManiphestTask.Description;
                    modifiedText = modifiedManiphestTask.Description;
                    title = modifiedManiphestTask.Name;
                    url = "maniphest/T" + modifiedManiphestTask.ID.ToString() + "/";
                }
            }

            Diff.GenerateDiffLeftRight(ref originalText, ref modifiedText, false, browser.Session.Locale);

            viewPage = new Http.Response.HtmlViewPage(httpServer, browser, true, "StagingDiff", null);
            viewPage.SetText("ITEM", title);
            viewPage.SetText("URL", url);
            viewPage.SetText("CONTENT-LEFT", originalText, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
            viewPage.SetText("CONTENT-RIGHT", modifiedText, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
        }
    }
}
