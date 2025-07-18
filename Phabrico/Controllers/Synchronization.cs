﻿using DocumentFormat.OpenXml.Wordprocessing;
using Newtonsoft.Json;
using Phabrico.Http;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;
using Phabrico.Parsers.Base64;
using Phabrico.Parsers.Remarkup;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static Phabrico.Phabricator.Data.Account;

namespace Phabrico.Controllers
{
    /// <summary>
    /// Controller which manages the synchronization process between Phabrico and Phabricator.
    /// Local (staging) changes at Phabrico site are uploaded to the Phabricator server
    /// New remote changes at the Phabricator server are downloaded into the Phabrico database
    /// </summary>
    public class Synchronization : Controller
    {
        /// <summary>
        /// subclass which is used as a static variable (SharedResource.Instance) to share data between the several Progress-methods.
        /// The shared data is mostly used to represent the progress bar during the synchronization progress
        /// </summary>
        private class SharedResource
        {
            private static SharedResource _instance = null;

            public double ProgressPercentage { get; set; }
            public string ProgressDescription { get; set; }
            public string ProgressState { get; set; }
            public string ProgressRequestData { get; set; }

            public static SharedResource Instance
            {
                get
                {
                    if (_instance == null)
                    {
                        _instance = new SharedResource();
                        _instance.ProgressPercentage = 0;
                        _instance.ProgressDescription = "";
                        _instance.ProgressState = "OK";
                        _instance.ProgressRequestData = "";
                    }

                    return _instance;
                }
            }
        }

        /// <summary>
        /// Model for table rows in the client backend
        /// </summary>
        public class JsonRecordData
        {
            /// <summary>
            /// Task or Document is newly created
            /// </summary>
            public bool IsNew { get; set; }

            /// <summary>
            /// Represents the 'Last modified by' column
            /// </summary>
            public string LastModifiedBy { get; set; }

            /// <summary>
            /// True if comments were added or status/owner/priority were modified (Maniphest Task only)
            /// </summary>
            public bool MetadataIsModified { get; set; } = false;

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
            /// Represents the Fontawesome icon in the 'Content' column
            /// </summary>
            public string Type { get; set; }

            /// <summary>
            /// Link to the modified document or task
            /// </summary>
            public string URL { get; set; }
        }

        /// <summary>
        /// subclass which contains some information about the expected duration of a Progress-method
        /// </summary>
        public class MethodProgress
        {
            /// <summary>
            /// The method to be executed
            /// <param name="synchronizationParameters"></param>
            /// <param name="processedDuration"></param>
            /// <param name="totalDuration"></param>
            /// </summary>
            public Action<SynchronizationParameters, int, int> Method { get; set; }

            /// <summary>
            /// Numeric value which represents the expected duration of the progress-method.
            /// This variable does not contain any specific duration.
            /// It is more or less a relative value compared to all the other MethodProgress classes (from the other Progress methods)
            /// </summary>
            public int DurationCoefficient { get; set; }
        }

        /// <summary>
        /// subclass which contains some data which is shared over the different Progress-methods.
        /// The whole synchronization process consists of several Progress-methods: for each data type (e.g. user, phriction, maniphest, ...)
        /// and for each action (download or upload) exists a Progress-method.
        /// Each Progress-method decides what text should be shown in the progress-bar, which is shown during the synchronization process.
        /// The Progress-method determines also which progression value should be shown
        /// </summary>
        public class SynchronizationParameters
        {
            /// <summary>
            /// Connection parameters to browser connection
            /// </summary>
            public Browser browser;

            /// <summary>
            /// Link to local database
            /// </summary>
            public Storage.Database database;

            /// <summary>
            /// The Phabricator account to who can download or upload to the Phabricator server
            /// It is the account to which the Phabricator token belongs to
            /// </summary>
            public Phabricator.Data.Account existingAccount;

            /// <summary>
            /// List of diagram id's per token
            /// In case a new or modified document or task contains a reference to a diagram, the diagram needs also to be downloaded.
            /// This dictionary is filled by the ProgressMethod_DownloadManiphestTasks and ProgressMethod_DownloadPhrictionDocuments methods and
            /// is read and cleared by the ProgressMethod_DownloadDiagramObjects method
            /// Key is the token of the owner (e.g. maniphest task or phriction document); Value is a list of all referenced diagram-id's
            /// </summary>
            public Dictionary<string, List<int>> diagramObjectsPerToken = new Dictionary<string, List<int>>();

            /// <summary>
            /// List of file id's per token
            /// In case a new or modified document or task contains a reference to a file (e.g. an image), the file needs also to be downloaded.
            /// This dictionary is filled by the ProgressMethod_DownloadManiphestTasks and ProgressMethod_DownloadPhrictionDocuments methods and
            /// is read and cleared by the ProgressMethod_DownloadFileObjects method
            /// Key is the token of the owner (e.g. maniphest task or phriction document); Value is a list of all referenced file-id's
            /// </summary>
            public Dictionary<string, List<int>> fileObjectsPerToken = new Dictionary<string, List<int>>();

            /// <summary>
            /// If set to true, Phabrico will only download the data from Phabricator since the last download
            /// </summary>
            public bool incrementalDownload = true;

            /// <summary>
            /// This variable can be set by the Commander.
            /// Before Phabrico will download all the content of wiki pages, it will first download all URL's.
            /// Afterwards it starts downloading the content of all those downloaded URL's.
            /// If this variable is set, the list of downloaded URL's will be filtered by its initialPath.
            /// In other words, any wiki URL that does not start with this initialPhrictionPath will not be downloaded.
            /// </summary>
            public string[] initialPhrictionPaths { get; set; } = new string[] { "" };

            /// <summary>
            /// This variable is internally used together with initialPhrictionPaths.
            /// In case you have more than 1 initialPhrictionPath configured, you will have more than 1 root page.
            /// This variable will contain the shortened root URLs for each each root page.
            /// Based on this content, a correct coverpage will be created
            /// </summary>
            public string[] initialPhrictionPathAliases = new string[] { "" };

            /// <summary>
            /// Timestamp when the latest download process was finished.
            /// This timestamp represents the last time that new/modified Phriction documents and/or Maniphest tasks were downloaded from Phabricator
            /// The difference between lastDownloadTimestamp and lastSynchronizationTimestamp, is that lastDownloadTimestamp is specifically used for filtering the
            /// results of Phabricator downloads by time for the second downloads (after the first download + upload)
            /// This way the 2nd download won't download the results again from the 1st download
            /// </summary>
            public DateTimeOffset lastDownloadTimestamp;

            /// <summary>
            /// Latest timestamp of a removal of a wiki document in Phabricator.
            /// It works together with the lastDownloadTimestamp variable: based on this timestamp, the starting timestamp for the next Phabricator sync can be determined
            /// </summary>
            public DateTimeOffset lastRemovalTimestamp;

            /// <summary>
            /// Timestamp of the latest the synchronization process was finished
            /// This timestamp represents the last time that the latest new users and new projects were downloaded from Phriction.
            /// This timestamp may also be reset when the synchronization selection of users and projects is changed (so that all of these may completely downloaded again)
            /// The difference between lastDownloadTimestamp and lastSynchronizationTimestamp, is that lastDownloadTimestamp is specifically used for filtering the
            /// results of Phabricator downloads by time for the second downloads (after the first download + upload)
            /// This way the 2nd download won't download the results again from the 1st download
            /// </summary>
            public DateTimeOffset lastSynchronizationTimestamp;

            /// <summary>
            /// Represents the style of the previous synchronization action: light or full
            /// A light synchronization is the first synchronization action executed ever: it will only download the users and projects from Phabricator
            /// A full synchronization will download everything from Phabricator (which was configured to be downloaded)
            /// </summary>
            public SyncMode previousSyncMode;

            /// <summary>
            /// List of projects which are or are not selected for download/upload
            /// </summary>
            public Dictionary<string, Phabricator.Data.Project.Selection> projectSelected = new Dictionary<string, Phabricator.Data.Project.Selection>();

            /// <summary>
            /// List of Phriction documents and/or Maniphest Tasks which could not be uploaded to the Phabricator server because
            /// the Phabricator server contained newer versions of them.
            /// This list will be shown after the synchronization process.
            /// </summary>
            public List<Phabricator.Data.PhabricatorObject> remotelyModifiedObjects = null;

            /// <summary>
            /// This flag is set to true after all the Upload progress methods are finished
            /// </summary>
            public bool stagedDataHasBeenUploaded { get; set; } = false;

            /// <summary>
            /// Calculated percentual length of 1 synchronization step
            /// It is used to visualize to progress bar in the browser while synchronizing
            /// </summary>
            public double stepSize;

            /// <summary>
            /// Translation table for new Phabrico file-references vs Phabricator file references.
            /// Before a phriction document or maniphest task is uploaded, its content will be checked first for
            /// file-references which don't exist yet in Phabricator.
            /// The new file, which is referenced in the content, will be uploaded first.
            /// Phabricator will return a new token and a new reference-number.
            /// After uploading the file, a new KeyValuePair will be added to this dictionary with key='Phabrico reference number'
            /// and value='Phabricator reference number'
            /// </summary>
            public Dictionary<int,int> uploadedFileReferences = new Dictionary<int, int>();

            /// <summary>
            /// List of user which are or are not selected for download/upload
            /// </summary>
            public Dictionary<string, bool> userSelected = new Dictionary<string, bool>();

            /// <summary>
            /// If set to true, the Storage.Users was initialized
            /// </summary>
            public bool UserNoneInitialized;

            /// <summary>
            /// Time difference between the Phabricator server and the local (Phabrico) computer.
            /// There shouldn't be any clock difference, but just in case...
            /// Just to be clear: time zone differences are not meant with this 'time difference'
            /// It's just for meant for times which are slightly off
            /// </summary>
            public TimeSpan TimeDifferenceBetweenPhabricatorAndLocalComputer;
        }

        /// <summary>
        /// Represents the style of the previous synchronization action: light or full
        /// A light synchronization is the first synchronization action executed ever: it will only download the users and projects from Phabricator
        /// A full synchronization will download everything from Phabricator (which was configured to be downloaded)
        /// </summary>
        public enum SyncMode
        {
            /// <summary>
            /// Represents the first synchronization action executed ever: it will only download the users and projects from Phabricator
            /// </summary>
            Light,

            /// <summary>
            /// Represents a download action from Phabricator which downloads everything (which was configured to be downloaded)
            /// </summary>
            Full
        }

        /// <summary>
        /// The synchronization process is executed in a separate thread.
        /// This ManualResetEvent checks that this thread can only be executed once.
        /// </summary>
        private static readonly ManualResetEvent evSynchronizationInProgress = new ManualResetEvent(false);

        /// <summary>
        /// Returns true if synchronization in progress
        /// </summary>
        public static bool InProgress
        {
            get
            {
                if (evSynchronizationInProgress.WaitOne(0))
                {
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Searches for diagram references in the given remarkup content
        /// </summary>
        /// <param name="tokenOwner">Token of Phabricator object to which the content belongs to</param>
        /// <param name="content">The remarkup content</param>
        /// <param name="diagramObjectsPerToken">Resulting list of referenced diagram-objects</param>
        private void CollectDiagramObjectsFromContent(string tokenOwner, string content, ref Dictionary<string, List<int>> diagramObjectsPerToken)
        {
            List<Match> diagramObjectReferences = RegexSafe.Matches(content, "{DIAG([0-9]+)([^}]*)}").OfType<Match>().OrderByDescending(match => match.Index).ToList();
            if (diagramObjectReferences.Any())
            {
                List<int> diagramTokenList;

                if (diagramObjectsPerToken.TryGetValue(tokenOwner, out diagramTokenList) == false)
                {
                    diagramTokenList = new List<int>();
                    diagramObjectsPerToken[tokenOwner] = diagramTokenList;
                }

                foreach (Match diagramObjectReference in diagramObjectReferences)
                {
                    diagramTokenList.Add(Int32.Parse(diagramObjectReference.Groups[1].Value));
                }
            }
        }

        /// <summary>
        /// Searches for file references in the given remarkup content
        /// </summary>
        /// <param name="tokenOwner">Token of Phabricator object to which the content belongs to</param>
        /// <param name="content">The remarkup content</param>
        /// <param name="fileObjectsPerToken">Resulting list of referenced fileobjects</param>
        private void CollectFileObjectsFromContent(string tokenOwner, string content, ref Dictionary<string, List<int>> fileObjectsPerToken)
        {
            List<Match> fileObjectReferences = RegexSafe.Matches(content, "{F([0-9]+)([^}]*)}").OfType<Match>().OrderByDescending(match => match.Index).ToList();
            if (fileObjectReferences.Any())
            {
                List<int> fileTokenList;

                if (fileObjectsPerToken.TryGetValue(tokenOwner, out fileTokenList) == false)
                {
                    fileTokenList = new List<int>();
                    fileObjectsPerToken[tokenOwner] = fileTokenList;
                }

                foreach (Match fileObjectReference in fileObjectReferences)
                {
                    fileTokenList.Add(Int32.Parse(fileObjectReference.Groups[1].Value));
                }
            }
        }

        /// <summary>
        /// Returns the timestamp based on the RemovalPeriod parameter
        /// </summary>
        /// <param name="removalPeriod"></param>
        /// <returns></returns>
        private DateTimeOffset GetOldestDateTimeOffsetToKeep(Phabricator.Data.Account.RemovalPeriod removalPeriod)
        {
            DateTimeOffset oldestTimestampToKeep = DateTimeOffset.UtcNow;
            switch (removalPeriod)
            {
                case Phabricator.Data.Account.RemovalPeriod.RemovalPeriod1Day:
                    oldestTimestampToKeep = oldestTimestampToKeep.AddDays(-1);
                    break;

                case Phabricator.Data.Account.RemovalPeriod.RemovalPeriod1Week:
                    oldestTimestampToKeep = oldestTimestampToKeep.AddDays(-7);
                    break;

                case Phabricator.Data.Account.RemovalPeriod.RemovalPeriod2Weeks:
                    oldestTimestampToKeep = oldestTimestampToKeep.AddDays(-14);
                    break;

                case Phabricator.Data.Account.RemovalPeriod.RemovalPeriod1Month:
                    oldestTimestampToKeep = oldestTimestampToKeep.AddMonths(-1);
                    break;

                case Phabricator.Data.Account.RemovalPeriod.RemovalPeriod3Months:
                    oldestTimestampToKeep = oldestTimestampToKeep.AddMonths(-3);
                    break;

                case Phabricator.Data.Account.RemovalPeriod.RemovalPeriod6Months:
                    oldestTimestampToKeep = oldestTimestampToKeep.AddMonths(-6);
                    break;

                case Phabricator.Data.Account.RemovalPeriod.RemovalPeriod1Year:
                    oldestTimestampToKeep = oldestTimestampToKeep.AddYears(-1);
                    break;

                case Phabricator.Data.Account.RemovalPeriod.RemovalPeriod10Years:
                    oldestTimestampToKeep = oldestTimestampToKeep.AddYears(-10);
                    break;

                default:
                    break;
            }

            return oldestTimestampToKeep;
        }

        /// <summary>
        /// This method is fired when opening the Latest synchronized data screen
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="viewPage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        /// <returns></returns>
        [UrlController(URL = "/synchronization/logging")]
        public void HttpGetLoadSyncLoggingScreen(Http.Server httpServer, ref HtmlViewPage viewPage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.MasterDataIsAccessible == false) throw new Phabrico.Exception.HttpNotFound("/synchronize/logging");

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException("/synchronization/logging", "session expired");
            if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/synchronization/logging", "You don't have sufficient rights to synchronize Phabrico with Phabricator");
        }

        /// <summary>
        /// This method is fired via javascript when the Synchronization Logging screen is opened or when the search filter has been changed.
        /// It will load all (modified) items and convert them in to a JSON array.
        /// This JSON array will be shown as a HTML table
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="resultHttpMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/synchronization/search")]
        public void HttpGetPopulateTableData(Http.Server httpServer, ref HttpMessage resultHttpMessage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.MasterDataIsAccessible == false) throw new Phabrico.Exception.HttpNotFound("/synchronize/search");

            List<JsonRecordData> tableRows = new List<JsonRecordData>();
            Storage.Maniphest maniphestStorage = new Storage.Maniphest();
            Storage.PhamePost phamePostStorage = new Storage.PhamePost();
            Storage.Phriction phrictionStorage = new Storage.Phriction();
            Storage.SynchronizationLogging synchronizationLoggingStorage = new Storage.SynchronizationLogging();
            string filterText = parameters.FirstOrDefault();
            if (filterText == null) filterText = "";

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException("/synchronization/search", "session expired");
            if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/synchronization/search", "You don't have sufficient rights to synchronize Phabrico with Phabricator");

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // show overview screen
                foreach (Phabricator.Data.SynchronizationLogging synchronizationLogging in synchronizationLoggingStorage.Get(database, browser.Session.Locale)
                                                                                                                        .OrderByDescending(modification => modification.DateModified
                                                                                                                                                                       .ToUnixTimeSeconds()
                                                                                                                                          )) // do not order by milliseconds
                {
                    JsonRecordData record = new JsonRecordData();

                    record.Token = synchronizationLogging.Token;
                    record.LastModifiedBy = getAccountName(synchronizationLogging.LastModifiedBy);
                    record.Timestamp = FormatDateTimeOffset(synchronizationLogging.DateModified, browser.Session.Locale);
                    record.Title = synchronizationLogging.Title;
                    record.URL = synchronizationLogging.URL;

                    if (record.Token.StartsWith(Phabricator.Data.Phriction.Prefix))
                    {
                        Phabricator.Data.Phriction document = phrictionStorage.Get(database, synchronizationLogging.Token, browser.Session.Locale, true);
                        if (document == null) continue;

                        record.IsNew = string.IsNullOrEmpty(synchronizationLogging.PreviousContent);
                        record.Type = "fa-book";

                        if (document.Content.Contains(filterText) == false &&                                                       // is filterText found in document content ?
                            document.Name.Contains(filterText) == false &&                                                          // is filterText found in document title ?
                            string.Join(" ", document.Projects                                                                      // is filterText found in project tags ?
                                                 .Split(',')                                                                        //
                                                 .Select(project => getProjectName(project))                                        //
                                       )                                                                                            //
                                  .Split(' ', '-', '.')                                                                             //
                                  .Any(word => word.StartsWith(filterText, StringComparison.InvariantCultureIgnoreCase)) == false)  //
                        {
                            continue;
                        }
                    }
                    else
                    if (record.Token.StartsWith(Phabricator.Data.Maniphest.Prefix))
                    {
                        Phabricator.Data.Maniphest task = maniphestStorage.Get(database, synchronizationLogging.Token, browser.Session.Locale, true);
                        if (task == null) continue;

                        record.IsNew = string.IsNullOrEmpty(synchronizationLogging.PreviousContent);
                        record.MetadataIsModified = synchronizationLogging.MetadataIsModified;
                        record.Type = "fa-anchor";

                        if (task.Description.ToLower().Contains(filterText.ToLower()) == false &&                                                       // is filterText found in task description ?
                            task.Name.ToLower().Contains(filterText.ToLower()) == false &&                                                              // is filterText found in task title ?
                            filterText.Equals("T" + task.ID) == false &&                                                            // is filterText T + task-id ?
                            string.Join(" ", task.Projects                                                                          // is filterText found in project tags ?
                                                 .Split(',')                                                                        //
                                                 .Select(project => getProjectName(project))                                        //
                                       )                                                                                            //
                                  .Split(' ', '-', '.')                                                                             //
                                  .Any(word => word.StartsWith(filterText, StringComparison.InvariantCultureIgnoreCase)) == false)  //
                        {
                            continue;
                        }
                    }
                    else
                    if (record.Token.StartsWith(Phabricator.Data.PhamePost.Prefix))
                    {
                        Phabricator.Data.PhamePost blogPost = phamePostStorage.Get(database, synchronizationLogging.Token, browser.Session.Locale);
                        if (blogPost == null) continue;

                        record.IsNew = true;
                        record.Type = "fa-feed";

                        if (blogPost.Title.Contains(filterText) == false &&     // is filterText found in blog post ?
                            blogPost.Content.Contains(filterText) == false)     //
                        {
                            continue;
                        }
                    }

                    tableRows.Add(record);
                }

                string jsonData = JsonConvert.SerializeObject(tableRows);
                resultHttpMessage = new JsonMessage(jsonData);
            }
        }

        /// <summary>
        /// This method is fired via the View Changes in the Synchronization Logging screen.
        /// It will return a the partial view SynchronizationDiff
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="resultHttpMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/synchronization/data")]
        public void HttpGetLoadSynchronizedObject(Http.Server httpServer, ref HttpMessage resultHttpMessage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.MasterDataIsAccessible == false) throw new Phabrico.Exception.HttpNotFound("/synchronization/data");

            Storage.Maniphest maniphestStorage = new Storage.Maniphest();
            Storage.Phriction phrictionStorage = new Storage.Phriction();
            Storage.PhamePost phamePostStorage = new Storage.PhamePost();
            Storage.SynchronizationLogging synchronizationLoggingStorage = new Storage.SynchronizationLogging();

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException("/synchronization/data", "session expired");
            if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/synchronization/data", "You don't have sufficient rights to synchronize Phabrico with Phabricator");

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                string phabricatorToken = parameters.FirstOrDefault();
                Phabricator.Data.SynchronizationLogging synchronizationLogging = synchronizationLoggingStorage.Get(database, phabricatorToken, browser.Session.Locale, false);
                if (synchronizationLogging != null)
                {
                    string currentContent = null;

                    if (phabricatorToken.StartsWith(Phabricator.Data.Phriction.Prefix))
                    {
                        Phabricator.Data.Phriction currentDocument = phrictionStorage.Get(database, phabricatorToken, browser.Session.Locale, false);
                        if (currentDocument != null)
                        {
                            currentContent = currentDocument.Content;
                        }
                    }
                    else
                    if (phabricatorToken.StartsWith(Phabricator.Data.Maniphest.Prefix))
                    {
                        Phabricator.Data.Maniphest currentTask = maniphestStorage.Get(database, phabricatorToken, browser.Session.Locale, false);
                        if (currentTask != null)
                        {
                            currentContent = currentTask.Description;
                        }
                    }
                    else
                    if (phabricatorToken.StartsWith(Phabricator.Data.PhamePost.Prefix))
                    {
                        Phabricator.Data.PhamePost currentPost = phamePostStorage.Get(database, phabricatorToken, browser.Session.Locale);
                        if (currentPost != null)
                        {
                            currentContent = currentPost.Content;
                        }
                    }

                    if (currentContent != null)
                    {
                        string previousContent = synchronizationLogging.PreviousContent;

                        Diff.GenerateDiffLeftRight(ref previousContent, ref currentContent, true, browser.Session.Locale);

                        HtmlViewPage viewPage = new HtmlViewPage(httpServer, browser, true, "SynchronizationDiff", null);
                        viewPage.SetText("ITEM", synchronizationLogging.Title);
                        viewPage.SetText("URL", synchronizationLogging.URL);
                        viewPage.SetText("CONTENT-LEFT", previousContent, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        viewPage.SetText("CONTENT-RIGHT", currentContent, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);

                        resultHttpMessage = viewPage;
                        return;
                    }
                }

                resultHttpMessage = new Http.Response.HttpRedirect(httpServer, browser, "/synchronization");
            }
        }

        /// <summary>
        /// This method is fired when the user clicks on the 'Synchronize' button
        /// </summary>
        /// <param name="httpServer">webserver object</param>
        /// <param name="parameters">N/A</param>
        [UrlController(URL = "/synchronize/full")]
        public void HttpPostStartFullSynchronization(Http.Server httpServer, string[] parameters)
        {
            if (browser.InvalidCSRF(browser.Request.RawUrl)) throw new Phabrico.Exception.InvalidCSRFException();
            if (httpServer.Customization.MasterDataIsAccessible == false) throw new Phabrico.Exception.HttpNotFound("/synchronize/full");
            if (browser.Conduit.PhabricatorUrl.StartsWith("http://") == false && browser.Conduit.PhabricatorUrl.StartsWith("https://") == false) throw new Phabrico.Exception.InvalidConfigurationException("Phabricator server not configured");
            if (RegexSafe.IsMatch(browser.Conduit.Token, "api-[a-zA-Z0-9]{28}", RegexOptions.None) == false) throw new Phabrico.Exception.InvalidConfigurationException("Phabricator token not configured");

            if (Synchronization.InProgress)
            {
                // synchronization already in progress -> skip
                return;
            }

            // initialize percentage and progress description
            SharedResource.Instance.ProgressPercentage = 0;
            SharedResource.Instance.ProgressState = "OK";
            SharedResource.Instance.ProgressDescription = Miscellaneous.Locale.TranslateText("Synchronization.Status.Initializing", browser.Session.Locale);

            // start thread which executes the actual synchronization code
            Task.Factory.StartNew(() => FullSynchronizationThread(httpServer));
        }

        /// <summary>
        /// This method is fired when the user clicks on the 'Synchronize' button
        /// </summary>
        /// <param name="httpServer">webserver object</param>
        /// <param name="parameters">N/A</param>
        [UrlController(URL = "/synchronize/light")]
        public void HttpPostStartLightSynchronization(Http.Server httpServer, string[] parameters)
        {
            if (httpServer.Customization.MasterDataIsAccessible == false) throw new Phabrico.Exception.HttpNotFound("/synchronize/light");

            if (Synchronization.InProgress)
            {
                // synchronization already in progress -> skip
                return;
            }

            // initialize percentage and progress description
            SharedResource.Instance.ProgressPercentage = 0;
            SharedResource.Instance.ProgressState = "OK";
            SharedResource.Instance.ProgressDescription = Miscellaneous.Locale.TranslateText("Synchronization.Status.Initializing", browser.Session.Locale);

            // start thread which executes the actual synchronization code
            Task.Factory.StartNew(() => LightSynchronizationThread(httpServer));
        }

        /// <summary>
        /// This method is fired when the user clicks on the 'Synchronize' button.
        /// It will count the number of unforzen changes that might be sent to the Phabricator server.
        /// This number will be displayed on a confirmation messagebox.
        /// When the user clicks 'yes', the unfrozen changes will be uploaded the Phabricator server
        /// </summary>
        /// <param name="httpServer">webserver object</param>
        /// <param name="jsonMessage">The JSON result that represents the number of unfrozen changes</param>
        /// <param name="parameters">N/A</param>
        /// <param name="parameterActions">N/A</param>
        [UrlController(URL = "/synchronize/prepare")]
        public void HttpGetSynchronizationPrepare(Http.Server httpServer, ref JsonMessage jsonMessage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.MasterDataIsAccessible == false) throw new Phabrico.Exception.HttpNotFound("/synchronize/prepare");

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException("/synchronization/prepare", "session expired");

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                Storage.Stage stageStorage = new Storage.Stage();
                int numberOfUnfrozenChanges = stageStorage.Get(database, Language.NotApplicable).Count(record => record.Frozen == false);

                string jsonData = JsonConvert.SerializeObject(new
                {
                    NumberOfUnfrozenChanges = numberOfUnfrozenChanges
                });
                jsonMessage = new JsonMessage(jsonData);
            }
        }

        /// <summary>
        /// This method is fired periodically by the webbrowser after the synchronization process has been started.
        /// It sends back the current progress of the synchronization to the webbrowser
        /// </summary>
        /// <param name="httpServer">webserver object</param>
        /// <param name="jsonMessage">The JSON result that represents the current progress of synchronization</param>
        /// <param name="parameters">N/A</param>
        /// <param name="parameterActions">N/A</param>
        [UrlController(URL = "/synchronize/status")]
        public void HttpGetSynchronizationStatus(Http.Server httpServer, ref JsonMessage jsonMessage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.MasterDataIsAccessible == false) throw new Phabrico.Exception.HttpNotFound("/synchronize/status");

            string jsonData = JsonConvert.SerializeObject(new {
                Percentage = (int)SharedResource.Instance.ProgressPercentage,
                Description = SharedResource.Instance.ProgressDescription,
                StackTrace = SharedResource.Instance.ProgressRequestData,
                Status = SharedResource.Instance.ProgressState
            });
            jsonMessage = new JsonMessage(jsonData);
        }

        /// <summary>
        /// This method is fired from the HttpPostStartFullSynchronization method ("/synchronize/full" url)
        /// It downloads the content of the Phabricator server into the local SQLite database and uploads
        /// the content of the StageInfo table to the Phabricator server
        /// </summary>
        /// <param name="httpServer">webserver object</param>
        public void FullSynchronizationThread(Http.Server httpServer)
        {
            evSynchronizationInProgress.Set();

            try
            {
                Storage.Account accountStorage = new Storage.Account();
                SessionManager.Token token = SessionManager.GetToken(browser);
                if (token == null) throw new Phabrico.Exception.AccessDeniedException("/synchronization", "session expired");
                if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/synchronize", "You don't have sufficient rights to synchronize Phabrico with Phabricator");

                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    // set private encryption key
                    database.PrivateEncryptionKey = token.PrivateEncryptionKey;

                    SynchronizationParameters synchronizationParameters = new SynchronizationParameters();
                    synchronizationParameters.database = database;
                    synchronizationParameters.browser = browser;
                    synchronizationParameters.existingAccount = accountStorage.Get(database, token);
                    synchronizationParameters.previousSyncMode = (SyncMode)Enum.Parse(typeof(SyncMode), database.GetConfigurationParameter("LastSyncMode") ?? SyncMode.Light.ToString());

                    database.SetConfigurationParameter("LastSyncMode", SyncMode.Full.ToString());

                    List<MethodProgress> synchronizationMethods = new List<MethodProgress>();

                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 1, Method = ProgressMethod_Connecting });
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 1, Method = ProgressMethod_DownloadProjects });
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 1, Method = ProgressMethod_DownloadUsers });
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 1, Method = ProgressMethod_WhoAmI });
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 1, Method = ProgressMethod_LoadLastSynchronizeTimeStamp });
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 1, Method = ProgressMethod_DownloadManiphestPrioritiesAndStates });
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 20, Method = ProgressMethod_DownloadManiphestTasks });
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 20, Method = ProgressMethod_DownloadPhrictionDocuments });
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 5, Method = ProgressMethod_DownloadPhamePosts });
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 40, Method = ProgressMethod_DownloadFileObjects });
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 20, Method = ProgressMethod_DownloadDiagramObjects });
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 1, Method = ProgressMethod_DownloadMacros });
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 5, Method = ProgressMethod_UploadTransactions });
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 1, Method = ProgressMethod_SaveLastDownloadTimeStamp });
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 5, Method = ProgressMethod_UploadPhrictionDocuments });
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 5, Method = ProgressMethod_UploadManiphestTasks });
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 5, Method = ProgressMethod_DownloadPhrictionDocuments });  // download uploaded phriction documents again from server so we get the correct tokens
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 5, Method = ProgressMethod_DownloadManiphestTasks });      // download uploaded maniphest tasks again from server so we get the correct tokens
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 10, Method = ProgressMethod_DownloadFileObjects });
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 10, Method = ProgressMethod_DownloadDiagramObjects });
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 1, Method = ProgressMethod_DownloadUnreferencedFileObjects });
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 1, Method = ProgressMethod_DownloadMacros });

                    foreach (Plugin.PluginBase plugin in Http.Server.Plugins)
                    {
                        if (plugin.State == Plugin.PluginBase.PluginState.Loaded)
                        {
                            plugin.Database = new Storage.Database(database.EncryptionKey);
                            plugin.Initialize();
                            plugin.State = Plugin.PluginBase.PluginState.Initialized;
                        }

                        if (plugin.SynchronizationReadData != null)
                        {
                            MethodProgress pluginMethodProgress = new MethodProgress();
                            pluginMethodProgress.DurationCoefficient = 5;
                            pluginMethodProgress.Method = new Action<Synchronization.SynchronizationParameters,int,int>(plugin.SynchronizationReadData);
                            synchronizationMethods.Add(pluginMethodProgress);
                        }
                    }

                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 1, Method = ProgressMethod_DeleteOldData });
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 1, Method = ProgressMethod_SaveLastSynchronizeTimeStamp });
                    synchronizationMethods.Add( new MethodProgress { DurationCoefficient = 1, Method = ProgressMethod_Finalize });

                    ExecuteProgressMethodsSynchronously(synchronizationParameters, synchronizationMethods.ToArray());

                    // store syncrhonization timestamp
                    synchronizationParameters.existingAccount = accountStorage.WhoAmI(database, browser);  // reload account again (because the WhoAmI call might have changed some settings)
                    synchronizationParameters.existingAccount.Parameters.LastSynchronizationTimestamp = DateTimeOffset.UtcNow;
                    accountStorage.Set(database, synchronizationParameters.existingAccount);

                    // mark end of synchronization process
                    SharedResource.Instance.ProgressDescription = Miscellaneous.Locale.TranslateText("Synchronization.Status.Finishing", browser.Session.Locale);
                    SharedResource.Instance.ProgressPercentage = 100;

                    // invalidate cached data
                    Server.InvalidateNonStaticCache(database, DateTime.MaxValue);
                }
            }
            catch (System.Exception exception)
            {
                Phabricator.API.Conduit.PhabricatorException conduitException = exception as Phabricator.API.Conduit.PhabricatorException;

                SharedResource.Instance.ProgressDescription = Miscellaneous.Locale.TranslateText("Synchronization.Status.Failed", browser.Session.Locale) + ": " + exception.Message;
                SharedResource.Instance.ProgressPercentage = 100;
                SharedResource.Instance.ProgressState = "ERROR";

                if (conduitException != null)
                {
                    SharedResource.Instance.ProgressRequestData = conduitException.Request;
                }
                else
                {
                    SharedResource.Instance.ProgressRequestData = "";
                }

                Logging.WriteException("FullSynchronizationThread", exception);
            }
            finally
            {
                evSynchronizationInProgress.Reset();
            }
        }

        /// <summary>
        /// This method is fired from the HttpPostStartLightSynchronization method ("/synchronize/full" light)
        /// It downloads the content of the Phabricator server into the local SQLite database and uploads
        /// the content of the StageInfo table to the Phabricator server
        /// </summary>
        /// <param name="httpServer">webserver object</param>
        public void LightSynchronizationThread(Http.Server httpServer)
        {
            evSynchronizationInProgress.Set();

            try
            {
                Storage.Account accountStorage = new Storage.Account();
                SessionManager.Token token = SessionManager.GetToken(browser);
                if (token == null) throw new Phabrico.Exception.AccessDeniedException("/synchronization", "session expired");
                if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/synchronize", "You don't have sufficient rights to synchronize Phabrico with Phabricator");

                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    // set private encryption key
                    database.PrivateEncryptionKey = token.PrivateEncryptionKey;

                    database.SetConfigurationParameter("LastSyncMode", SyncMode.Light.ToString());

                    SynchronizationParameters synchronizationParameters = new SynchronizationParameters();
                    synchronizationParameters.database = database;
                    synchronizationParameters.browser = browser;
                    synchronizationParameters.existingAccount = accountStorage.Get(database, token);
                    synchronizationParameters.previousSyncMode = SyncMode.Light;

                    ExecuteProgressMethodsSynchronously(synchronizationParameters,
                        new MethodProgress { DurationCoefficient = 1, Method = ProgressMethod_Connecting },
                        new MethodProgress { DurationCoefficient = 1, Method = ProgressMethod_DownloadProjects },
                        new MethodProgress { DurationCoefficient = 1, Method = ProgressMethod_DownloadUsers },
                        new MethodProgress { DurationCoefficient = 1, Method = ProgressMethod_WhoAmI },
                        new MethodProgress { DurationCoefficient = 1, Method = ProgressMethod_DownloadManiphestPrioritiesAndStates }
                    );

                    // mark end of synchronization process
                    SharedResource.Instance.ProgressDescription = Miscellaneous.Locale.TranslateText("Synchronization.Status.Finishing", browser.Session.Locale);
                    SharedResource.Instance.ProgressPercentage = 100;

                    // invalidate cached data
                    Server.InvalidateNonStaticCache(database, DateTime.MaxValue);
                }
            }
            catch (System.Exception exception)
            {
                Phabricator.API.Conduit.PhabricatorException conduitException = exception as Phabricator.API.Conduit.PhabricatorException;

                SharedResource.Instance.ProgressDescription = Miscellaneous.Locale.TranslateText("Synchronization.Status.Failed", browser.Session.Locale) + ": " + exception.Message;
                SharedResource.Instance.ProgressPercentage = 100;
                SharedResource.Instance.ProgressState = "ERROR";

                if (conduitException != null)
                {
                    SharedResource.Instance.ProgressRequestData = conduitException.Request;
                }
                else
                {
                    SharedResource.Instance.ProgressRequestData = "";
                }
            }
            finally
            {
                evSynchronizationInProgress.Reset();
            }
        }

        /// <summary>
        /// Executes some given sync-methods synchronously
        /// </summary>
        /// <param name="synchronizationParameters">Shared parameters through the whole synchronization process</param>
        /// <param name="progresses">The sync methods to be executed</param>
        private void ExecuteProgressMethodsSynchronously(SynchronizationParameters synchronizationParameters, params MethodProgress[] progresses)
        {
            int totalDuration = progresses.Sum(p => p.DurationCoefficient);

            synchronizationParameters.remotelyModifiedObjects = new List<Phabricator.Data.PhabricatorObject>();
            foreach (MethodProgress progress in progresses)
            {
                int processedDuration = progresses.TakeWhile(p => p != progress)
                                                  .Sum(p => p.DurationCoefficient);
                Thread.Sleep(100);
                synchronizationParameters.stepSize = progress.DurationCoefficient;
                SharedResource.Instance.ProgressPercentage = processedDuration * 100.0 / totalDuration;

                try
                {
                    Logging.WriteInfo("(progress)", "Start {0}", progress.Method.Method.Name);
                    progress.Method(synchronizationParameters, processedDuration, totalDuration);
                }
                finally
                {
                    Logging.WriteInfo("(progress)", "End {0}", progress.Method.Method.Name);
                }
            }
        }

        /// <summary>
        /// This Progress Method will try to connect to the Phabricator server
        /// </summary>
        /// <param name="synchronizationParameters">Collection of parameters which are shared over all the Progress Methods</param>
        /// <param name="processedDuration">N/A</param>
        /// <param name="totalDuration">N/A</param>
        public void ProgressMethod_Connecting(SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration)
        {
            Phabricator.API.User phabricatorUserAPI = new Phabricator.API.User();

            SharedResource.Instance.ProgressDescription = Miscellaneous.Locale.TranslateText("Synchronization.Status.ConnectingToPhabricatorServer", browser.Session.Locale)
                                                                              .Replace("@@PHABRICATOR-SERVER@@", synchronizationParameters.browser.Conduit.PhabricatorUrl);

            // execute some API to check if the Phabricator server respond correctly
            phabricatorUserAPI.GetAll(synchronizationParameters.database, synchronizationParameters.browser.Conduit, DateTimeOffset.MinValue).FirstOrDefault();

            // check if there's a time difference between the local computer and the Phabricator server
            // if any time difference, the difference will be incorporated in the downloaded objects (or interpreted
            // as: assume the timestamp of the local computer is correct  and  the timestamp of the Phabricator server is wrong)
            DateTime dtWebServer = synchronizationParameters.browser.Conduit.GetTimestampPhabricatorServer();
            DateTime dtLocalComputer = DateTime.Now;
            synchronizationParameters.TimeDifferenceBetweenPhabricatorAndLocalComputer = dtWebServer.Subtract(dtLocalComputer);

            // clear all synchronization logging
            Storage.SynchronizationLogging synchronizationLoggingStorage = new Storage.SynchronizationLogging();
            synchronizationLoggingStorage.Clear(synchronizationParameters.database);
        }

        /// <summary>
        /// Deletes old data (i.e. Phriction, Maniphest, Stage, BannedObject)
        /// </summary>
        /// <param name="synchronizationParameters"></param>
        /// <param name="processedDuration"></param>
        /// <param name="totalDuration"></param>
        public void ProgressMethod_DeleteOldData(SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration)
        {
            Storage.BannedObject bannedObjectStorage = new Storage.BannedObject();
            Storage.Phriction phrictionStorage = new Storage.Phriction();
            Storage.Maniphest maniphestStorage = new Storage.Maniphest();
            Storage.Stage stageStorage = new Storage.Stage();

            SharedResource.Instance.ProgressDescription = Miscellaneous.Locale.TranslateText("Synchronization.Status.CleaningUp", browser.Session.Locale);

            bannedObjectStorage.Clear(synchronizationParameters.database);

            // == Determine what exactly should be deleted ==============================================================================================================================
            // determine disallowed project tags
            string[] disallowedProjectTags = synchronizationParameters.projectSelected
                                                                      .Where(project => project.Key.Equals(Phabricator.Data.Project.None) == false)
                                                                      .Where(project => project.Value == Phabricator.Data.Project.Selection.Disallowed)
                                                                      .Select(project => project.Key)
                                                                      .ToArray();

            // determine selected projects tokens
            string[] selectedProjects = synchronizationParameters.projectSelected
                                                              .Where(project => project.Value == Phabricator.Data.Project.Selection.Selected)
                                                              .Select(project => project.Key)
                                                              .ToArray();
            bool keepUntaggedTasksOrDocuments = selectedProjects.Contains(Phabricator.Data.Project.None);

            // determine selected user tokens
            string[] selectedUsers = synchronizationParameters.userSelected
                                                              .Where(user => user.Value == true)
                                                              .Select(user => user.Key)
                                                              .ToArray();

            // determine if unassigned maniphest tasks should be removed
            bool keepUnassignedTasks = selectedUsers.Contains(Phabricator.Data.User.None);

            // determine selected user tokens (without the dummy 'None' user)
            selectedUsers = selectedUsers.Where(user => user.Equals(Phabricator.Data.User.None) == false).ToArray();

            // == Delete old Phriction documents ========================================================================================================================================
            List<Phabricator.Data.Phriction> documentsToRemove = new List<Phabricator.Data.Phriction>();
            IEnumerable<Phabricator.Data.Phriction> allDocuments = phrictionStorage.Get(synchronizationParameters.database, browser.Session.Locale);
            if (synchronizationParameters.existingAccount.Parameters.Synchronization.HasFlag(SynchronizationMethod.PhrictionAllProjects) == false)
            {
                // remove documents without a project tagged
                if (keepUntaggedTasksOrDocuments == false &&
                    synchronizationParameters.existingAccount.Parameters.Synchronization.HasFlag(SynchronizationMethod.PhrictionSelectedProjectsOnlyIncludingDocumentTree) == false
                   )
                {
                    documentsToRemove.AddRange(allDocuments.Where(document => string.IsNullOrWhiteSpace(document.Projects)));
                }

                // remove unselected projects
                if (synchronizationParameters.existingAccount.Parameters.Synchronization.HasFlag(SynchronizationMethod.PhrictionSelectedProjectsOnlyIncludingDocumentTree) == false)
                {
                    IEnumerable<Phabricator.Data.Phriction> unselectedProjectDocuments;
                    if (synchronizationParameters.existingAccount.Parameters.Synchronization.HasFlag(SynchronizationMethod.PhrictionAllSelectedProjectsOnly))
                    {
                        // remove all documents for which at least 1 of the selected projects is not tagged
                        unselectedProjectDocuments = allDocuments.Where(document => selectedProjects.All(selectedProject => document.Projects
                                                                                                                                    .Split(',')
                                                                                                                                    .Contains(selectedProject)) == false);
                    }
                    else
                    {
                        // remove all documents for which none of the selected projects was tagged
                        unselectedProjectDocuments = allDocuments.Where(document => document.Projects
                                                                                            .Split(',')
                                                                                            .All(project => string.IsNullOrWhiteSpace(project) == false
                                                                                                         && selectedProjects.Contains(project) == false));
                    }
                    documentsToRemove.AddRange(unselectedProjectDocuments);
                }
            }

            // remove documents which are tagged with disallowed projects
            documentsToRemove.AddRange(allDocuments.Where(task => task.Projects
                                                                        .Split(',')
                                                                        .Any(project => disallowedProjectTags.Contains(project))));

            // check if we still have a root document
            bool noRootDocument = documentsToRemove.Any(document => document.Path.Equals("/")) || allDocuments.All(document => document.Path.Equals("/") == false);
            if (noRootDocument)
            {
                // no root document found -> search for documents which are the closest to the 'virtual' root
                string[] initialPhrictionPathAliases = synchronizationParameters.initialPhrictionPathAliases.OrderByDescending(p => p.Length).ToArray();
                Dictionary<string, string[][]> urlPartsCollection = phrictionStorage.Get(synchronizationParameters.database, browser.Session.Locale)
                                                                    .Where(document => !documentsToRemove.Contains(document))
                                                                    .Select(document =>
                                                                    {
                                                                        var matchingPath = initialPhrictionPathAliases.FirstOrDefault(p => document.Path.StartsWith(p));
                                                                        return new
                                                                        {
                                                                            Key = matchingPath ?? "",
                                                                            Value = document.Path.Split('/')
                                                                        };
                                                                    })
                                                                    .GroupBy(item => item.Key)
                                                                    .ToDictionary(
                                                                        g => g.Key,
                                                                        g => g.Select(item => item.Value).ToArray()
                                                                    );
                if (urlPartsCollection.Any())  // check if any documents found
                {
                    Phabricator.Data.Phriction[] rootDocuments = urlPartsCollection.SelectMany(kv =>
                        {
                            if (kv.Key != "")
                            {
                                int initialPathPartsCount = kv.Key.Split('/').Length;
                                int minimumRelativeLength = kv.Value.Min(urlParts => urlParts.Length - initialPathPartsCount);

                                return phrictionStorage.Get(synchronizationParameters.database, browser.Session.Locale)
                                                       .Where(document => document.Path.StartsWith(kv.Key)
                                                                       && document.Path.Split('/').Length - initialPathPartsCount == minimumRelativeLength
                                                                       && documentsToRemove.Contains(document) == false);
                            }
                            return Enumerable.Empty<Phabricator.Data.Phriction>();
                        }).ToArray();

                    if (rootDocuments.Count() == 1)
                    {
                        Phabricator.Data.Phriction linkedDocument = rootDocuments.FirstOrDefault();
                        phrictionStorage.AddAlias(synchronizationParameters.database, "/", linkedDocument);
                    }
                    else
                    {
                        Phabricator.Data.Phriction coverPage = new Phabricator.Data.Phriction();
                        coverPage.Path = "/";
                        coverPage.Content = "";
                        coverPage.Token = Phabricator.Data.Phriction.PrefixCoverPage + "HOMEPAGE";
                        phrictionStorage.Add(synchronizationParameters.database, coverPage);

                        foreach (Phabricator.Data.Phriction rootDocument in rootDocuments)
                        {
                            synchronizationParameters.database.DescendTokenFrom(coverPage.Token, rootDocument.Token);
                        }
                    }
                }
            }

            // remove all documents where content is empty
            documentsToRemove.AddRange(allDocuments.OrderByDescending(document => document.Path.Length)
                                                   .Where(document => string.IsNullOrEmpty(document.Content)
                                                                   && phrictionStorage.GetHierarchy(synchronizationParameters.database, browser, document.Token).Any() == false
                                                         ));
            synchronizationParameters.lastRemovalTimestamp = documentsToRemove.Select(document => document.DateModified)
                                                                              .OrderByDescending(timestamp => timestamp)
                                                                              .DefaultIfEmpty()
                                                                              .FirstOrDefault();

            // == Delete old/disallowed maniphest tasks =================================================================================================================================
            DateTimeOffset oldestTimestampToKeep = GetOldestDateTimeOffsetToKeep(synchronizationParameters.existingAccount.Parameters.RemovalPeriodClosedManiphests);
            IEnumerable<Phabricator.Data.Maniphest> allTasks = maniphestStorage.Get(synchronizationParameters.database, browser.Session.Locale);
            List<Phabricator.Data.Maniphest> tasksToRemove = new List<Phabricator.Data.Maniphest>();
            tasksToRemove.AddRange(allTasks.Where(task => task.IsOpen == false && task.DateModified < oldestTimestampToKeep)); // remove old closed tasks

            tasksToRemove.AddRange(allTasks.Where(task => task.Projects
                                                            .Split(',')
                                                            .Any(project => disallowedProjectTags.Contains(project))));  // remove disallowed projects

            if (synchronizationParameters.existingAccount
                                         .Parameters
                                         .Synchronization
                                         .HasFlag(SynchronizationMethod.ManiphestSelectedProjectsOnly))
            {
                // synchronize tasks by project selection
                tasksToRemove.AddRange(allTasks.Where(task => task.Projects
                                                                .Split(',')
                                                                .All(project => string.IsNullOrWhiteSpace(project) == false
                                                                             && selectedProjects.Contains(project) == false)));  // remove unselected projects

                if (keepUntaggedTasksOrDocuments == false)
                {
                    tasksToRemove.AddRange(allTasks.Where(task => string.IsNullOrWhiteSpace(task.Projects)));  // remove tasks without a project tagged
                }
            }
            else
            {
                // synchronize tasks by user selection
                tasksToRemove.AddRange(allTasks.Where(task => selectedUsers.Contains(task.Author) == false
                                                           && selectedUsers.All(selectedUser => task.Subscribers
                                                                                                    .Split(',')
                                                                                                    .Contains(selectedUser)) == false
                                                           && selectedUsers.Contains(task.Owner) == false
                                                           && (keepUnassignedTasks && string.IsNullOrEmpty(task.Owner) == false
                                                               || keepUnassignedTasks == false
                                                              )
                                                     ));
            }

            // == Perform the actual removal ============================================================================================================================================
            SharedResource.Instance.ProgressDescription = Miscellaneous.Locale.TranslateText("Synchronization.Status.CleaningUp.Phriction", browser.Session.Locale);
            for (int i=0; i<documentsToRemove.Count; i++)
            {
                Phabricator.Data.Phriction documentToRemove = documentsToRemove[i];

                SharedResource.Instance.ProgressDescription = string.Format("{0} [{1}/{2}]",
                    Miscellaneous.Locale.TranslateText("Synchronization.Status.CleaningUp.Phriction", browser.Session.Locale),
                    i,
                    documentsToRemove.Count
                );

                List<Phabricator.Data.PhabricatorObject> documentLinkers = synchronizationParameters.database.GetDependentObjects(documentToRemove.Token, Language.NotApplicable).ToList();
                IEnumerable<Phabricator.Data.Phriction> phrictionDocumentsWithLinks = documentLinkers.OfType<Phabricator.Data.Phriction>();
                IEnumerable<Phabricator.Data.Maniphest> maniphestTasksWithLinks = documentLinkers.OfType<Phabricator.Data.Maniphest>();

                if (string.IsNullOrEmpty(documentToRemove.Content) == false)
                {
                    if (phrictionDocumentsWithLinks.Any(phrictionDocumentWithLinks => documentsToRemove.Contains(phrictionDocumentWithLinks) == false) ||
                        maniphestTasksWithLinks.Any(maniphestTaskWithLinks => tasksToRemove.Contains(maniphestTaskWithLinks) == false))
                    {
                        // Do not delete if this document is linked in a maniphest task or another phriction document which is not supposed to be deleted
                        continue;
                    }
                }

                stageStorage.Remove(synchronizationParameters.database, synchronizationParameters.browser, documentToRemove);
                phrictionStorage.Remove(synchronizationParameters.database, documentToRemove);
                synchronizationParameters.database.UndescendTokenFrom(documentToRemove.Token);

                Storage.SynchronizationLogging.Delete(synchronizationParameters.database, documentToRemove.Token);

                bannedObjectStorage.Add(synchronizationParameters.database, documentToRemove.Path, documentToRemove.Name);
            }


            SharedResource.Instance.ProgressDescription = Miscellaneous.Locale.TranslateText("Synchronization.Status.CleaningUp.Maniphest", browser.Session.Locale);
            for (int i=0; i<tasksToRemove.Count; i++)
            {
                Phabricator.Data.Maniphest taskToRemove = tasksToRemove[i];

                SharedResource.Instance.ProgressDescription = string.Format("{0} [{1}/{2}]",
                    Miscellaneous.Locale.TranslateText("Synchronization.Status.CleaningUp.Maniphest", browser.Session.Locale),
                    i,
                    tasksToRemove.Count
                );


                List<Phabricator.Data.PhabricatorObject> documentLinkers = synchronizationParameters.database.GetDependentObjects(taskToRemove.Token, Language.NotApplicable).ToList();
                IEnumerable<Phabricator.Data.Phriction> phrictionDocumentsWithLinks = documentLinkers.OfType<Phabricator.Data.Phriction>();
                IEnumerable<Phabricator.Data.Maniphest> maniphestTasksWithLinks = documentLinkers.OfType<Phabricator.Data.Maniphest>();

                if (phrictionDocumentsWithLinks.Any(phrictionDocumentWithLinks => documentsToRemove.Contains(phrictionDocumentWithLinks) == false) ||
                    maniphestTasksWithLinks.Any(maniphestTaskWithLinks => tasksToRemove.Contains(maniphestTaskWithLinks) == false))
                {
                    // Do not delete if this task is linked in another maniphest task or phriction document which is not supposed to be deleted
                    continue;
                }

                stageStorage.Remove(synchronizationParameters.database, synchronizationParameters.browser, taskToRemove);
                maniphestStorage.Remove(synchronizationParameters.database, taskToRemove);

                Storage.SynchronizationLogging.Delete(synchronizationParameters.database, taskToRemove.Token);

                bannedObjectStorage.Add(synchronizationParameters.database, taskToRemove.ID, taskToRemove.Name);
            }

            // == Clean up children of version document roots ===========================================================================================================================
            phrictionStorage.CleanupOldVersions(synchronizationParameters.database, synchronizationParameters.browser);

            // == Delete unreferenced files in staging area (and shrinks the database) ==================================================================================================
            SharedResource.Instance.ProgressDescription = Miscellaneous.Locale.TranslateText("Synchronization.Status.CleaningUp.UnreferencedFiles", browser.Session.Locale);
            Storage.Stage.DeleteUnreferencedFiles(synchronizationParameters.database, synchronizationParameters.browser);
        }
        
        /// <summary>
        /// Downloads referenced diagrams from Phabricator
        /// </summary>
        /// <param name="synchronizationParameters"></param>
        /// <param name="processedDuration"></param>
        /// <param name="totalDuration"></param>
        public void ProgressMethod_DownloadDiagramObjects(SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration)
        {
            Phabricator.API.Diagram phabricatorDiagramAPI = new Phabricator.API.Diagram();
            Storage.Diagram diagramStorage = new Storage.Diagram();

            // load all diagrams from phabricator which were referenced in the downloaded phriction and maniphest objects
            string messageLoadingDiagramObjects = Miscellaneous.Locale.TranslateText("Synchronization.Status.LoadingDiagramObjects", browser.Session.Locale);
            SharedResource.Instance.ProgressDescription = messageLoadingDiagramObjects;
            List<int> diagramIDsToDownload = synchronizationParameters.diagramObjectsPerToken
                                                                          .Values
                                                                          .SelectMany(diagramObjectId => diagramObjectId)
                                                                          .Distinct()
                                                                          .Where(diagramID => diagramStorage.GetByID(synchronizationParameters.database, diagramID, true) == null)
                                                                          .ToList();

            // include modified diagrams for which the wiki document or maniphest task was not modified
            List<int> lastModifiedDiagramIDs = phabricatorDiagramAPI.GetModifiedDiagramIDs(
                                                                            synchronizationParameters.browser.Conduit,
                                                                            synchronizationParameters.lastDownloadTimestamp
                                                                    )
                                                                    .ToList();

            foreach (int modifiedDiagramID in lastModifiedDiagramIDs.ToArray())
            {
                if (diagramStorage.GetByID(synchronizationParameters.database, modifiedDiagramID, true) != null)
                {
                    if (diagramIDsToDownload.Contains(modifiedDiagramID) == false)
                    {
                        diagramIDsToDownload.Add(modifiedDiagramID);
                    }
                }
            }

            List<Phabricator.Data.Diagram> phabricatorDiagramReferences = phabricatorDiagramAPI.GetReferences(synchronizationParameters.database,
                                                                                                              synchronizationParameters.browser.Conduit,
                                                                                                              diagramIDsToDownload
                                                                                                             )
                                                                                               .ToList();

            int index = 0;
            int count = phabricatorDiagramReferences.Count();
            double stepsize = (synchronizationParameters.stepSize * 100.00) / ((double)count * (double)totalDuration);
            foreach (Phabricator.Data.Diagram phabricatorDiagramReference in phabricatorDiagramReferences)
            {
                string size;
                if (phabricatorDiagramReference.Size > 1024 * 1024)
                {
                    size = string.Format("{0} MB", phabricatorDiagramReference.Size / (1024*1024));
                }
                else
                if (phabricatorDiagramReference.Size > 1024)
                {
                    size = string.Format("{0} KB", phabricatorDiagramReference.Size / 1024);
                }
                else
                {
                    size = string.Format("{0} bytes", phabricatorDiagramReference.Size);
                }

                SharedResource.Instance.ProgressDescription = string.Format("{0} [{1}/{2}] ({3})", messageLoadingDiagramObjects, index++, count, size);
                SharedResource.Instance.ProgressPercentage += stepsize;

                // sleep a bit, so the progress-bar is shown a little more animated...
                if ((index % 100) == 0) Thread.Sleep(100);

                Phabricator.Data.Diagram phabricatorDiagram = new Phabricator.Data.Diagram(phabricatorDiagramReference);
                Base64EIDOStream base64EIDOStream = phabricatorDiagramAPI.DownloadData(synchronizationParameters.browser.Conduit, phabricatorDiagramReference);
                base64EIDOStream.Seek(0, System.IO.SeekOrigin.Begin);
                phabricatorDiagram.DataStream = base64EIDOStream;
                diagramStorage.Add(synchronizationParameters.database, phabricatorDiagram);

                // link diagram-references to their owners (e.g. Phriction document or Maniphest task)
                foreach (string owner in synchronizationParameters.diagramObjectsPerToken.Where(kvp => kvp.Value.Contains(phabricatorDiagram.ID)).Select(kvp => kvp.Key))
                {
                    synchronizationParameters.database.AssignToken(owner, phabricatorDiagram.Token, Language.NotApplicable);
                }

                // add sync logging
                if (synchronizationParameters.previousSyncMode == SyncMode.Full)
                {
                    Storage.SynchronizationLogging synchronizationLoggingStorage = new Storage.SynchronizationLogging();
                    Phabricator.Data.SynchronizationLogging synchronizationLogging = new Phabricator.Data.SynchronizationLogging();
                    synchronizationLogging.Token = phabricatorDiagram.Token;
                    synchronizationLogging.DateModified = phabricatorDiagram.DateModified;
                    synchronizationLogging.LastModifiedBy = "";
                    synchronizationLogging.Title = "DIAG" + phabricatorDiagram.ID;
                    synchronizationLogging.URL = "diagrams.net/DIAG" + phabricatorDiagram.ID;
                    synchronizationLogging.PreviousContent = null;
                    synchronizationLoggingStorage.Add(synchronizationParameters.database, synchronizationLogging);
                }
            }

            synchronizationParameters.diagramObjectsPerToken.Clear();
        }

        /// <summary>
        /// Downloads referenced files from Phabricator
        /// </summary>
        /// <param name="synchronizationParameters"></param>
        /// <param name="processedDuration"></param>
        /// <param name="totalDuration"></param>
        public void ProgressMethod_DownloadFileObjects(SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration)
        {
            Phabricator.API.File phabricatorFileAPI = new Phabricator.API.File();
            Storage.File fileStorage = new Storage.File();

            // load all file objects from phabricator which were referenced in the downloaded phriction and maniphest objects
            string messageLoadingFileObjects = Miscellaneous.Locale.TranslateText("Synchronization.Status.LoadingFileObjects", browser.Session.Locale);
            SharedResource.Instance.ProgressDescription = messageLoadingFileObjects;
            List<int> fileIDsToDownload = synchronizationParameters.fileObjectsPerToken
                                                                          .Values
                                                                          .SelectMany(fileObjectId => fileObjectId)
                                                                          .Distinct()
                                                                          .Where(fileID => fileStorage.GetByID(synchronizationParameters.database, fileID, true) == null)
                                                                          .ToList();

            IEnumerable<Phabricator.Data.File> phabricatorFileReferences = phabricatorFileAPI.GetReferences(synchronizationParameters.database,
                                                                                                            synchronizationParameters.browser.Conduit,
                                                                                                            fileIDsToDownload
                                                                                                           );

            int index = 0;
            int count = phabricatorFileReferences.Count();
            double stepsize = (synchronizationParameters.stepSize * 100.00) / ((double)count * (double)totalDuration);
            foreach (Phabricator.Data.File phabricatorFileReference in phabricatorFileReferences)
            {
                string size;
                if (phabricatorFileReference.Size > 1024 * 1024)
                {
                    size = string.Format("{0} MB", phabricatorFileReference.Size / (1024*1024));
                }
                else
                if (phabricatorFileReference.Size > 1024)
                {
                    size = string.Format("{0} KB", phabricatorFileReference.Size / 1024);
                }
                else
                {
                    size = string.Format("{0} bytes", phabricatorFileReference.Size);
                }

                SharedResource.Instance.ProgressDescription = string.Format("{0} [{1}/{2}] ({3})", messageLoadingFileObjects, index++, count, size);
                SharedResource.Instance.ProgressPercentage += stepsize;

                // sleep a bit, so the progress-bar is shown a little more animated...
                if ((index % 100) == 0) Thread.Sleep(100);

                Phabricator.Data.File phabricatorFile = new Phabricator.Data.File(phabricatorFileReference);
                Base64EIDOStream base64EIDOStream = phabricatorFileAPI.DownloadData(synchronizationParameters.browser.Conduit, phabricatorFileReference.Token);
                base64EIDOStream.Seek(0, System.IO.SeekOrigin.Begin);
                phabricatorFile.DataStream = base64EIDOStream;
                fileStorage.Add(synchronizationParameters.database, phabricatorFile);

                // link file-references to their owners (e.g. Phriction document or Maniphest task)
                foreach (string owner in synchronizationParameters.fileObjectsPerToken.Where(kvp => kvp.Value.Contains(phabricatorFile.ID)).Select(kvp => kvp.Key))
                {
                    synchronizationParameters.database.AssignToken(owner, phabricatorFile.Token, Language.NotApplicable);
                }
            }

            synchronizationParameters.fileObjectsPerToken.Clear();
        }

        /// <summary>
        /// Downloads Macro information from Phabricator
        /// </summary>
        /// <param name="synchronizationParameters"></param>
        /// <param name="processedDuration"></param>
        /// <param name="totalDuration"></param>
        public void ProgressMethod_DownloadMacros(SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration)
        {
            Phabricator.API.File phabricatorFileAPI = new Phabricator.API.File();
            Storage.File fileStorage = new Storage.File();

            SharedResource.Instance.ProgressDescription = Miscellaneous.Locale.TranslateText("Synchronization.Status.LoadingMacroData", browser.Session.Locale);

            int fileId = -100001;
            foreach (KeyValuePair<string, string> macroFileReference in phabricatorFileAPI.GetMacroReferences(synchronizationParameters.browser.Conduit, synchronizationParameters.lastDownloadTimestamp))
            {
                Phabricator.Data.File fileData = new Phabricator.Data.File();
                Base64EIDOStream base64EIDOStream = phabricatorFileAPI.DownloadData(synchronizationParameters.browser.Conduit, macroFileReference.Value);
                base64EIDOStream.Seek(0, System.IO.SeekOrigin.Begin);
                fileData.DataStream = base64EIDOStream;

                fileData.DateModified = DateTimeOffset.UtcNow;
                fileData.FileName = "Macro." + macroFileReference.Key;
                fileData.Token = macroFileReference.Value;
                fileData.ID = fileId;
                fileData.Size = (int)base64EIDOStream.Length;
                fileData.MacroName = macroFileReference.Key;

                fileStorage.Add(synchronizationParameters.database, fileData);

                fileId--;
            }
        }

        /// <summary>
        /// Downloads Maniphest priority and state information from Phabricator
        /// </summary>
        /// <param name="synchronizationParameters"></param>
        /// <param name="processedDuration"></param>
        /// <param name="totalDuration"></param>
        public void ProgressMethod_DownloadManiphestPrioritiesAndStates(SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration)
        {
            Phabricator.API.ManiphestPriority phabricatorManiphestPriorityAPI = new Phabricator.API.ManiphestPriority();
            Phabricator.API.ManiphestStatus phabricatorManiphestStatusAPI = new Phabricator.API.ManiphestStatus();
            Storage.ManiphestPriority maniphestPriorityStorage = new Storage.ManiphestPriority();
            Storage.ManiphestStatus maniphestStatusStorage = new Storage.ManiphestStatus();

            // load all maniphest priority values from phabricator
            SharedResource.Instance.ProgressDescription = Miscellaneous.Locale.TranslateText("Synchronization.Status.LoadingManiphestPrioritiesAndStates", browser.Session.Locale);
            IEnumerable<Phabricator.Data.ManiphestPriority> phabricatorManiphestTaskPriorities = phabricatorManiphestPriorityAPI.GetAll(synchronizationParameters.database,
                                                                                                                                        synchronizationParameters.browser.Conduit
                                                                                                                                       );
            foreach (Phabricator.Data.ManiphestPriority phabricatorManiphestTaskPriority in phabricatorManiphestTaskPriorities)
            {
                maniphestPriorityStorage.Add(synchronizationParameters.database, phabricatorManiphestTaskPriority);
            }

            // load all maniphest state values from phabricator
            IEnumerable<Phabricator.Data.ManiphestStatus> phabricatorManiphestTaskStates = phabricatorManiphestStatusAPI.GetAll(synchronizationParameters.database,
                                                                                                                                synchronizationParameters.browser.Conduit
                                                                                                                               );
            foreach (Phabricator.Data.ManiphestStatus phabricatorManiphestTaskState in phabricatorManiphestTaskStates)
            {
                switch (phabricatorManiphestTaskState.Value)
                {
                    case "resolved":
                        phabricatorManiphestTaskState.Icon = "fa-check-circle";
                        break;

                    case "wontfix":
                        phabricatorManiphestTaskState.Icon = "fa-ban";
                        break;

                    case "invalid":
                        phabricatorManiphestTaskState.Icon = "fa-minus-circle";
                        break;

                    case "duplicate":
                        phabricatorManiphestTaskState.Icon = "fa-files-o";
                        break;

                    case "spite":
                        phabricatorManiphestTaskState.Icon = "fa-thumbs-down";
                        break;

                    case "open":
                        phabricatorManiphestTaskState.Icon = "fa-exclamation-circle";
                        break;

                    default:
                        phabricatorManiphestTaskState.Icon = "fa-bell";
                        break;
                }

                maniphestStatusStorage.Add(synchronizationParameters.database, phabricatorManiphestTaskState);
            }

        }

        /// <summary>
        /// Downloads Maniphest tasks from Phabricator
        /// </summary>
        /// <param name="synchronizationParameters"></param>
        /// <param name="processedDuration"></param>
        /// <param name="totalDuration"></param>
        public void ProgressMethod_DownloadManiphestTasks(SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration)
        {
            Phabricator.API.Maniphest phabricatorManiphestAPI = new Phabricator.API.Maniphest();
            Storage.Transaction transactionStorage = new Storage.Transaction();
            Storage.Maniphest maniphestStorage = new Storage.Maniphest();
            Storage.Keyword keywordStorage = new Storage.Keyword();

            phabricatorManiphestAPI.TimeDifferenceBetweenPhabricatorAndLocalComputer = synchronizationParameters.TimeDifferenceBetweenPhabricatorAndLocalComputer;

            SharedResource.Instance.ProgressDescription = Miscellaneous.Locale.TranslateText("Synchronization.Status.PreparingDownloadManiphestData", browser.Session.Locale);

            // prepare loading maniphest tasks: check if project filter (constraint) should be applied
            List<IEnumerable<Phabricator.Data.Maniphest>> phabricatorManiphestTaskCollection = new List<IEnumerable<Phabricator.Data.Maniphest>>();
            IEnumerable<Phabricator.Data.Maniphest> phabricatorManiphestTasks;
            Phabricator.API.Constraint[] phabricatorManiphestTaskConstraints = new Phabricator.API.Constraint[0];
            if (synchronizationParameters.existingAccount.Parameters.Synchronization.HasFlag(Phabricator.Data.Account.SynchronizationMethod.ManiphestSelectedProjectsOnly))
            {
                Phabricator.API.Constraint constraintActivatedProjects;
                Storage.Project projectStorage = new Storage.Project();
                string[] selectedProjectTags = synchronizationParameters.projectSelected
                                                                        .Where(project => project.Key.Equals(Phabricator.Data.Project.None) == false)
                                                                        .Where(project => project.Value == Phabricator.Data.Project.Selection.Selected)
                                                                        .Select(project => project.Key)
                                                                        .ToArray();
                IEnumerable<Phabricator.Data.Project> availableProjects = projectStorage.Get(synchronizationParameters.database, synchronizationParameters.browser.Session.Locale);

                if (availableProjects.Count() > 0 &&
                    selectedProjectTags.Count() == availableProjects.Count(p => p.Token.Equals(Phabricator.Data.Project.None) == false) &&
                    synchronizationParameters.projectSelected[Phabricator.Data.Project.None] == Phabricator.Data.Project.Selection.Selected
                   )
                {
                    // all projects are selected -> load all maniphest tasks from phabricator without a project constraint
                    phabricatorManiphestTasks = phabricatorManiphestAPI.GetAll(synchronizationParameters.database,
                                                                                synchronizationParameters.browser.Conduit,
                                                                                phabricatorManiphestTaskConstraints,
                                                                                synchronizationParameters.lastDownloadTimestamp);
                    phabricatorManiphestTaskCollection.Add(phabricatorManiphestTasks);
                }
                else
                {
                    if (synchronizationParameters.projectSelected[Phabricator.Data.Project.None] == Phabricator.Data.Project.Selection.Selected)
                    {
                        constraintActivatedProjects = new Phabricator.API.Constraint("projects", new string[] { "null()" });

                        phabricatorManiphestTaskConstraints = new Phabricator.API.Constraint[] { constraintActivatedProjects };

                        // load all maniphest tasks from phabricator without a project tag assigned
                        phabricatorManiphestTasks = phabricatorManiphestAPI.GetAll(synchronizationParameters.database,
                                                                                   synchronizationParameters.browser.Conduit,
                                                                                   phabricatorManiphestTaskConstraints,
                                                                                   synchronizationParameters.lastDownloadTimestamp);
                        phabricatorManiphestTaskCollection.Add(phabricatorManiphestTasks);
                    }

                    foreach (string selectedProjectTag in selectedProjectTags)
                    {
                        constraintActivatedProjects = new Phabricator.API.Constraint("projects", new string[] { selectedProjectTag });

                        phabricatorManiphestTaskConstraints = new Phabricator.API.Constraint[] { constraintActivatedProjects };

                        // load all maniphest tasks from phabricator
                        phabricatorManiphestTasks = phabricatorManiphestAPI.GetAll(synchronizationParameters.database,
                                                                                   synchronizationParameters.browser.Conduit,
                                                                                   phabricatorManiphestTaskConstraints,
                                                                                   synchronizationParameters.lastDownloadTimestamp);
                        phabricatorManiphestTaskCollection.Add(phabricatorManiphestTasks);
                    }
                }
            }
            else
            {
                Storage.User userStorage = new Storage.User();
                Phabricator.API.Constraint constraintSelectedUsers;
                string[] selectedUserTags = synchronizationParameters.userSelected
                                                                     .Where(user => user.Value == true)
                                                                     .Select(user => user.Key)
                                                                     .ToArray();
                IEnumerable<Phabricator.Data.User> availableUsers = userStorage.Get(synchronizationParameters.database, browser.Session.Locale);
                if (selectedUserTags.Count() == availableUsers.Count())
                {
                    // all users are selected -> load all maniphest tasks from phabricator without a user constraint
                    phabricatorManiphestTasks = phabricatorManiphestAPI.GetAll(synchronizationParameters.database,
                                                                                synchronizationParameters.browser.Conduit,
                                                                                phabricatorManiphestTaskConstraints,
                                                                                synchronizationParameters.lastDownloadTimestamp);
                    phabricatorManiphestTaskCollection.Add(phabricatorManiphestTasks);
                }
                else
                {
                    if (selectedUserTags.Any(user => user.Equals(Phabricator.Data.User.None)))
                    {
                        constraintSelectedUsers = new Phabricator.API.Constraint("assigned", new string[] { "none()" });

                        phabricatorManiphestTaskConstraints = new Phabricator.API.Constraint[] { constraintSelectedUsers };

                        // load all maniphest tasks from phabricator
                        phabricatorManiphestTasks = phabricatorManiphestAPI.GetAll(synchronizationParameters.database,
                                                                                   synchronizationParameters.browser.Conduit,
                                                                                   phabricatorManiphestTaskConstraints,
                                                                                   synchronizationParameters.lastDownloadTimestamp);
                        phabricatorManiphestTaskCollection.Add(phabricatorManiphestTasks);
                    }

                    foreach (string selectedUserTag in selectedUserTags.Where(userTag => userTag.Equals(Phabricator.Data.User.None) == false))
                    {
                        // == search for task owners ==========================================================================================
                        constraintSelectedUsers = new Phabricator.API.Constraint("assigned", new string[] { selectedUserTag });
                        phabricatorManiphestTaskConstraints = new Phabricator.API.Constraint[] { constraintSelectedUsers };

                        // load all maniphest tasks from phabricator
                        phabricatorManiphestTasks = phabricatorManiphestAPI.GetAll(synchronizationParameters.database,
                                                                                   synchronizationParameters.browser.Conduit,
                                                                                   phabricatorManiphestTaskConstraints,
                                                                                   synchronizationParameters.lastDownloadTimestamp);
                        phabricatorManiphestTaskCollection.Add(phabricatorManiphestTasks);

                        // == search for task authors =========================================================================================
                        constraintSelectedUsers = new Phabricator.API.Constraint("authorPHIDs", new string[] { selectedUserTag });
                        phabricatorManiphestTaskConstraints = new Phabricator.API.Constraint[] { constraintSelectedUsers };

                        // load all maniphest tasks from phabricator
                        phabricatorManiphestTasks = phabricatorManiphestAPI.GetAll(synchronizationParameters.database,
                                                                                   synchronizationParameters.browser.Conduit,
                                                                                   phabricatorManiphestTaskConstraints,
                                                                                   synchronizationParameters.lastDownloadTimestamp);
                        phabricatorManiphestTaskCollection.Add(phabricatorManiphestTasks);

                        // == search for task subscribers =====================================================================================
                        constraintSelectedUsers = new Phabricator.API.Constraint("subscribers", new string[] { selectedUserTag });
                        phabricatorManiphestTaskConstraints = new Phabricator.API.Constraint[] { constraintSelectedUsers };

                        // load all maniphest tasks from phabricator
                        phabricatorManiphestTasks = phabricatorManiphestAPI.GetAll(synchronizationParameters.database,
                                                                                   synchronizationParameters.browser.Conduit,
                                                                                   phabricatorManiphestTaskConstraints,
                                                                                   synchronizationParameters.lastDownloadTimestamp);
                        phabricatorManiphestTaskCollection.Add(phabricatorManiphestTasks);
                    }
                }
            }

            // remove disallowed project tags from result
            string[] disallowedProjectTags = synchronizationParameters.projectSelected
                                                                      .Where(project => project.Key.Equals(Phabricator.Data.Project.None) == false)
                                                                      .Where(project => project.Value == Phabricator.Data.Project.Selection.Disallowed)
                                                                      .Select(project => project.Key)
                                                                      .ToArray();

            phabricatorManiphestTasks = phabricatorManiphestTaskCollection.SelectMany(task => task)
                                                                          .Distinct()
                                                                          .Where(task => task.Projects
                                                                                             .Split(',')
                                                                                             .All(project => disallowedProjectTags.Contains(project) == false));

            // execute the Conduit API: start downloading
            phabricatorManiphestTasks = phabricatorManiphestTasks.ToList();

            // process the downloaded results
            List<string> loggedManiphestTokens = new List<string>();
            int index = 0;
            int count = phabricatorManiphestTasks.Count();
            double stepsize = (synchronizationParameters.stepSize * 100.00) / ((double)count * (double)totalDuration);
            string messageDownloadingManiphestData = Miscellaneous.Locale.TranslateText("Synchronization.Status.DownloadingManiphestData", browser.Session.Locale);
            foreach (Phabricator.Data.Maniphest phabricatorManiphestTask in phabricatorManiphestTasks)
            {
                SharedResource.Instance.ProgressDescription = string.Format("{0} [{1}/{2}]", messageDownloadingManiphestData, index++, count);
                SharedResource.Instance.ProgressPercentage += stepsize;

                // sleep a bit, so the progress-bar is shown a little more animated...
                if ((index % 100) == 0) Thread.Sleep(100);

                // add sync logging
                if (synchronizationParameters.previousSyncMode == SyncMode.Full)
                {
                    Storage.SynchronizationLogging synchronizationLoggingStorage = new Storage.SynchronizationLogging();
                    Phabricator.Data.SynchronizationLogging synchronizationLogging = new Phabricator.Data.SynchronizationLogging();
                    synchronizationLogging.Token = phabricatorManiphestTask.Token;
                    synchronizationLogging.DateModified = phabricatorManiphestTask.DateModified;
                    synchronizationLogging.LastModifiedBy = phabricatorManiphestTask.LastModifiedBy;
                    synchronizationLogging.Title = phabricatorManiphestTask.Name;
                    synchronizationLogging.URL = "maniphest/T" + phabricatorManiphestTask.ID + "/";

                    if (string.IsNullOrEmpty(synchronizationLogging.LastModifiedBy))
                    {
                        // task is created with default settings and no metadata: take first subscriber as the 'LastModifier'
                        synchronizationLogging.LastModifiedBy = phabricatorManiphestTask.Subscribers.Split(',').FirstOrDefault();
                    }

                    Phabricator.Data.Maniphest previousData = maniphestStorage.Get(synchronizationParameters.database, phabricatorManiphestTask.Token, synchronizationParameters.browser.Session.Locale, true);
                    if (previousData == null)
                    {
                        if (loggedManiphestTokens.Contains(phabricatorManiphestTask.Token) == false)
                        {
                            // new data
                            synchronizationLogging.PreviousContent = null;
                            synchronizationLoggingStorage.Add(synchronizationParameters.database, synchronizationLogging);
                        }
                    }
                    else
                    {
                        if (previousData.Description.Equals(phabricatorManiphestTask.Description) == false)
                        {
                            // modified data
                            synchronizationLogging.PreviousContent = previousData.Description;
                            synchronizationLoggingStorage.Add(synchronizationParameters.database, synchronizationLogging);

                            // make sure we don't log this maniphest again in the sync-logging (otherwise the previousContent is overwritten)
                            loggedManiphestTokens.Add(phabricatorManiphestTask.Token);
                        }
                        else
                        {
                            if (loggedManiphestTokens.Contains(phabricatorManiphestTask.Token) == false)
                            {
                                // modified/new comment, state, priority
                                synchronizationLogging.MetadataIsModified = true;
                                synchronizationLoggingStorage.Add(synchronizationParameters.database, synchronizationLogging);
                            }
                        }
                    }
                }

                synchronizationParameters.database.MarkFileObject(null, false, phabricatorManiphestTask.Token);
                maniphestStorage.Add(synchronizationParameters.database, phabricatorManiphestTask);

                // collect all file object references used in the maniphest task content
                CollectFileObjectsFromContent(phabricatorManiphestTask.Token, phabricatorManiphestTask.Description, ref synchronizationParameters.fileObjectsPerToken);

                // collect all diagram object references used in the maniphest task content
                CollectDiagramObjectsFromContent(phabricatorManiphestTask.Token, phabricatorManiphestTask.Description, ref synchronizationParameters.diagramObjectsPerToken);

                // collect all transactions/comments for current maniphest task
                foreach (Phabricator.Data.Transaction transaction in phabricatorManiphestTask.Transactions)
                {
                    if (transaction.Type == "comment")
                    {
                        CollectFileObjectsFromContent(phabricatorManiphestTask.Token, transaction.NewValue, ref synchronizationParameters.fileObjectsPerToken);
                    }

                    transactionStorage.Add(synchronizationParameters.database, transaction);
                }

                // parse task content
                RemarkupParserOutput remarkupParserOutput;
                ConvertRemarkupToHTML(synchronizationParameters.database, "/", phabricatorManiphestTask.Description, out remarkupParserOutput, false, phabricatorManiphestTask.Token);

                // get all available words from task content and save it into search database
                keywordStorage.DeletePhabricatorObject(synchronizationParameters.database, phabricatorManiphestTask);
                keywordStorage.AddPhabricatorObject(this, synchronizationParameters.database, phabricatorManiphestTask);

                // remember comments before clearing transactions
                Phabricator.Data.Transaction[] linkedComments = transactionStorage.GetAll(synchronizationParameters.database, phabricatorManiphestTask.Token, Language.NotApplicable)
                                                                                  .Where(transaction => transaction.Type.Equals("comment"))
                                                                                  .ToArray();

                // (re)assign dependent Phabricator objects
                synchronizationParameters.database.ClearAssignedTokens(phabricatorManiphestTask.Token, Language.NotApplicable);
                foreach (Phabricator.Data.PhabricatorObject linkedPhabricatorObject in remarkupParserOutput.LinkedPhabricatorObjects)
                {
                    synchronizationParameters.database.AssignToken(phabricatorManiphestTask.Token, linkedPhabricatorObject.Token, Language.NotApplicable);
                }

                // (re)assign dependent Phabricator objects found in comments
                foreach (Phabricator.Data.Transaction linkedComment in linkedComments)
                {
                    ConvertRemarkupToHTML(synchronizationParameters.database, "/", linkedComment.NewValue, out remarkupParserOutput, false, phabricatorManiphestTask.Token);
                    foreach (Phabricator.Data.PhabricatorObject linkedPhabricatorObject in remarkupParserOutput.LinkedPhabricatorObjects)
                    {
                        synchronizationParameters.database.AssignToken(phabricatorManiphestTask.Token, linkedPhabricatorObject.Token, Language.NotApplicable);
                    }
                }
            }
        }

        /// <summary>
        /// Downloads Phame blog posts from Phabricator
        /// </summary>
        /// <param name="synchronizationParameters"></param>
        /// <param name="processedDuration"></param>
        /// <param name="totalDuration"></param>
        public void ProgressMethod_DownloadPhamePosts(SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration)
        {
            SharedResource.Instance.ProgressDescription = Miscellaneous.Locale.TranslateText("Synchronization.Status.DownloadingPhamePostData", browser.Session.Locale);
            SharedResource.Instance.ProgressPercentage += synchronizationParameters.stepSize;

            Storage.Keyword keywordStorage = new Storage.Keyword();
            Storage.PhamePost phamePostStorage = new Storage.PhamePost();
            Phabricator.API.PhamePost phabricatorPhamePostAPI = new Phabricator.API.PhamePost();

            phabricatorPhamePostAPI.TimeDifferenceBetweenPhabricatorAndLocalComputer = synchronizationParameters.TimeDifferenceBetweenPhabricatorAndLocalComputer;

            DateTimeOffset lastDownloadTimestamp = synchronizationParameters.lastDownloadTimestamp;
            if (phamePostStorage.Get(synchronizationParameters.database, synchronizationParameters.browser.Session.Locale).Any() == false)
            {
                lastDownloadTimestamp = new DateTimeOffset(1970, 1, 1, 0, 0, 1, new TimeSpan());
            }


            List<Phabricator.Data.PhamePost> newPhamePosts = phabricatorPhamePostAPI.GetAll(synchronizationParameters.database,
                                                                                            synchronizationParameters.browser.Conduit,
                                                                                            new Phabricator.API.Constraint[0],
                                                                                            lastDownloadTimestamp)
                                                                                    .ToList();
            foreach (Phabricator.Data.PhamePost newPhamePost in newPhamePosts)
            {
                phamePostStorage.Add(synchronizationParameters.database, newPhamePost);

                Storage.SynchronizationLogging synchronizationLoggingStorage = new Storage.SynchronizationLogging();
                Phabricator.Data.SynchronizationLogging synchronizationLogging = new Phabricator.Data.SynchronizationLogging();
                synchronizationLogging.Token = newPhamePost.Token;
                synchronizationLogging.DateModified = newPhamePost.DateModified;
                synchronizationLogging.LastModifiedBy = newPhamePost.Author;
                synchronizationLogging.Title = newPhamePost.Blog + ": " + newPhamePost.Title;
                synchronizationLogging.URL = "phame/post/" + newPhamePost.ID + "/";
                synchronizationLogging.PreviousContent = null;
                synchronizationLoggingStorage.Add(synchronizationParameters.database, synchronizationLogging);

                // collect all file object references used in the maniphest task content
                CollectFileObjectsFromContent(newPhamePost.Token, newPhamePost.Content, ref synchronizationParameters.fileObjectsPerToken);

                // collect all diagram object references used in the maniphest task content
                CollectDiagramObjectsFromContent(newPhamePost.Token, newPhamePost.Content, ref synchronizationParameters.diagramObjectsPerToken);

                // parse task content
                RemarkupParserOutput remarkupParserOutput;
                ConvertRemarkupToHTML(synchronizationParameters.database, "/", newPhamePost.Content, out remarkupParserOutput, false, newPhamePost.Token);

                // get all available words from task content and save it into search database
                keywordStorage.DeletePhabricatorObject(synchronizationParameters.database, newPhamePost);
                keywordStorage.AddPhabricatorObject(this, synchronizationParameters.database, newPhamePost);

                // (re)assign dependent Phabricator objects
                synchronizationParameters.database.ClearAssignedTokens(newPhamePost.Token, Language.NotApplicable);
                foreach (Phabricator.Data.PhabricatorObject linkedPhabricatorObject in remarkupParserOutput.LinkedPhabricatorObjects)
                {
                    synchronizationParameters.database.AssignToken(newPhamePost.Token, linkedPhabricatorObject.Token, Language.NotApplicable);
                }
            }
        }

        /// <summary>
        /// Downloads Phriction documents from Phabricator
        /// </summary>
        /// <param name="synchronizationParameters"></param>
        /// <param name="processedDuration"></param>
        /// <param name="totalDuration"></param>
        public void ProgressMethod_DownloadPhrictionDocuments(SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration)
        {
            Phabricator.API.Phriction phabricatorPhrictionAPI = new Phabricator.API.Phriction();
            Storage.Phriction phrictionStorage = new Storage.Phriction();
            Storage.Keyword keywordStorage = new Storage.Keyword();
            Content content = new Content(synchronizationParameters.database);

            phabricatorPhrictionAPI.TimeDifferenceBetweenPhabricatorAndLocalComputer = synchronizationParameters.TimeDifferenceBetweenPhabricatorAndLocalComputer;

            // if the root-document is aliased, remove the alias
            Phabricator.Data.Phriction rootAlias = phrictionStorage.GetAliases(synchronizationParameters.database)
                                                                   .FirstOrDefault(alias => alias.Path.Equals("/"));
            if (rootAlias != null)
            {
                phrictionStorage.Remove(synchronizationParameters.database, rootAlias);
            }

            // initialize root page urls
            if (synchronizationParameters.initialPhrictionPaths.Length != synchronizationParameters.initialPhrictionPathAliases.Length)
            {
                List<List<string>> pathElements = synchronizationParameters.initialPhrictionPaths.Select(path => path.Split('/').ToList()).ToList();
                int i = 0;
                while (true)
                {
                    if (pathElements.Any(segments => segments[0] != pathElements[0][0])) break;
                    pathElements = pathElements.Select(segments => segments.Skip(1).ToList()).ToList();

                    if (pathElements.Any(segments => segments.Any() == false)) break;
                    i++;
                }
                string commonParentPath = string.Join("/", synchronizationParameters.initialPhrictionPaths.First().Split('/').Take(i)) + "/";

                // set root page urls
                synchronizationParameters.initialPhrictionPathAliases = new string[synchronizationParameters.initialPhrictionPaths.Length];
                for (i = 0; i < synchronizationParameters.initialPhrictionPaths.Length; i++)
                {
                    synchronizationParameters.initialPhrictionPathAliases[i] = synchronizationParameters.initialPhrictionPaths[i].Substring(commonParentPath.Length);
                }
            }

            // prepare loading phriction documents: check if project filter (constraint) should be applied
            Phabricator.API.Constraint[] phabricatorPhrictionDocumentConstraints = new Phabricator.API.Constraint[0];
            if (synchronizationParameters.existingAccount.Parameters.Synchronization.HasFlag(Phabricator.Data.Account.SynchronizationMethod.PhrictionAllSelectedProjectsOnly) &&
                synchronizationParameters.existingAccount.Parameters.Synchronization.HasFlag(Phabricator.Data.Account.SynchronizationMethod.PhrictionAllSelectedProjectsOnlyIncludingDocumentTree) == false
               )
            {
                Phabricator.API.Constraint constraintActivatedProjects = new Phabricator.API.Constraint("projects", synchronizationParameters.projectSelected.Where(project => project.Value == Phabricator.Data.Project.Selection.Selected)
                                                                                                                                    .Select(project => project.Key)
                                                                                                                                    .ToArray());

                phabricatorPhrictionDocumentConstraints = new Phabricator.API.Constraint[] { constraintActivatedProjects };
            }

            // determine project tags which should not be downloaded
            string[] disallowedProjectTags = synchronizationParameters.projectSelected
                                                                      .Where(project => project.Key.Equals(Phabricator.Data.Project.None) == false)
                                                                      .Where(project => project.Value == Phabricator.Data.Project.Selection.Disallowed)
                                                                      .Select(project => project.Key)
                                                                      .ToArray();

            // check if we need to download all Phriction documents
            DateTimeOffset lastDownloadTimestamp = synchronizationParameters.lastDownloadTimestamp;
            if (synchronizationParameters.stagedDataHasBeenUploaded && synchronizationParameters.existingAccount.Parameters.ForceDownloadAllPhrictionMetadata)
            {
                lastDownloadTimestamp = new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 1));
            }

            // load all phriction documents from phabricator
            string messageDownloadingPhrictionData = Miscellaneous.Locale.TranslateText("Synchronization.Status.DownloadingPhrictionData", browser.Session.Locale);
            SharedResource.Instance.ProgressDescription = messageDownloadingPhrictionData;
            List<Phabricator.Data.Phriction> phabricatorPhrictionDocuments;
            if (synchronizationParameters.existingAccount.Parameters.Synchronization != SynchronizationMethod.All &&
                synchronizationParameters.existingAccount.Parameters.Synchronization.HasFlag(Phabricator.Data.Account.SynchronizationMethod.PhrictionAllSelectedProjectsOnly) &&
                synchronizationParameters.existingAccount.Parameters.Synchronization.HasFlag(Phabricator.Data.Account.SynchronizationMethod.PhrictionAllSelectedProjectsOnlyIncludingDocumentTree) == false
               )
            {
                if (synchronizationParameters.incrementalDownload)
                {
                    phabricatorPhrictionDocuments = phabricatorPhrictionAPI.GetModifiedPhrictionDocuments(synchronizationParameters.database,
                                                                                   synchronizationParameters.browser.Conduit,
                                                                                   phabricatorPhrictionDocumentConstraints,
                                                                                   lastDownloadTimestamp)
                                                                           .ToList();
                }
                else
                {
                    phabricatorPhrictionDocuments = phabricatorPhrictionAPI.GetPhrictionDocuments(synchronizationParameters.database,
                                                                                   synchronizationParameters.browser.Conduit,
                                                                                   phabricatorPhrictionDocumentConstraints)
                                                                           .ToList();
                }
            }
            else
            if (synchronizationParameters.existingAccount.Parameters.Synchronization != SynchronizationMethod.All &&
                synchronizationParameters.existingAccount.Parameters.Synchronization.HasFlag(Phabricator.Data.Account.SynchronizationMethod.PhrictionSelectedProjectsOnly))
            {
                phabricatorPhrictionDocuments = new List<Phabricator.Data.Phriction>();
                foreach (string selectedProject in synchronizationParameters.projectSelected.Where(project => project.Value == Phabricator.Data.Project.Selection.Selected)
                                                                                            .Select(project => project.Key))
                {
                    if (synchronizationParameters.incrementalDownload)
                    {
                        phabricatorPhrictionDocuments.AddRange(phabricatorPhrictionAPI.GetModifiedPhrictionDocuments(synchronizationParameters.database,
                                                                                               synchronizationParameters.browser.Conduit,
                                                                                               phabricatorPhrictionDocumentConstraints,
                                                                                               lastDownloadTimestamp)
                                                              );
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(synchronizationParameters.initialPhrictionPaths?.FirstOrDefault()) == false)
                        {
                            string[] constraintInitialPaths = synchronizationParameters.initialPhrictionPaths
                                .Select(constraintInitialPath => constraintInitialPath.StartsWith("w/", StringComparison.OrdinalIgnoreCase)
                                            ? constraintInitialPath.Substring("w/".Length)
                                            : constraintInitialPath
                                ).ToArray();

                            Phabricator.API.Constraint[] rootPageConstraints = new Phabricator.API.Constraint[] {
                                phabricatorPhrictionDocumentConstraints.FirstOrDefault(),
                                new Phabricator.API.Constraint("paths", constraintInitialPaths)
                            };

                            rootPageConstraints = rootPageConstraints.Where(c => c != null).ToArray();

                            phabricatorPhrictionDocuments.AddRange(
                                phabricatorPhrictionAPI.GetPhrictionDocuments(synchronizationParameters.database,
                                                                              synchronizationParameters.browser.Conduit,
                                                                              rootPageConstraints));

                            if (synchronizationParameters.existingAccount.Parameters.Synchronization.HasFlag(Phabricator.Data.Account.SynchronizationMethod.PhrictionSelectedProjectsOnlyIncludingDocumentTree))
                            {
                                string[] selectedProjectPHIDs = synchronizationParameters.projectSelected
                                                                                         .Where(sel => sel.Value == Phabricator.Data.Project.Selection.Selected)
                                                                                         .Select(sel => sel.Key)
                                                                                         .ToArray();
                                string[] rootPageProjectPHIDs = phabricatorPhrictionDocuments.FirstOrDefault()?.Projects.Split(',');
                                if (rootPageProjectPHIDs != null && Array.TrueForAll(selectedProjectPHIDs, phid => rootPageProjectPHIDs.Contains(phid)) == false)
                                {
                                    Phabricator.API.Constraint[] childPageConstraints = new Phabricator.API.Constraint[] {
                                        phabricatorPhrictionDocumentConstraints.FirstOrDefault(),
                                        new Phabricator.API.Constraint("parentPaths", constraintInitialPaths)
                                    };

                                    childPageConstraints = childPageConstraints.Where(c => c != null).ToArray();

                                    phabricatorPhrictionDocuments.AddRange(phabricatorPhrictionAPI.GetPhrictionDocuments(synchronizationParameters.database,
                                                                                                                         synchronizationParameters.browser.Conduit,
                                                                                                                         childPageConstraints)
                                                                          );
                                }
                            }
                        }
                        else
                        {
                            phabricatorPhrictionDocuments.AddRange(phabricatorPhrictionAPI.GetPhrictionDocuments(synchronizationParameters.database,
                                                                                                                 synchronizationParameters.browser.Conduit,
                                                                                                                 phabricatorPhrictionDocumentConstraints)
                                                                  );
                        }
                    }
                }
            }
            else
            {
                if (synchronizationParameters.incrementalDownload)
                {
                    phabricatorPhrictionDocuments = phabricatorPhrictionAPI.GetModifiedPhrictionDocuments(synchronizationParameters.database,
                                                                                   synchronizationParameters.browser.Conduit,
                                                                                   phabricatorPhrictionDocumentConstraints,
                                                                                   lastDownloadTimestamp)
                                                                           .ToList();
                }
                else
                {
                    if (phabricatorPhrictionDocumentConstraints.Any() == false && string.IsNullOrWhiteSpace(synchronizationParameters.initialPhrictionPaths?.FirstOrDefault()) == false)
                    {
                        string[] constraintInitialPaths = synchronizationParameters.initialPhrictionPaths
                            .Select(constraintInitialPath => constraintInitialPath.StartsWith("w/", StringComparison.OrdinalIgnoreCase)
                                        ? constraintInitialPath.Substring("w/".Length)
                                        : constraintInitialPath
                            ).ToArray();

                        phabricatorPhrictionDocumentConstraints = new Phabricator.API.Constraint[] {
                            new Phabricator.API.Constraint("parentPaths", constraintInitialPaths)
                        };
                    }

                    phabricatorPhrictionDocuments = phabricatorPhrictionAPI.GetPhrictionDocuments(synchronizationParameters.database,
                                                                                   synchronizationParameters.browser.Conduit,
                                                                                   phabricatorPhrictionDocumentConstraints)
                                                                           .ToList();
                }
            }

            List<Phabricator.Data.Phriction> forbiddenDocuments = phabricatorPhrictionDocuments.Where(document => document.Projects
                                                                                                                          .Split(',')
                                                                                                                          .Any(project => disallowedProjectTags.Contains(project))
                                                                                                     )
                                                                                               .ToList();

            phabricatorPhrictionDocuments = phabricatorPhrictionDocuments.Where(document => forbiddenDocuments.Contains(document) == false).ToList();

            if (phabricatorPhrictionDocuments.Any())
            {
                // load child documents also if necessary
                if (synchronizationParameters.existingAccount.Parameters.Synchronization.HasFlag(Phabricator.Data.Account.SynchronizationMethod.PhrictionSelectedProjectsOnlyIncludingDocumentTree)
                    || synchronizationParameters.existingAccount.Parameters.Synchronization.HasFlag(Phabricator.Data.Account.SynchronizationMethod.PhrictionOnlyIncludingDocumentTree)
                   )
                {
                    List<string> ancestorPages = phabricatorPhrictionDocuments.Select(document => document.Path).OrderBy(path => path).ToList();
                    if (ancestorPages.Contains("/")) ancestorPages = new List<string>(new string[] { "/" });
                    foreach (string path in ancestorPages.ToList())
                    {
                        ancestorPages.RemoveAll(p => p.Equals(path) == false && p.StartsWith(path));
                    }

                    for (int chunk = 0; ; chunk += 99)
                    {
                        string[] currentBlockAncestorPages = ancestorPages.Skip(chunk * 99)
                                                                          .Take(99)
                                                                          .ToArray();
                        if (currentBlockAncestorPages.Any() == false) break;

                        Phabricator.API.Constraint constraintChildPages = new Phabricator.API.Constraint("ancestorPaths", currentBlockAncestorPages);
                        phabricatorPhrictionDocumentConstraints = new Phabricator.API.Constraint[] { constraintChildPages };

                        List<Phabricator.Data.Phriction> chilDocuments;
                        if (synchronizationParameters.incrementalDownload)
                        {
                            chilDocuments = phabricatorPhrictionAPI.GetModifiedPhrictionDocuments(synchronizationParameters.database,
                                                                           synchronizationParameters.browser.Conduit,
                                                                           phabricatorPhrictionDocumentConstraints,
                                                                           synchronizationParameters.lastDownloadTimestamp)
                                                                   .ToList();
                        }
                        else
                        {
                            chilDocuments = phabricatorPhrictionAPI.GetPhrictionDocuments(synchronizationParameters.database,
                                                                           synchronizationParameters.browser.Conduit,
                                                                           phabricatorPhrictionDocumentConstraints)
                                                                   .ToList();
                        }

                        foreach (Phabricator.Data.Phriction childDocument in chilDocuments)
                        {
                            if (phabricatorPhrictionDocuments.Any(document => document.Token == childDocument.Token) == false)
                            {
                                phabricatorPhrictionDocuments.Add(childDocument);
                            }
                        }
                    }
                }
            }

            List<Phabricator.Data.Phriction> phabricatorPhrictionDocumentsInCorrectOrder = phabricatorPhrictionDocuments.Where(rootDocument => rootDocument.Path.Equals("/"))
                                                                                                                        .Concat(phabricatorPhrictionDocuments.OrderBy(document => document.Path))
                                                                                                                        .Distinct()
                                                                                                                        .ToList();

            if (synchronizationParameters.existingAccount.Parameters.Synchronization.HasFlag(Phabricator.Data.Account.SynchronizationMethod.All) == false
                && synchronizationParameters.existingAccount.Parameters.Synchronization.HasFlag(Phabricator.Data.Account.SynchronizationMethod.PhrictionAllSelectedProjectsOnlyIncludingDocumentTree)
               )
            {
                string[] selectedProjects = synchronizationParameters.projectSelected.Where(p => p.Key.Equals(Phabricator.Data.Project.None) == false
                                                                                              && p.Value == Phabricator.Data.Project.Selection.Selected
                                                                                           )
                                                                                     .Select(p => p.Key)
                                                                                     .OrderBy(p => p)
                                                                                     .ToArray();
                var projectsPerProjectedDocuments = phabricatorPhrictionDocuments.Where(document => document.Projects.Split(',')
                                                                                                            .Any(p => selectedProjects.Contains(p))
                                                                                       )
                                                                                 .ToDictionary(key => key, value => new List<string>());
                foreach (var project in projectsPerProjectedDocuments.Keys)
                {
                    foreach (string assignedProject in project.Projects.Split(','))
                    {
                        if (selectedProjects.Contains(assignedProject))
                        {
                            projectsPerProjectedDocuments[project].Add(assignedProject);
                        }
                    }
                }

                foreach (var document in projectsPerProjectedDocuments.Keys.ToArray())
                {
                    foreach (string missingProject in selectedProjects.Where(p => projectsPerProjectedDocuments[document].Contains(p) == false))
                    {
                        if (projectsPerProjectedDocuments.Any(p => p.Value.Contains(missingProject) && document.Path.StartsWith(p.Key.Path)))
                        {
                            projectsPerProjectedDocuments[document].Add(missingProject);
                        }
                    }
                }

                Phabricator.Data.Phriction[] documentsWithCombinedProjects = projectsPerProjectedDocuments.Where(kvp => kvp.Value.OrderBy(v => v).SequenceEqual(selectedProjects))
                                                                                                          .Select(kvp => kvp.Key)
                                                                                                          .ToArray();
                phabricatorPhrictionDocumentsInCorrectOrder.RemoveAll(document => documentsWithCombinedProjects.All(d => document.Path.StartsWith(d.Path) == false));
            }

            if (string.IsNullOrWhiteSpace(synchronizationParameters.initialPhrictionPaths?.FirstOrDefault()) == false)
            {
                for (int i = 0; i < synchronizationParameters.initialPhrictionPaths.Length; i++)
                {
                    if (synchronizationParameters.initialPhrictionPaths[i].StartsWith("w/"))
                        synchronizationParameters.initialPhrictionPaths[i] = synchronizationParameters.initialPhrictionPaths[i].Substring("w/".Length);
                }

                phabricatorPhrictionDocumentsInCorrectOrder.RemoveAll(document => synchronizationParameters.initialPhrictionPaths.Any(path => document.Path.StartsWith(path)) == false);
            }

            int index = 0;
            int count = phabricatorPhrictionDocumentsInCorrectOrder.Count;
            double stepsize = (synchronizationParameters.stepSize * 100.00) / ((double)count * (double)totalDuration);

            foreach (Phabricator.Data.Phriction phabricatorPhrictionDocument in phabricatorPhrictionDocumentsInCorrectOrder)
            {
                SharedResource.Instance.ProgressDescription = string.Format("{0} [{1}/{2}]", messageDownloadingPhrictionData, index++, count);
                SharedResource.Instance.ProgressPercentage += stepsize;

                // sleep a bit, so the progress-bar is shown a little more animated...
                if ((index % 100) == 0) Thread.Sleep(100);

                // add sync logging
                if (synchronizationParameters.previousSyncMode == SyncMode.Full)
                {
                    Storage.SynchronizationLogging synchronizationLoggingStorage = new Storage.SynchronizationLogging();
                    Phabricator.Data.SynchronizationLogging synchronizationLogging = new Phabricator.Data.SynchronizationLogging();
                    synchronizationLogging.Token = phabricatorPhrictionDocument.Token;
                    synchronizationLogging.DateModified = phabricatorPhrictionDocument.DateModified;
                    synchronizationLogging.LastModifiedBy = phabricatorPhrictionDocument.LastModifiedBy;
                    synchronizationLogging.Title = phabricatorPhrictionDocument.Name;
                    synchronizationLogging.URL = "w/" + phabricatorPhrictionDocument.Path;

                    Phabricator.Data.Phriction previousData = phrictionStorage.Get(synchronizationParameters.database, phabricatorPhrictionDocument.Token, synchronizationParameters.browser.Session.Locale, true);
                    if (previousData == null)
                    {
                        // new data
                        synchronizationLogging.PreviousContent = null;
                        synchronizationLoggingStorage.Add(synchronizationParameters.database, synchronizationLogging);
                    }
                    else
                    {
                        // modified data
                        if (previousData.Content.Equals(phabricatorPhrictionDocument.Content) == false)
                        {
                            synchronizationLogging.PreviousContent = previousData.Content;
                            synchronizationLoggingStorage.Add(synchronizationParameters.database, synchronizationLogging);
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(synchronizationParameters.initialPhrictionPaths?.FirstOrDefault()) == false)
                {
                    string initialPath = synchronizationParameters.initialPhrictionPaths.OrderByDescending(path => path.Length).FirstOrDefault(path => phabricatorPhrictionDocument.Path.StartsWith(path));
                    int initialPathIndex = Array.IndexOf(synchronizationParameters.initialPhrictionPaths, initialPath);
                    string initialPathAlias = synchronizationParameters.initialPhrictionPathAliases[initialPathIndex];

                    phabricatorPhrictionDocument.Path = phabricatorPhrictionDocument.Path.Substring(initialPath.Length);
                    if (string.IsNullOrEmpty(phabricatorPhrictionDocument.Path))
                    {
                        phabricatorPhrictionDocument.Path = initialPathAlias.TrimEnd('/') + "/";
                    }
                    else
                    {
                        phabricatorPhrictionDocument.Path = (initialPathAlias.TrimEnd('/') + "/" + phabricatorPhrictionDocument.Path).TrimStart('/');
                    }
                }

                synchronizationParameters.database.MarkFileObject(null, false, phabricatorPhrictionDocument.Token);
                phrictionStorage.Add(synchronizationParameters.database, phabricatorPhrictionDocument);

                if (phabricatorPhrictionDocument.DateModified >= synchronizationParameters.lastDownloadTimestamp)
                {
                    // collect all file object references used in the phriction document content
                    CollectFileObjectsFromContent(phabricatorPhrictionDocument.Token, phabricatorPhrictionDocument.Content, ref synchronizationParameters.fileObjectsPerToken);

                    // collect all diagram object references used in the maniphest task content
                    CollectDiagramObjectsFromContent(phabricatorPhrictionDocument.Token, phabricatorPhrictionDocument.Content, ref synchronizationParameters.diagramObjectsPerToken);

                    // identify parent document
                    string[] urlParts = phabricatorPhrictionDocument.Path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (urlParts.Any())
                    {
                        string parentUrl = string.Join("/", urlParts.Take(urlParts.Length - 1)) + "/";
                        Phabricator.Data.Phriction parentDocument = phrictionStorage.Get(synchronizationParameters.database, parentUrl, synchronizationParameters.browser.Session.Locale);
                        if (parentDocument != null)
                        {
                            synchronizationParameters.database.DescendTokenFrom(parentDocument.Token, phabricatorPhrictionDocument.Token);
                        }
                    }

                    // parse document content
                    RemarkupParserOutput remarkupParserOutput;
                    ConvertRemarkupToHTML(synchronizationParameters.database, "/", phabricatorPhrictionDocument.Content, out remarkupParserOutput, false, phabricatorPhrictionDocument.Token);

                    // get all available words from phriction document and save it into search database
                    keywordStorage.DeletePhabricatorObject(synchronizationParameters.database, phabricatorPhrictionDocument);
                    keywordStorage.AddPhabricatorObject(this, synchronizationParameters.database, phabricatorPhrictionDocument);

                    // (re)assign dependent Phabricator objects
                    synchronizationParameters.database.ClearAssignedTokens(phabricatorPhrictionDocument.Token, Language.NotApplicable);
                    foreach (Phabricator.Data.PhabricatorObject linkedPhabricatorObject in remarkupParserOutput.LinkedPhabricatorObjects)
                    {
                        synchronizationParameters.database.AssignToken(phabricatorPhrictionDocument.Token, linkedPhabricatorObject.Token, Language.NotApplicable);
                    }
                }

                content.DisapproveTranslationForAllLanguagesIfOlderThan(phabricatorPhrictionDocument.Token, phabricatorPhrictionDocument.DateModified);
            }

            // delete any local document tagged with a disallowed project
            foreach (Phabricator.Data.Phriction forbiddenDocument in forbiddenDocuments)
            {
                phrictionStorage.Remove(synchronizationParameters.database, forbiddenDocument);
            }

            if (synchronizationParameters.incrementalDownload)
            {
                string messageDownloadingPhrictionMetaData = Miscellaneous.Locale.TranslateText("Synchronization.Status.DownloadingPhrictionMetaData", browser.Session.Locale);
                SharedResource.Instance.ProgressDescription = messageDownloadingPhrictionMetaData;
                phabricatorPhrictionAPI.DownloadModifiedMetadataPhrictionDocuments(synchronizationParameters.database, synchronizationParameters.browser.Conduit);
            }
        }

        /// <summary>
        /// Downloads projects from Phabricator
        /// </summary>
        /// <param name="synchronizationParameters"></param>
        /// <param name="processedDuration"></param>
        /// <param name="totalDuration"></param>
        public void ProgressMethod_DownloadProjects(SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration)
        {
            Phabricator.API.Project phabricatorProjectAPI = new Phabricator.API.Project();
            Storage.Project projectStorage = new Storage.Project();

            // check if untagged-dummy-project exists
            if (projectStorage.Get(synchronizationParameters.database, Phabricator.Data.Project.None, Language.NotApplicable) == null)
            {
                // create dummy project for untagged Phriction documents or Maniphest tasks
                Phabricator.Data.Project dummyProject = new Phabricator.Data.Project();
                dummyProject.Description = "(No project)";
                dummyProject.InternalName = "(No project)";
                dummyProject.Name = "(No project)";
                dummyProject.Selected = Phabricator.Data.Project.Selection.Selected;
                dummyProject.Token = Phabricator.Data.Project.None;
                projectStorage.Add(synchronizationParameters.database, dummyProject);
            }

            // load all current projects (to remember which are currently selected)
            SharedResource.Instance.ProgressDescription = Miscellaneous.Locale.TranslateText("Synchronization.Status.DownloadingProjects", browser.Session.Locale);
            synchronizationParameters.projectSelected = projectStorage.Get(synchronizationParameters.database, synchronizationParameters.browser.Session.Locale)
                                                                      .ToDictionary(
                                                                            key => key.Token,
                                                                            value => value.Selected
                                                                       );

            // load all projects from Phabricator
            IEnumerable<Phabricator.Data.Project> phabricatorProjects = phabricatorProjectAPI.GetAll(synchronizationParameters.database,
                                                                                                     synchronizationParameters.browser.Conduit,
                                                                                                     synchronizationParameters.lastSynchronizationTimestamp
                                                                                                    );
            foreach (Phabricator.Data.Project phabricatorProject in phabricatorProjects)
            {
                Phabricator.Data.Project.Selection selected;
                if (synchronizationParameters.projectSelected.TryGetValue(phabricatorProject.Token, out selected) == false)
                {
                    selected = Phabricator.Data.Project.Selection.Unselected;
                }

                // get DateSynchronized and color
                Phabricator.Data.Project localProject = projectStorage.Get(synchronizationParameters.database, phabricatorProject.Token, synchronizationParameters.browser.Session.Locale);
                if (localProject != null)
                {
                    phabricatorProject.Color = localProject.Color;
                    phabricatorProject.DateSynchronized = localProject.DateSynchronized;
                }

                phabricatorProject.Selected = selected;

                projectStorage.Add(synchronizationParameters.database, phabricatorProject);
            }
            SharedResource.Instance.ProgressPercentage += synchronizationParameters.stepSize;

            // load all current users (to remember which are currently selected)
            Storage.User userStorage = new Storage.User();
            synchronizationParameters.userSelected = userStorage.Get(synchronizationParameters.database, synchronizationParameters.browser.Session.Locale)
                                                                .ToDictionary(
                                                                    key => key.Token,
                                                                    value => value.Selected
                                                                );
        }

        /// <summary>
        /// Downloads unreferenced files from Phabricator
        /// </summary>
        /// <param name="synchronizationParameters"></param>
        /// <param name="processedDuration"></param>
        /// <param name="totalDuration"></param>
        public void ProgressMethod_DownloadUnreferencedFileObjects(SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration)
        {
            Phabricator.API.File phabricatorFileAPI = new Phabricator.API.File();
            Storage.File fileStorage = new Storage.File();

            // load all file objects from phabricator which were referenced in the downloaded phriction and maniphest objects
            List<int> fileIDsToDownload = synchronizationParameters.database
                                                                   .GetAllMarkedFileIDs()
                                                                   .Select(f => f.FileID)
                                                                   .ToList();

            string messageLoadingFileObjects = Miscellaneous.Locale.TranslateText("Synchronization.Status.LoadingUnreferencedFileObjects", browser.Session.Locale) + " [" + fileIDsToDownload.Count + "]";
            SharedResource.Instance.ProgressDescription = messageLoadingFileObjects;

            IEnumerable<Phabricator.Data.File> phabricatorFileReferences = phabricatorFileAPI.GetReferences(synchronizationParameters.database,
                                                                                                            synchronizationParameters.browser.Conduit,
                                                                                                            fileIDsToDownload
                                                                                                           );

            int index = 0;
            int count = phabricatorFileReferences.Count();
            double stepsize = (synchronizationParameters.stepSize * 100.00) / ((double)count * (double)totalDuration);
            foreach (Phabricator.Data.File phabricatorFileReference in phabricatorFileReferences)
            {
                string size;
                if (phabricatorFileReference.Size > 1024 * 1024)
                {
                    size = string.Format("{0} MB", phabricatorFileReference.Size / (1024*1024));
                }
                else
                if (phabricatorFileReference.Size > 1024)
                {
                    size = string.Format("{0} KB", phabricatorFileReference.Size / 1024);
                }
                else
                {
                    size = string.Format("{0} bytes", phabricatorFileReference.Size);
                }

                SharedResource.Instance.ProgressDescription = string.Format("{0} [{1}/{2}] ({3})", messageLoadingFileObjects, index++, count, size);
                SharedResource.Instance.ProgressPercentage += stepsize;

                // sleep a bit, so the progress-bar is shown a little more animated...
                if ((index % 100) == 0) Thread.Sleep(100);

                Phabricator.Data.File phabricatorFile = new Phabricator.Data.File(phabricatorFileReference);
                Base64EIDOStream base64EIDOStream = phabricatorFileAPI.DownloadData(synchronizationParameters.browser.Conduit, phabricatorFileReference.Token);
                base64EIDOStream.Seek(0, System.IO.SeekOrigin.Begin);
                phabricatorFile.DataStream = base64EIDOStream;
                fileStorage.Add(synchronizationParameters.database, phabricatorFile);

                // link file-references to their owners (e.g. Phriction document or Maniphest task)
                foreach (string owner in synchronizationParameters.fileObjectsPerToken.Where(kvp => kvp.Value.Contains(phabricatorFile.ID)).Select(kvp => kvp.Key))
                {
                    synchronizationParameters.database.AssignToken(owner, phabricatorFile.Token, Language.NotApplicable);
                }
            }

            int nbrMarkedFileObjects = synchronizationParameters.database.GetAllMarkedFileIDs().Count();
            Http.Server.SendNotificationError("/errorinaccessiblefiles/notification", nbrMarkedFileObjects.ToString());

            synchronizationParameters.fileObjectsPerToken.Clear();
        }

        /// <summary>
        /// Downloads users from Phabricator
        /// </summary>
        /// <param name="synchronizationParameters"></param>
        /// <param name="processedDuration"></param>
        /// <param name="totalDuration"></param>
        public void ProgressMethod_DownloadUsers(SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration)
        {
            Phabricator.API.User phabricatorUserAPI = new Phabricator.API.User();
            Storage.User userStorage = new Storage.User();

            // check if untagged-dummy-project exists
            synchronizationParameters.UserNoneInitialized = false;
            if (userStorage.Get(synchronizationParameters.database, Phabricator.Data.User.None, Language.NotApplicable) == null)
            {
                // create dummy project for untagged Phriction documents or Maniphest tasks
                Phabricator.Data.User dummyUser = new Phabricator.Data.User();
                dummyUser.RealName = "(Unassigned)";
                dummyUser.UserName = "(Unassigned)";
                dummyUser.Selected = false;
                dummyUser.Token = Phabricator.Data.User.None;
                userStorage.Add(synchronizationParameters.database, dummyUser);

                synchronizationParameters.UserNoneInitialized = true;
            }

            // load all users from phabricator
            string messageDownloadingUserData = Miscellaneous.Locale.TranslateText("Synchronization.Status.DownloadingUsers", browser.Session.Locale);
            SharedResource.Instance.ProgressDescription = messageDownloadingUserData;
            List<Phabricator.Data.User> phabricatorUsers = phabricatorUserAPI.GetAll(synchronizationParameters.database,
                                                                                     synchronizationParameters.browser.Conduit,
                                                                                     synchronizationParameters.lastSynchronizationTimestamp
                                                                                    )
                                                                             .ToList();
            int index = 0;
            int count = phabricatorUsers.Count();
            double stepsize = (synchronizationParameters.stepSize * 100.00) / ((double)count * (double)totalDuration);
            foreach (Phabricator.Data.User phabricatorUser in phabricatorUsers)
            {
                SharedResource.Instance.ProgressDescription = string.Format("{0} [{1}/{2}]", messageDownloadingUserData, index++, count);
                SharedResource.Instance.ProgressPercentage += stepsize;

                // sleep a bit, so the progress-bar is shown a little more animated...
                if ((index % 100) == 0) Thread.Sleep(100);

                bool selected;
                if (synchronizationParameters.userSelected.TryGetValue(phabricatorUser.Token, out selected) == false)
                {
                    selected = false;
                }

                phabricatorUser.Selected = selected;

                // get DateSynchronized
                Phabricator.Data.User localUser = userStorage.Get(synchronizationParameters.database, phabricatorUser.Token, synchronizationParameters.browser.Session.Locale);
                if (localUser != null)
                {
                    phabricatorUser.DateSynchronized = localUser.DateSynchronized;
                }

                userStorage.Add(synchronizationParameters.database, phabricatorUser);
            }
        }

        /// <summary>
        /// This method is fired at the end of the synchronization process
        /// </summary>
        /// <param name="synchronizationParameters"></param>
        /// <param name="processedDuration"></param>
        /// <param name="totalDuration"></param>
        public void ProgressMethod_Finalize(SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration)
        {
            // update review states of all translations
            Storage.Content.SynchronizeReviewStatesWithMasterObjects(synchronizationParameters.database);

            // update FilesPerMacroName dictionary
            Http.Server.FilesPerMacroName[""] = null;
            synchronizationParameters.browser.HttpServer.PreloadFileMacros(synchronizationParameters.database.EncryptionKey);

            if (synchronizationParameters.remotelyModifiedObjects.Any())
            {
                SharedResource.Instance.ProgressState = "MERGE-CONFLICT";
            }
        }

        /// <summary>
        /// Determines the last time Phabrico was successfully synchronized with Phabricator.
        /// This timestamp is stored in the shared parameters
        /// </summary>
        /// <param name="synchronizationParameters">Shared parameters through the whole synchronization process</param>
        /// <param name="processedDuration"></param>
        /// <param name="totalDuration"></param>
        public void ProgressMethod_LoadLastSynchronizeTimeStamp(SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration)
        {
            DateTimeOffset minimumTimeStamp = DateTimeOffset.MaxValue;

            if (synchronizationParameters.existingAccount.Parameters.Synchronization.HasFlag(Phabricator.Data.Account.SynchronizationMethod.PerProjects))
            {
                Storage.Project projectStorage = new Storage.Project();

                DateTimeOffset minimumProjectTimeStamp = synchronizationParameters.projectSelected
                                                                                  .DefaultIfEmpty()
                                                                                  .Where(project => project.Value == Phabricator.Data.Project.Selection.Selected)
                                                                                  .Select(project => projectStorage.Get(synchronizationParameters.database, project.Key, synchronizationParameters.browser.Session.Locale))
                                                                                  .DefaultIfEmpty()
                                                                                  .Min(project => project == null
                                                                                                ? DateTimeOffset.MinValue
                                                                                                : project.DateSynchronized
                                                                                      );
                if (minimumTimeStamp > minimumProjectTimeStamp)
                {
                    minimumTimeStamp = minimumProjectTimeStamp;
                }
            }

            if (synchronizationParameters.existingAccount.Parameters.Synchronization.HasFlag(Phabricator.Data.Account.SynchronizationMethod.PerUsers))
            {
                Storage.User userStorage = new Storage.User();

                DateTimeOffset minimumUserTimeStamp = synchronizationParameters.userSelected
                                                                               .DefaultIfEmpty()
                                                                               .Where(user => user.Value == true)
                                                                               .Select(user => userStorage.Get(synchronizationParameters.database, user.Key, synchronizationParameters.browser.Session.Locale))
                                                                               .DefaultIfEmpty()
                                                                               .Min(user => user == null
                                                                                         || user.DateSynchronized == DateTimeOffset.MinValue
                                                                                          ? DateTimeOffset.MaxValue
                                                                                          : user.DateSynchronized
                                                                                   );
                if (minimumTimeStamp > minimumUserTimeStamp)
                {
                    minimumTimeStamp = minimumUserTimeStamp;
                }
            }

            if (minimumTimeStamp.Year <= 1970 || minimumTimeStamp == DateTimeOffset.MaxValue)
            {
                // no filtering on timestamp found => download everything from in the beginning
                minimumTimeStamp = DateTimeOffset.FromUnixTimeSeconds(1);
                synchronizationParameters.lastDownloadTimestamp = minimumTimeStamp.ToUniversalTime();
            }
            else
            {
                synchronizationParameters.lastDownloadTimestamp = minimumTimeStamp.Add(synchronizationParameters.TimeDifferenceBetweenPhabricatorAndLocalComputer) .ToUniversalTime();
            }

            synchronizationParameters.lastSynchronizationTimestamp = minimumTimeStamp.ToUniversalTime();
        }

        /// <summary>
        /// This method is fired near the end of the synchronization process.
        /// It will store the current timestamp as 'Last synchronization time'.
        /// The next sync will start downloading all items from Phabricator which have been modified/created
        /// since this timestamp
        /// </summary>
        /// <param name="synchronizationParameters"></param>
        /// <param name="processedDuration"></param>
        /// <param name="totalDuration"></param>
        public void ProgressMethod_SaveLastSynchronizeTimeStamp(SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration)
        {
            Storage.Maniphest maniphestStorage = new Storage.Maniphest();
            Storage.Phriction phrictionStorage = new Storage.Phriction();
            Storage.Project projectStorage = new Storage.Project();
            Storage.User userStorage = new Storage.User();

            // determine new SynchronizeTimeStamp based on the last modified maniphest task or phriction document
            DateTimeOffset maxManiphestDateModified = maniphestStorage.Get(synchronizationParameters.database, synchronizationParameters.browser.Session.Locale).Select(record => record.DateModified).DefaultIfEmpty().Max();
            DateTimeOffset maxPhrictionDateModified = phrictionStorage.Get(synchronizationParameters.database, synchronizationParameters.browser.Session.Locale).Select(record => record.DateModified).DefaultIfEmpty().Max();
            DateTimeOffset newSynchronizationTimeStamp = maxManiphestDateModified > maxPhrictionDateModified
                                                       ? maxManiphestDateModified
                                                       : maxPhrictionDateModified;
            newSynchronizationTimeStamp = newSynchronizationTimeStamp > synchronizationParameters.lastRemovalTimestamp
                                        ? newSynchronizationTimeStamp
                                        : synchronizationParameters.lastRemovalTimestamp;
            newSynchronizationTimeStamp = newSynchronizationTimeStamp.AddSeconds(1);

            foreach (string selectedProjectToken in synchronizationParameters.projectSelected
                                                                             .Where(project => project.Value == Phabricator.Data.Project.Selection.Selected)
                                                                             .Select(project => project.Key))
            {
                Phabricator.Data.Project project = projectStorage.Get(synchronizationParameters.database, selectedProjectToken, Language.NotApplicable);
                if (project != null)
                {
                    project.DateSynchronized = newSynchronizationTimeStamp;
                    projectStorage.Add(synchronizationParameters.database, project);
                }
            }

            foreach (string selectedUserToken in synchronizationParameters.userSelected
                                                                          .Where(user => user.Value == true)
                                                                          .Select(user => user.Key))
            {
                Phabricator.Data.User user = userStorage.Get(synchronizationParameters.database, selectedUserToken, Language.NotApplicable);
                if (user != null)
                {
                    user.DateSynchronized = newSynchronizationTimeStamp;
                    userStorage.Add(synchronizationParameters.database, user);
                }
            }

            synchronizationParameters.lastSynchronizationTimestamp = newSynchronizationTimeStamp;
        }

        /// <summary>
        /// This method is fired when all new/modified phriction documents and maniphest tasks are downloaded from Phabricator.
        /// This method will be executed twice:
        /// 1. Downloading new/modified phriction documents and maniphest tasks
        /// 2. After uploading staged phriction documents and maniphest tasks, a new download is started (so the staged tokens will be converted
        ///    into real Phabricator tokens)
        /// </summary>
        /// <param name="synchronizationParameters"></param>
        /// <param name="processedDuration"></param>
        /// <param name="totalDuration"></param>
        public void ProgressMethod_SaveLastDownloadTimeStamp(SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration)
        {
            Storage.Maniphest maniphestStorage = new Storage.Maniphest();
            Storage.Phriction phrictionStorage = new Storage.Phriction();
            Storage.Project projectStorage = new Storage.Project();
            Storage.User userStorage = new Storage.User();

            // determine new DownloadTimeStamp based on the last modified maniphest task or phriction document
            DateTimeOffset maxManiphestDateModified = maniphestStorage.Get(synchronizationParameters.database, synchronizationParameters.browser.Session.Locale).Select(record => record.DateModified).DefaultIfEmpty().Max();
            DateTimeOffset maxPhrictionDateModified = phrictionStorage.Get(synchronizationParameters.database, synchronizationParameters.browser.Session.Locale).Select(record => record.DateModified).DefaultIfEmpty().Max();
            DateTimeOffset newDownloadTimeStamp = maxManiphestDateModified > maxPhrictionDateModified ? maxManiphestDateModified : maxPhrictionDateModified;
            newDownloadTimeStamp = newDownloadTimeStamp.AddSeconds(1);
            newDownloadTimeStamp = newDownloadTimeStamp.Add(synchronizationParameters.TimeDifferenceBetweenPhabricatorAndLocalComputer);

            foreach (string selectedProjectToken in synchronizationParameters.projectSelected
                                                                             .Where(project => project.Value == Phabricator.Data.Project.Selection.Selected)
                                                                             .Select(project => project.Key))
            {
                Phabricator.Data.Project project = projectStorage.Get(synchronizationParameters.database, selectedProjectToken, synchronizationParameters.browser.Session.Locale);
                if (project != null)
                {
                    project.DateSynchronized = newDownloadTimeStamp;
                    projectStorage.Add(synchronizationParameters.database, project);
                }
            }

            foreach (string selectedUserToken in synchronizationParameters.userSelected
                                                                          .Where(user => user.Value == true)
                                                                          .Select(user => user.Key))
            {
                Phabricator.Data.User user = userStorage.Get(synchronizationParameters.database, selectedUserToken, Language.NotApplicable);
                if (user != null)
                {
                    user.DateSynchronized = newDownloadTimeStamp;
                    userStorage.Add(synchronizationParameters.database, user);
                }
            }

            if (synchronizationParameters.lastDownloadTimestamp < newDownloadTimeStamp)
            {
                synchronizationParameters.lastDownloadTimestamp = newDownloadTimeStamp;
            }
        }

        /// <summary>
        /// Uploads Maniphest tasks to Phabricator
        /// </summary>
        /// <param name="synchronizationParameters"></param>
        /// <param name="processedDuration"></param>
        /// <param name="totalDuration"></param>
        public void ProgressMethod_UploadManiphestTasks(SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration)
        {
            Storage.Stage stageStorage = new Storage.Stage();
            Storage.Maniphest maniphestStorage = new Storage.Maniphest();
            Phabricator.API.Maniphest phabricatorManiphestAPI = new Phabricator.API.Maniphest();

            // uploading maniphest tasks
            string messageUploadingManiphestData = Miscellaneous.Locale.TranslateText("Synchronization.Status.UploadingManiphestData", browser.Session.Locale);
            SharedResource.Instance.ProgressDescription = messageUploadingManiphestData;
            IEnumerable<Phabricator.Data.Maniphest> maniphestTasksLocallyModified = stageStorage.Get<Phabricator.Data.Maniphest>(synchronizationParameters.database, Language.NotApplicable, true);
            IEnumerable<Phabricator.Data.Maniphest> maniphestTasksRemotelyModified = maniphestTasksLocallyModified.Where(locallyModifiedTask => {
                Phabricator.Data.Maniphest remotelyModifiedTask = maniphestStorage.Get(synchronizationParameters.database, locallyModifiedTask.Token, synchronizationParameters.browser.Session.Locale);
                if (remotelyModifiedTask == null || remotelyModifiedTask.Token.StartsWith("PHID-NEWTOKEN-")) return false;  // new document

                return remotelyModifiedTask.DateModified > synchronizationParameters.lastSynchronizationTimestamp;
            });

            int index = 0;
            int count = maniphestTasksLocallyModified.Count() - maniphestTasksRemotelyModified.Count();
            double stepsize = (synchronizationParameters.stepSize * 100.00) / ((double)count * (double)totalDuration);
            List<Phabricator.Data.Maniphest> processedManiphestTasks = new List<Phabricator.Data.Maniphest>();
            foreach (var modifiedManiphestTask in maniphestTasksLocallyModified.Where(task => maniphestTasksRemotelyModified.Any(modification => modification.Token.Equals(task.Token)) == false))
            {
                SharedResource.Instance.ProgressDescription = string.Format("{0} [{1}/{2}]", messageUploadingManiphestData, index++, count);
                SharedResource.Instance.ProgressPercentage += stepsize;

                // sleep a bit, so the progress-bar is shown a little more animated...
                if ((index % 100) == 0) Thread.Sleep(100);

                UploadOfflineAttachments(synchronizationParameters, modifiedManiphestTask);

                phabricatorManiphestAPI.Edit(synchronizationParameters.browser, synchronizationParameters.database, synchronizationParameters.browser.Conduit, modifiedManiphestTask);

                processedManiphestTasks.Add(modifiedManiphestTask);
            }

            foreach (Phabricator.Data.Maniphest processedManiphestTask in processedManiphestTasks)
            {
                // delete the local version
                stageStorage.Remove(synchronizationParameters.database, synchronizationParameters.browser, processedManiphestTask);
            }

            synchronizationParameters.remotelyModifiedObjects.AddRange(maniphestTasksRemotelyModified);
            synchronizationParameters.stagedDataHasBeenUploaded = true;

            SharedResource.Instance.ProgressPercentage = (int)(((double)processedDuration * 100) / (double)totalDuration) - 1;
        }

        /// <summary>
        /// Uploads Phriction documents to Phabricator
        /// </summary>
        /// <param name="synchronizationParameters"></param>
        /// <param name="processedDuration"></param>
        /// <param name="totalDuration"></param>
        public void ProgressMethod_UploadPhrictionDocuments(SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration)
        {
            Storage.Stage stageStorage = new Storage.Stage();
            Storage.Phriction phrictionStorage = new Storage.Phriction();
            Phabricator.API.Phriction phabricatorPhrictionAPI = new Phabricator.API.Phriction();

            string messageUploadingPhrictionData = Miscellaneous.Locale.TranslateText("Synchronization.Status.UploadingPhrictionData", browser.Session.Locale);
            SharedResource.Instance.ProgressDescription = messageUploadingPhrictionData;

            // verify that we don't overwrite remotely changed documents
            IEnumerable<Phabricator.Data.Phriction> phrictionDocumentsLocallyModified = stageStorage.Get<Phabricator.Data.Phriction>(synchronizationParameters.database, Language.NotApplicable, true);
            IEnumerable<Phabricator.Data.Phriction> phrictionDocumentsRemotelyModified = phrictionDocumentsLocallyModified.Where(locallyModifiedDocument => {
                Phabricator.Data.Phriction remotelyModifiedDocument = phrictionStorage.Get(synchronizationParameters.database, locallyModifiedDocument.Token, Language.NotApplicable, true);
                if (remotelyModifiedDocument == null || remotelyModifiedDocument.Token.StartsWith("PHID-NEWTOKEN-")) return false;  // new document

                return remotelyModifiedDocument.DateModified > synchronizationParameters.lastSynchronizationTimestamp;
            });

            // uploading phriction documents
            int index = 0;
            int count = phrictionDocumentsLocallyModified.Count() - phrictionDocumentsRemotelyModified.Count();
            double stepsize = (synchronizationParameters.stepSize * 100.00) / ((double)count * (double)totalDuration);
            List<Phabricator.Data.Phriction> processedPhrictionDocuments = new List<Phabricator.Data.Phriction>();
            foreach (var modifiedPhrictionDocument in phrictionDocumentsLocallyModified.Where(doc => phrictionDocumentsRemotelyModified.Any(modification => modification.Token.Equals(doc.Token)) == false)
                                                                                       .OrderBy(doc => doc.Path.Length)
                                                                                       .Where(doc => doc.Path.Length < 115   // Phabricator will not accept slugs longer than 115 characters
                                                                                                  || doc.Token.StartsWith("PHID-NEWTOKEN-") == false   // document was already stored in Phabricator
                                                                                             )
                    )
            {
                SharedResource.Instance.ProgressDescription = string.Format("{0} [{1}/{2}]", messageUploadingPhrictionData, index++, count);
                SharedResource.Instance.ProgressPercentage += stepsize;

                // sleep a bit, so the progress-bar is shown a little more animated...
                if ((index % 100) == 0) Thread.Sleep(100);

                // make sure that there is content -> if not create a 1-space content
                if (string.IsNullOrEmpty(modifiedPhrictionDocument.Content)) modifiedPhrictionDocument.Content = " ";

                UploadOfflineAttachments(synchronizationParameters, modifiedPhrictionDocument);

                phabricatorPhrictionAPI.Edit(synchronizationParameters.browser, synchronizationParameters.database, synchronizationParameters.browser.Conduit, modifiedPhrictionDocument);

                processedPhrictionDocuments.Add(modifiedPhrictionDocument);
            }

            foreach (Phabricator.Data.Phriction processedPhrictionDocument in processedPhrictionDocuments)
            {
                // delete the local version
                stageStorage.Remove(synchronizationParameters.database, synchronizationParameters.browser, processedPhrictionDocument);
            }

            synchronizationParameters.remotelyModifiedObjects.AddRange(phrictionDocumentsRemotelyModified);

            SharedResource.Instance.ProgressPercentage = (int)(((double)processedDuration * 100) / (double)totalDuration) - 1;
        }

        /// <summary>
        /// Uploads metadata for Maniphest Tasks (e.g. comments, state changes, ...)
        /// </summary>
        /// <param name="synchronizationParameters"></param>
        /// <param name="processedDuration"></param>
        /// <param name="totalDuration"></param>
        public void ProgressMethod_UploadTransactions(SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration)
        {
            Storage.Stage stageStorage = new Storage.Stage();
            Phabricator.API.Maniphest phabricatorManiphestAPI = new Phabricator.API.Maniphest();
            phabricatorManiphestAPI.TimeDifferenceBetweenPhabricatorAndLocalComputer = synchronizationParameters.TimeDifferenceBetweenPhabricatorAndLocalComputer;

            // uploading maniphest meta data
            string messageUploadingCommentsAndTransactions = Miscellaneous.Locale.TranslateText("Synchronization.Status.UploadingCommentsAndTransactions", browser.Session.Locale);
            SharedResource.Instance.ProgressDescription = messageUploadingCommentsAndTransactions;
            IEnumerable<Phabricator.Data.Transaction> modifiedTransactions = stageStorage.Get<Phabricator.Data.Transaction>(synchronizationParameters.database, Language.NotApplicable, true);
            int index = 0;
            int count = modifiedTransactions.Count();
            double stepsize = (synchronizationParameters.stepSize * 100.00) / ((double)count * (double)totalDuration);
            foreach (var modifiedTransactionsPerObject in modifiedTransactions.GroupBy(key => key.Token))
            {
                SharedResource.Instance.ProgressDescription = string.Format("{0} [{1}/{2}]", messageUploadingCommentsAndTransactions, index++, count);
                SharedResource.Instance.ProgressPercentage += stepsize;

                // sleep a bit, so the progress-bar is shown a little more animated...
                if ((index % 100) == 0) Thread.Sleep(100);

                if (modifiedTransactionsPerObject.Key.StartsWith(Phabricator.Data.Maniphest.Prefix))
                {
                    foreach (Phabricator.Data.Transaction transaction in modifiedTransactionsPerObject)
                    {
                        UploadOfflineAttachments(synchronizationParameters, transaction);
                    }
                    phabricatorManiphestAPI.Edit(synchronizationParameters.browser, synchronizationParameters.database, synchronizationParameters.browser.Conduit, modifiedTransactionsPerObject);
                }

                // delete the local versions
                foreach (var modifiedTransaction in modifiedTransactionsPerObject.Reverse())
                {
                    stageStorage.Remove(synchronizationParameters.database, synchronizationParameters.browser, modifiedTransaction);
                }
            }
            SharedResource.Instance.ProgressPercentage = (processedDuration * 100.0) / totalDuration;
        }

        /// <summary>
        /// Determines which Phabricator account is used for downloading and uploading
        /// </summary>
        /// <param name="synchronizationParameters"></param>
        /// <param name="processedDuration"></param>
        /// <param name="totalDuration"></param>
        public void ProgressMethod_WhoAmI(SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration)
        {
            Phabricator.API.User phabricatorUserAPI = new Phabricator.API.User();
            Storage.Account accountStorage = new Storage.Account();

            SharedResource.Instance.ProgressDescription = Miscellaneous.Locale.TranslateText("Synchronization.Status.LoadingAccountInfo", browser.Session.Locale);
            Phabricator.Data.User phabricatorAccount = phabricatorUserAPI.WhoAmI(synchronizationParameters.database, synchronizationParameters.browser.Conduit);

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException("/synchronization", "session expired");
            if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/synchronize", "You don't have sufficient rights to synchronize Phabrico with Phabricator");

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // set private encryption key
                database.PrivateEncryptionKey = token.PrivateEncryptionKey;

                Phabricator.Data.Account existingAccount = accountStorage.Get(database, token);
                if (existingAccount != null)
                {
                    existingAccount.Parameters.UserToken = phabricatorAccount.Token;
                    accountStorage.Set(database, existingAccount);

                    if (synchronizationParameters.UserNoneInitialized)
                    {
                        Storage.User userStorage = new Storage.User();

                        phabricatorAccount.Selected = true;
                        userStorage.Add(synchronizationParameters.database, phabricatorAccount);
                    }
                }
            }
        }

        /// <summary>
        /// Uploads files to Phabricator
        /// </summary>
        /// <param name="synchronizationParameters"></param>
        /// <param name="phabricatorObject"></param>
        private void UploadOfflineAttachments(SynchronizationParameters synchronizationParameters, Phabricator.Data.PhabricatorObject phabricatorObject)
        {
            string remarkupContent = null;

            // find out which kind of phabricatorObject we're processing
            Phabricator.Data.Phriction phrictionDocument = phabricatorObject as Phabricator.Data.Phriction;
            if (phrictionDocument != null)
            {
                remarkupContent = phrictionDocument.Content;
            }

            Phabricator.Data.Maniphest maniphestTask = phabricatorObject as Phabricator.Data.Maniphest;
            if (maniphestTask != null)
            {
                remarkupContent = maniphestTask.Description;
            }

            Phabricator.Data.Transaction transaction = phabricatorObject as Phabricator.Data.Transaction;
            if (transaction != null)
            {
                remarkupContent = transaction.NewValue;
            }

            // execute the actual processing
            if (remarkupContent != null)
            {
                Action<Phabricator.Data.PhabricatorObject> processFileReferencedInRemarkupContent = new Action<Phabricator.Data.PhabricatorObject>((dataObject) =>
                {
                    Storage.Stage stageStorage = new Storage.Stage();
                    Phabricator.API.File fileAPI = new Phabricator.API.File();
                    Dictionary<int, Phabricator.Data.File> cachedFileReferences = new Dictionary<int, Phabricator.Data.File>();
                    Regex matchFileAttachments = new Regex("{F(-[0-9]+)[^}]*}");
                    foreach (Match match in matchFileAttachments.Matches(remarkupContent).OfType<Match>().ToArray().OrderByDescending(match => match.Index))
                    {
                        int phabricatorFileReference;
                        int phabricoFileReference = Int32.Parse(match.Groups[1].Value);
                        if (synchronizationParameters.uploadedFileReferences.TryGetValue(phabricoFileReference, out phabricatorFileReference) == false)
                        {
                            Phabricator.Data.File file = null;
                            Phabricator.Data.File fileWithoutContent = null;
                            if (cachedFileReferences.TryGetValue(phabricoFileReference, out fileWithoutContent) == false)
                            {
                                fileWithoutContent = stageStorage.Get<Phabricator.Data.File>(synchronizationParameters.database, Language.NotApplicable)
                                                                 .FirstOrDefault(f => f.ID == phabricoFileReference);
                            }

                            if (fileWithoutContent != null)
                            {
                                if (cachedFileReferences.TryGetValue(phabricoFileReference, out file) == false)
                                {
                                    // get content of file
                                    file = stageStorage.Get<Phabricator.Data.File>(synchronizationParameters.database,
                                                                                   browser.Session.Locale,
                                                                                   Phabricator.Data.File.Prefix,
                                                                                   phabricoFileReference,
                                                                                   true
                                                                                  );

                                    // cache file in case we have multiple references to this file in this document
                                    cachedFileReferences[phabricoFileReference] = file;
                                }

                                int? newFileReference = fileAPI.Edit(synchronizationParameters.browser.Conduit, file);
                                if (newFileReference.HasValue == false)
                                {
                                    // some error occurred while uploading the file
                                    continue;
                                }

                                phabricatorFileReference = newFileReference.Value;
                                synchronizationParameters.uploadedFileReferences[phabricoFileReference] = phabricatorFileReference;

                                // remove file from offline stage area
                                stageStorage.Remove(synchronizationParameters.database, synchronizationParameters.browser, file);
                            }
                        }

                        remarkupContent = remarkupContent.Substring(0, match.Groups[1].Index)
                                        + phabricatorFileReference
                                        + remarkupContent.Substring(match.Groups[1].Index + match.Groups[1].Length);

                        // copy result into phabricatorObject
                        if (phrictionDocument != null)
                        {
                            phrictionDocument.Content = remarkupContent;
                        }

                        if (maniphestTask != null)
                        {
                            maniphestTask.Description = remarkupContent;
                        }

                        if (transaction != null)
                        {
                            transaction.NewValue = remarkupContent;
                        }
                    }
                });

                processFileReferencedInRemarkupContent.Invoke(phrictionDocument); // execute
            }
        }
    }
}
