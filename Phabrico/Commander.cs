using Newtonsoft.Json;
using Phabrico.Miscellaneous;
using Phabrico.Parsers.Remarkup.Rules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Phabrico
{
    /// <summary>
    /// Takes care of command line interfacing
    /// </summary>
    class Commander
    {
        /// <summary>
        /// Represents the contents of JSON config file
        /// </summary>
        public class CommanderConfiguration
        {
            public class Maniphest
            {
                public string[] projectTags { get; set; }
                public string[] userTags { get; set; }
                public bool showTasks { get; set; }
            }

            public class Phriction
            {
                public string[] projectTags { get; set; }
                public string[] userTags { get; set; }
                public bool combined { get; set; } = true;
                public string initialPath { get; set; } = "";
                public bool showDocuments { get; set; }
                public bool tree { get; set; } = true;
                public string translation { get; set; }
            }

            public class SecondaryUser
            {
                public string name { get; set; }
                public string password { get; set; }
                public string[] tags { get; set; }
            }

            public string source { get; set; }
            public string destination { get; set; }
            public string username { get; set; }
            public string password { get; set; }
            public Maniphest maniphest { get; set; }
            public Phriction phriction { get; set; }
            public SecondaryUser[] users { get; set; }
        }

        /// <summary>
        /// Actions which the Commander can execute
        /// </summary>
        public enum CommanderAction
        {
            Download,
            Strip,
            Nothing
        }

        /// <summary>
        /// Is used by the ConsoleWriteStatus to clear the previous message from the console before writing the new one
        /// </summary>
        private string lastConsoleStatusMessage = "";

        /// <summary>
        /// List of messages which should be shown at the end of the Commander execution
        /// </summary>
        private readonly List<string> informationalMessages = new List<string>();

        /// <summary>
        /// Action to be executed from the command prompt
        /// </summary>
        public CommanderAction Action { get; private set; } = CommanderAction.Nothing;

        /// <summary>
        /// JSON configuration for the Commander
        /// </summary>
        public CommanderConfiguration Configuration { get; private set; }

        /// <summary>
        /// Conduit API token to be used for communication with Phabricator
        /// </summary>
        public string ConduitAPIToken { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="program"></param>
        /// <param name="arguments"></param>
        public Commander(Program program, string[] arguments)
        {
            string argument = null;

            try
            {
                for (int arg = 0; arg < arguments.Length; arg++)
                {
                    argument = arguments[arg].ToLower();
                    switch (argument)
                    {
                        case "/download":
                        case "/strip":
                            if (arg + 1 >= arguments.Length) throw new ArgumentException("Path to JSON file missing");
                            arg++;

                            if (System.IO.File.Exists(arguments[arg]) == false) throw new ArgumentException("JSON file not found");

                            try
                            {
                                string jsonData = System.IO.File.ReadAllText(arguments[arg]);
                                Configuration = JsonConvert.DeserializeObject<CommanderConfiguration>(jsonData);
                            }
                            catch (System.Exception exception)
                            {
                                throw new ArgumentException("Invalid JSON content in " + arguments[arg] + ": " + exception.Message);
                            }

                            if (Configuration.source == null || RegexSafe.IsMatch(Configuration.source, "^http?://", RegexOptions.None) == false) throw new ArgumentException("source is invalid or missing in JSON file");
                            if (Configuration.destination == null || FilePathIsValid(Configuration.destination) == false) throw new ArgumentException("destination is invalid or missing in JSON file");
                            if (Configuration.username == null) throw new ArgumentException("username is missing in JSON file");
                            if (Configuration.password == null) throw new ArgumentException("password is missing in JSON file");
                            if (Configuration.password.Length < 12 ||
                                RegexSafe.Matches(Configuration.password, "[A-Z]").Count == 0 ||
                                RegexSafe.Matches(Configuration.password, "[a-z]").Count == 0 ||
                                RegexSafe.Matches(Configuration.password, "[0-9]").Count == 0 ||
                                RegexSafe.Matches(Configuration.password, "[!\"#$%&'()*+,-./:;<=>?@\\\\\\[\\]^_`{|}~]").Count == 0
                               )
                            {
                                throw new ArgumentException("password should be at least 12 characters and contain at least 1 uppercase, 1 lowercase, 1 number and 1 punctuation character");
                            }

                            if (Configuration.phriction != null && string.IsNullOrWhiteSpace(Configuration.phriction.translation) == false)
                            {
                                Configuration.phriction.translation = Configuration.phriction.translation.Replace('/', '\\');
                                if (System.IO.File.Exists(Configuration.phriction.translation) == false)
                                {
                                    throw new ArgumentException(string.Format("ERROR: translation file \"{0}\" not found", Configuration.phriction.translation));
                                }
                            }

                            if (argument.Equals("/download"))
                                Action = CommanderAction.Download;
                            else if (argument.Equals("/strip"))
                                Action = CommanderAction.Strip;
                            break;

                        case "/?":
                            ShowUsage();
                            break;

                        default:
                            if (argument.StartsWith("/token:"))
                            {
                                string token = arguments[arg].Substring("/token:".Length);
                                if (RegexSafe.IsMatch(token, "api-[a-zA-Z0-9]{28}", RegexOptions.None) == false) throw new ArgumentException("Invalid Conduit token");

                                ConduitAPIToken = token;
                                break;
                            }
                            throw new ArgumentException("Invalid argument");
                    }
                }
            }
            catch (System.Exception exception)
            {
                Console.WriteLine("{0}: {1}", exception.Message, argument);
                Action = CommanderAction.Nothing;

                Console.WriteLine();
                ShowUsage();
            }
        }

        /// <summary>
        /// Reads a string inputed by the user.
        /// The input data is not visible
        /// </summary>
        /// <param name="message">Text to be shown in front</param>
        /// <returns>Input data</returns>
        public string ConsoleReadString(string message = "")
        {
            string value = "";

            ConsoleWriteStatus(message);
            while (true)
            {
                ConsoleKeyInfo k = Console.ReadKey(true);

                if (k.Key == ConsoleKey.Enter)
                {
                    Console.Write("\r");
                    Console.Write(new string(' ', message.Length + value.Length));
                    Console.Write("\r");
                    break;
                }

                if (k.KeyChar == '\0') continue;

                if (k.Key == ConsoleKey.Backspace)
                {
                    if (value.Length > 0)
                    {
                        value = value.Substring(0, value.Length - 1);
                        Console.Write("\b \b");
                    }
                }
                else
                {
                    value += k.KeyChar;
                    Console.Write("*");
                }
            }

            return value;
        }

        /// <summary>
        /// Writes a message to the command prompt on the same line as the previous message
        /// </summary>
        /// <param name="message"></param>
        public void ConsoleWriteStatus(string message)
        {
            Console.Write("\r");
            Console.Write(new string(' ', lastConsoleStatusMessage.Length));
            Console.Write("\r" + message);

            lastConsoleStatusMessage = message;
        }

        /// <summary>
        /// The method is executed as soon as an invalid URL is found in Phriction documents / Maniphest tasks
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="origin"></param>
        /// <param name="invalidUrl"></param>
        private void Database_InvalidUrlFound(object sender, string origin, string invalidUrl)
        {
            informationalMessages.Add(string.Format("WARNING: Invalid URL \"{0}\" found in \"{1}\"", invalidUrl, origin));
        }

        /// <summary>
        /// Executesthe Commander
        /// </summary>
        public void Execute()
        {
            if (Configuration != null)
            {
                if (Configuration.destination != null)
                {
                    // correct file path
                    Configuration.destination = Configuration.destination.Replace("/", "\\");
                }

                if (Configuration.users == null)
                {
                    Configuration.users = new CommanderConfiguration.SecondaryUser[0];
                }
            }

            switch (Action)
            {
                case CommanderAction.Download:
                    ExecuteDownload();
                    break;

                case CommanderAction.Strip:
                    ExecuteStrip();
                    break;

                default:
                    throw new NotImplementedException();
            }

            foreach (string informationalMessage in informationalMessages)
            {
                Console.WriteLine(informationalMessage);
            }
        }

        /// <summary>
        /// Executes the download action
        /// </summary>
        private void ExecuteDownload()
        {
            Logging.SetLoggingEventHandler(LoggingEvent);

            if (System.IO.File.Exists(Configuration.destination))
            {
                System.IO.File.Delete(Configuration.destination);
            }

            Storage.Database.DataSource = Configuration.destination;

            if (string.IsNullOrWhiteSpace(Configuration.phriction.translation) == false)
            {
                System.IO.File.Copy(Configuration.phriction.translation, System.IO.Path.GetDirectoryName(Configuration.destination) + "\\Phabrico.translation", true);
            }

            using (Storage.Database database = new Storage.Database(null))
            {
                Storage.Account accountStorage = new Storage.Account();
                Storage.Maniphest maniphestStorage = new Storage.Maniphest();
                Storage.Phriction phrictionStorage = new Storage.Phriction();
                Storage.Project projectStorage = new Storage.Project();
                Storage.User userStorage = new Storage.User();
                Http.Server httpServer = new Http.Server(false, -1, "/", true);
                Miscellaneous.HttpListenerContext httpListenerContext = new Miscellaneous.HttpListenerContext();
                Controllers.Synchronization synchronizationController = new Controllers.Synchronization();

                synchronizationController.browser = new Http.Browser(httpServer, httpListenerContext);
                synchronizationController.EncryptionKey = Encryption.GenerateEncryptionKey(Configuration.username, Configuration.password);

                synchronizationController.TokenId = httpServer.Session.ClientSessions.LastOrDefault().Key;

                Controllers.Synchronization.SynchronizationParameters synchronizationParameters = new Controllers.Synchronization.SynchronizationParameters();
                synchronizationParameters.database = database;
                synchronizationParameters.browser = synchronizationController.browser;
                synchronizationParameters.previousSyncMode = Controllers.Synchronization.SyncMode.Full;
                synchronizationParameters.existingAccount = new Phabricator.Data.Account();
                synchronizationParameters.existingAccount.Token = Encryption.GenerateTokenKey(Configuration.username, Configuration.password);
                synchronizationParameters.existingAccount.UserName = Configuration.username;
                synchronizationParameters.existingAccount.Parameters.Synchronization = (Configuration.maniphest != null && Configuration.maniphest.projectTags != null && Configuration.maniphest.projectTags.Any()) ? Phabricator.Data.Account.SynchronizationMethod.ManiphestSelectedProjectsOnly
                                                                                     : (Configuration.maniphest != null && Configuration.maniphest.userTags != null && Configuration.maniphest.userTags.Any()) ? Phabricator.Data.Account.SynchronizationMethod.ManiphestSelectedUsersOnly
                                                                                     : Phabricator.Data.Account.SynchronizationMethod.None;
                synchronizationParameters.existingAccount.Parameters.Synchronization |= (Configuration.phriction != null && Configuration.phriction.combined && Configuration.phriction.projectTags != null && Configuration.phriction.projectTags.Any()) ? Phabricator.Data.Account.SynchronizationMethod.PhrictionAllSelectedProjectsOnly
                                                                                      : (Configuration.phriction != null && Configuration.phriction.combined == false && Configuration.phriction.projectTags != null && Configuration.phriction.projectTags.Any()) ? Phabricator.Data.Account.SynchronizationMethod.PhrictionSelectedProjectsOnly
                                                                                      : (Configuration.phriction != null && Configuration.phriction.userTags != null && Configuration.phriction.userTags.Any()) ? Phabricator.Data.Account.SynchronizationMethod.PhrictionSelectedUsersOnly
                                                                                      : Phabricator.Data.Account.SynchronizationMethod.None;
                synchronizationParameters.existingAccount.Parameters.Synchronization |= (Configuration.phriction != null && Configuration.phriction.tree && Configuration.phriction.combined) ? Phabricator.Data.Account.SynchronizationMethod.PhrictionAllSelectedProjectsOnlyIncludingDocumentTree
                                                                                      : (Configuration.phriction != null && Configuration.phriction.tree && Configuration.phriction.combined == false) ? Phabricator.Data.Account.SynchronizationMethod.PhrictionSelectedProjectsOnlyIncludingDocumentTree
                                                                                      : Phabricator.Data.Account.SynchronizationMethod.None;
                synchronizationParameters.existingAccount.Parameters.ColumnHeadersToHide = new string[0];
                synchronizationParameters.existingAccount.PhabricatorUrl = Configuration.source;

                synchronizationParameters.incrementalDownload = false;
                synchronizationParameters.lastDownloadTimestamp = new DateTimeOffset(1970, 1, 1, 0, 0, 1, new TimeSpan());
                synchronizationParameters.remotelyModifiedObjects = new List<Phabricator.Data.PhabricatorObject>();

                synchronizationController.browser.Conduit = new Phabricator.API.Conduit(httpServer, synchronizationParameters.browser);
                synchronizationController.browser.Conduit.PhabricatorUrl = Configuration.source;
                synchronizationController.browser.Conduit.Token = ConduitAPIToken;

                database.EncryptionKey = synchronizationController.EncryptionKey;
                database.PrivateEncryptionKey = Encryption.GeneratePrivateEncryptionKey(Configuration.username, Configuration.password);

                Http.SessionManager.Token token = synchronizationController.browser.HttpServer.Session.CreateToken(synchronizationParameters.existingAccount.Token, synchronizationController.browser);
                synchronizationController.browser.SetCookie("token", token.ID, true);
                token.EncryptionKey = Encryption.XorString(synchronizationParameters.database.EncryptionKey, synchronizationParameters.existingAccount.PublicXorCipher);
                token.PrivateEncryptionKey = Encryption.XorString(synchronizationParameters.database.PrivateEncryptionKey, synchronizationParameters.existingAccount.PrivateXorCipher);
                synchronizationController.TokenId = token.ID;
                synchronizationController.browser.ResetToken(token);

                database.InvalidUrlFound += Database_InvalidUrlFound;

                accountStorage.Add(database, synchronizationParameters.existingAccount);

                // start download process - phase 1
                Console.WriteLine();
                ConsoleWriteStatus("Connecting...");
                synchronizationController.ProgressMethod_Connecting(synchronizationParameters, 0, 0);
                ConsoleWriteStatus("Downloading projects...");
                synchronizationController.ProgressMethod_DownloadProjects(synchronizationParameters, 0, 0);
                ConsoleWriteStatus("Downloading users...");
                synchronizationController.ProgressMethod_DownloadUsers(synchronizationParameters, 0, 0);

                // start create secondary users
                foreach (CommanderConfiguration.SecondaryUser secondaryUser in Configuration.users)
                {
                    string newTokenHash = Encryption.GenerateTokenKey(secondaryUser.name, secondaryUser.password ?? "");
                    string newPublicEncryptionKey = Encryption.GenerateEncryptionKey(secondaryUser.name, secondaryUser.password ?? "");

                    Phabricator.Data.Account newUserAccount = new Phabricator.Data.Account();
                    newUserAccount.UserName = secondaryUser.name;
                    newUserAccount.Token = newTokenHash;
                    newUserAccount.PublicXorCipher = Encryption.GetXorValue(database.EncryptionKey, newPublicEncryptionKey);
                    newUserAccount.PrivateXorCipher = null;
                    newUserAccount.DpapiXorCipher1 = null;
                    newUserAccount.DpapiXorCipher2 = null;
                    newUserAccount.ConduitAPIToken = "";
                    newUserAccount.PhabricatorUrl = "";
                    newUserAccount.Theme = "light";
                    newUserAccount.Parameters = new Phabricator.Data.Account.Configuration();
                    newUserAccount.Parameters.AccountType = Phabricator.Data.Account.AccountTypes.SecondaryUser;
                    newUserAccount.Parameters.DefaultUserRoleTag = "";
                    if (secondaryUser.tags != null)
                    {
                        foreach (string tag in secondaryUser.tags)
                        {
                            Phabricator.Data.Project userRoleTag = projectStorage.Get(database, tag, Language.NotApplicable);
                            if (userRoleTag != null)
                            {
                                newUserAccount.Parameters.DefaultUserRoleTag += userRoleTag.Token + ",";
                            }
                        }

                        newUserAccount.Parameters.DefaultUserRoleTag = newUserAccount.Parameters.DefaultUserRoleTag.TrimEnd(',');
                    }
                    accountStorage.Add(database, newUserAccount);
                }

                // continue download process - phase 2
                synchronizationParameters.projectSelected[Phabricator.Data.Project.None] = Phabricator.Data.Project.Selection.Unselected;
                synchronizationParameters.userSelected[Phabricator.Data.User.None] = false;

                if (synchronizationParameters.existingAccount.Parameters.Synchronization.HasFlag(Phabricator.Data.Account.SynchronizationMethod.PhrictionSelectedProjectsOnly))
                {
                    // filter by projects

                    // unselect (None)
                    Phabricator.Data.Project project = projectStorage.Get(database, Phabricator.Data.Project.None, Language.NotApplicable);
                    project.Selected = Phabricator.Data.Project.Selection.Unselected;
                    projectStorage.Add(database, project);

                    string[] maniphestProjectTags = Configuration.maniphest?.projectTags ?? new string[0];
                    string[] phrictionProjectTags = Configuration.phriction?.projectTags ?? new string[0];

                    // select all projects from json config
                    foreach (string projectTag in maniphestProjectTags.Concat(phrictionProjectTags).Distinct())
                    {
                        project = projectStorage.Get(database, projectTag, Language.NotApplicable);
                        if (project != null)
                        {
                            project.Selected = Phabricator.Data.Project.Selection.Selected;
                            projectStorage.Add(database, project);

                            synchronizationParameters.projectSelected[project.Token] = Phabricator.Data.Project.Selection.Selected;
                        }
                    }
                }
                else
                {
                    // filter by users

                    // unselect (None)
                    Phabricator.Data.User user = userStorage.Get(database, Phabricator.Data.User.None, Language.NotApplicable);
                    user.Selected = false;
                    userStorage.Add(database, user);

                    string[] maniphestUserTags = Configuration.maniphest.userTags ?? new string[0];
                    string[] phrictionUserTags = Configuration.phriction.userTags ?? new string[0];

                    // select all users from json config
                    foreach (string userTag in maniphestUserTags.Concat(phrictionUserTags).Distinct())
                    {
                        user = userStorage.Get(database, userTag, Language.NotApplicable);
                        if (user != null)
                        {
                            user.Selected = true;
                            userStorage.Add(database, user);

                            synchronizationParameters.userSelected[user.Token] = true;
                        }
                    }
                }

                // continue download process - phase 3
                if ((Configuration.maniphest?.projectTags != null && Configuration.maniphest.projectTags.Any()) || 
                    (Configuration.maniphest?.userTags != null && Configuration.maniphest.userTags.Any()))
                {
                    ConsoleWriteStatus("Downloading Maniphest priorities and states...");
                    synchronizationController.ProgressMethod_DownloadManiphestPrioritiesAndStates(synchronizationParameters, 0, 0);
                    ConsoleWriteStatus("Downloading Maniphest tasks...");
                    synchronizationController.ProgressMethod_DownloadManiphestTasks(synchronizationParameters, 0, 0);

                }

                if ((Configuration.phriction?.projectTags != null && Configuration.phriction.projectTags.Any()) || 
                    (Configuration.phriction?.userTags != null && Configuration.phriction.userTags.Any()))
                {
                    ConsoleWriteStatus("Downloading Phriction wiki documents...");
                    synchronizationController.ProgressMethod_DownloadPhrictionDocuments(synchronizationParameters, 0, 0);
                }
                ConsoleWriteStatus("Downloading referenced files...");
                synchronizationController.ProgressMethod_DownloadFileObjects(synchronizationParameters, 0, 0);
                ConsoleWriteStatus("Downloading macro objects...");
                synchronizationController.ProgressMethod_DownloadMacros(synchronizationParameters, 0, 0);
                ConsoleWriteStatus("Cleaning up data...");
                synchronizationController.ProgressMethod_DeleteOldData(synchronizationParameters, 0, 0);
                ConsoleWriteStatus("Saving synchronization timestamp...");
                synchronizationController.ProgressMethod_SaveLastSynchronizeTimeStamp(synchronizationParameters, 0, 0);

                ConsoleWriteStatus("Finalizing master data...");
                synchronizationController.ProgressMethod_Finalize(synchronizationParameters, 0, 0);

                // clear sensitive information in database (1)
                List<string> referencedTokens = new List<string>();
                List<Phabricator.Data.Maniphest> downloadedManiphestTasks = maniphestStorage.Get(database, Language.NotApplicable).ToList();
                List<Phabricator.Data.Phriction> downloadedPhrictionDocuments = phrictionStorage.Get(database, Language.NotApplicable).ToList();

                synchronizationParameters.existingAccount.PhabricatorUrl = "";
                accountStorage.Add(database, synchronizationParameters.existingAccount);

                if (string.IsNullOrWhiteSpace(Configuration.phriction?.initialPath) == false)
                {
                    // delete all phriction document whose path does not start with c
                    string initialPath = Configuration.phriction.initialPath;
                    if (initialPath.StartsWith("http://") || initialPath.StartsWith("https://"))
                    {
                        Match absoluteInitialPath = RegexSafe.Match(initialPath, "/w/", RegexOptions.IgnoreCase);
                        if (absoluteInitialPath.Success)
                        {
                            initialPath = initialPath.Substring(absoluteInitialPath.Index);
                        }
                        else
                        {
                            initialPath = "";
                        }
                    }
                    initialPath = initialPath.TrimStart('/');
                    if (initialPath.StartsWith("w/"))
                    {
                        initialPath = initialPath.Substring("w/".Length);
                    }

                    foreach (Phabricator.Data.Phriction downloadedInvalidPhrictionDocument in downloadedPhrictionDocuments.Where(wiki => wiki.Path.StartsWith(initialPath) == false)
                                                                                                                          .OrderByDescending(wiki => wiki.Path.Length)
                                                                                                                          .ToArray())
                    {
                        phrictionStorage.Remove(database, downloadedInvalidPhrictionDocument);

                        downloadedPhrictionDocuments.Remove(downloadedInvalidPhrictionDocument);
                    }
                }

                if (downloadedManiphestTasks.Any() == false && downloadedPhrictionDocuments.Any() == true)
                {
                    // collect all referenced users and projects
                    foreach (Phabricator.Data.Phriction phriction in downloadedPhrictionDocuments)
                    {
                        if (referencedTokens.Contains(phriction.Author) == false)
                        {
                            referencedTokens.Add(phriction.Author);
                        }

                        if (referencedTokens.Contains(phriction.LastModifiedBy) == false)
                        {
                            referencedTokens.Add(phriction.LastModifiedBy);
                        }

                        foreach (string referencedProject in phriction.Projects
                                                                      .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                                      .Where(project => referencedTokens.Contains(project) == false)
                                )
                        {
                            referencedTokens.Add(referencedProject);
                        }

                        foreach (string referencedSubscriber in phriction.Subscribers
                                                                         .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                                         .Where(subscriber => referencedTokens.Contains(subscriber) == false)
                                )
                        {
                            referencedTokens.Add(referencedSubscriber);
                        }
                    }
                }

                // store public encryption key
                database.SetConfigurationParameter("EncryptionKey", database.EncryptionKey);

                // validate downloaded content
                ConsoleWriteStatus("Validating...");
                
                informationalMessages.Add(string.Format("Number of Maniphest tasks    : {0}", downloadedManiphestTasks.Count()));
                informationalMessages.Add(string.Format("Number of Phriction documents: {0}", downloadedPhrictionDocuments.Count()));
                informationalMessages.Add(string.Format("Number of referenced files   : {0}", Storage.File.Count(database)));
                informationalMessages.Add("");

                // == validation - step 1: correct absolute URLs in Maniphest tasks and Phriction documents =============================================
                // replace absolute URLs of our Phabricator server in Maniphest tasks into relative URLs
                Phabricator.Data.Maniphest[] maniphestTasksMentioningPhabricatorUrl = downloadedManiphestTasks.Where(maniphest => maniphest.Description
                                                                                                                                           .Contains(Configuration.source)
                                                                                                                    )
                                                                                                              .ToArray();
                foreach (Phabricator.Data.Maniphest maniphestTask in maniphestTasksMentioningPhabricatorUrl)
                {
                    maniphestTask.Description = maniphestTask.Description.Replace(Configuration.source.TrimEnd('/') + "/T", "maniphest/T");
                    maniphestTask.Description = maniphestTask.Description.Replace(Configuration.source.TrimEnd('/') + "/w/", "w/");
                    if (maniphestTask.Description.Contains(Configuration.source))
                    {
                        informationalMessages.Add(string.Format("WARNING: \"{0}\" is mentioned in Maniphest task \"{1}\"", Configuration.source, maniphestTask.ID));
                    }

                    maniphestStorage.Add(database, maniphestTask);
                }
                // replace absolute URLs of our Phabricator server in Phriction documents into relative URLs
                Phabricator.Data.Phriction[] phrictionDocumentsMentioningPhabricatorUrl = downloadedPhrictionDocuments.Where(phriction => phriction.Content
                                                                                                                                                   .Contains(Configuration.source)
                                                                                                                            )
                                                                                                                      .ToArray();
                foreach (Phabricator.Data.Phriction phrictionDocument in phrictionDocumentsMentioningPhabricatorUrl)
                {
                    phrictionDocument.Content = phrictionDocument.Content.Replace(Configuration.source.TrimEnd('/') + "/T", "maniphest/T");
                    phrictionDocument.Content = phrictionDocument.Content.Replace(Configuration.source.TrimEnd('/') + "/w/", "w/");
                    if (phrictionDocument.Content.Contains(Configuration.source))
                    {
                        informationalMessages.Add(string.Format("WARNING: \"{0}\" is mentioned in Maniphest task \"{1}\"", Configuration.source, phrictionDocument.Path));
                    }

                    phrictionStorage.Add(database, phrictionDocument);
                }


                // == validation - step 2: search for invalid URLs and collect them by means of the Database_InvalidUrlFound event ======================
                foreach (Phabricator.Data.Maniphest maniphestTask in downloadedManiphestTasks)
                {
                    Parsers.Remarkup.RemarkupParserOutput remarkupParserOutput;
                    synchronizationController.ConvertRemarkupToHTML(database, "maniphest/" + maniphestTask.ID, maniphestTask.Description, out remarkupParserOutput, false);
                }

                foreach (Phabricator.Data.Phriction phrictionDocument in downloadedPhrictionDocuments)
                {
                    // convert Remarkup content to HTML
                    // in case any invalid URLs are found, the Database_InvalidUrlFound method will be executed
                    Parsers.Remarkup.RemarkupParserOutput remarkupParserOutput;
                    synchronizationController.ConvertRemarkupToHTML(database, phrictionDocument.Path, phrictionDocument.Content, out remarkupParserOutput, false);

                    // collect all referenced users in Remarkup content
                    foreach (RuleReferenceUser ruleReferenceUser in remarkupParserOutput.TokenList
                                                                                        .OfType<RuleReferenceUser>()
                                                                                        .Distinct()
                                                                                        .Where(usr => referencedTokens.Contains(usr.UserToken) == false)
                                                                                        .ToArray())
                    {
                        Phabricator.Data.User user = userStorage.Get(database, ruleReferenceUser.UserToken, Language.NotApplicable);
                        if (user != null)
                        {
                            userStorage.Remove(database, user);
                        }
                    }

                    // collect all referenced projects in Remarkup content
                    foreach (RuleReferenceProject ruleReferenceProject in remarkupParserOutput.TokenList
                                                                                              .OfType<RuleReferenceProject>()
                                                                                              .Distinct()
                                                                                              .Where(proj => referencedTokens.Contains(proj.ProjectToken) == false)
                                                                                              .ToArray())
                    {
                        Phabricator.Data.Project project = projectStorage.Get(database, ruleReferenceProject.ProjectToken, Language.NotApplicable);
                        if (project != null)
                        {
                            projectStorage.Remove(database, project);
                        }
                    }

                    // validate tables (check if the number of header columns matches with the number of columns in the other rows)
                    foreach (RuleTable ruleTable in remarkupParserOutput.TokenList.OfType<RuleTable>().Distinct().ToArray())
                    {
                        Match tableHeadBody = RegexSafe.Match(ruleTable.Html, "<thead>(.*?(?<!</thead>))\\s*<tr>(\\s*<th>(.*?(?<!</th>))</th>)+\\s*</tr>\\s*</thead>\\s*<tbody>(.*?(?<!</tbody>))(\\s*<tr>(\\s*<t[hd]>(.*?(?<!</t[hd]>))</t[hd]>)+\\s*</tr>)+\\s*</tbody>", RegexOptions.Singleline);
                        if (tableHeadBody.Success)
                        {
                            int numberOfHeaderColumns = tableHeadBody.Groups[2].Captures.Count;
                            foreach (Capture tableBodyRow in tableHeadBody.Groups[5].Captures)
                            {
                                Match row = RegexSafe.Match(tableBodyRow.Value, "<tr>(\\s*<t[hd]>(.*?(?<!</t[hd]>))</t[hd]>)+\\s*</tr>", RegexOptions.Singleline);
                                if (row.Success)
                                {
                                    int numberOfColumns = row.Groups[1].Captures.Count;
                                    if (numberOfHeaderColumns != numberOfColumns)
                                    {
                                        informationalMessages.Add(string.Format("WARNING: Table found in \"{0}\" with {1} header columns and a row with {2} columns", phrictionDocument.Path, numberOfHeaderColumns, numberOfColumns));
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }


                // clear sensitive information in database (2)
                // remove all users which are not referenced
                foreach (Phabricator.Data.User user in userStorage.Get(database, Language.NotApplicable)
                                                                  .Where(usr => referencedTokens.Contains(usr.Token) == false)
                                                                  .ToArray())
                {
                    userStorage.Remove(database, user);
                }

                // remove all projects which are not referenced
                foreach (Phabricator.Data.Project project in projectStorage.Get(database, Language.NotApplicable)
                                                                           .Where(proj => referencedTokens.Contains(proj.Token) == false)
                                                                           .ToArray()
                        )
                {
                    projectStorage.Remove(database, project);
                }

                // optimize database
                database.Shrink();

                // show all referenced projects
                Phabricator.Data.Project[] referencedProjects = referencedTokens.Where(projectToken => projectToken.StartsWith(Phabricator.Data.Project.Prefix))
                                                                                .Select(projectToken => projectStorage.Get(database, projectToken, Language.NotApplicable))
                                                                                .Where(project => project != null)
                                                                                .ToArray();
                if (referencedProjects.Any())
                {
                    informationalMessages.Add("");
                    informationalMessages.Add("The following project(s) were referenced:");
                    foreach (Phabricator.Data.Project referencedProject in referencedProjects.OrderBy(project => project.InternalName))
                    {
                        informationalMessages.Add(string.Format(" - {0}  ({1})", referencedProject.InternalName, referencedProject.Name));
                    }
                }

                // show all referenced users
                Phabricator.Data.User[] referencedUsers = referencedTokens.Where(userToken => userToken.StartsWith(Phabricator.Data.User.Prefix))
                                                                                .Select(userToken => userStorage.Get(database, userToken, Language.NotApplicable))
                                                                                .Where(user => user != null)
                                                                                .ToArray();
                if (referencedUsers.Any())
                {
                    informationalMessages.Add("");
                    informationalMessages.Add("The following user(s) were referenced:");
                    foreach (Phabricator.Data.User referencedUser in referencedUsers.OrderBy(user => user.UserName))
                    {
                        informationalMessages.Add(string.Format(" - {0}  ({1})", referencedUser.UserName, referencedUser.RealName));
                    }
                }

                if (Configuration.maniphest?.showTasks == true)
                {
                    informationalMessages.Add("");
                    informationalMessages.Add("The following maniphest tasks(s) are included:");
                    foreach (Phabricator.Data.Maniphest downloadedManiphestTask in downloadedManiphestTasks.OrderBy(task => task.ID))
                    {
                        informationalMessages.Add(string.Format(" - T{0}\r\n     => {1}", downloadedManiphestTask.ID, downloadedManiphestTask.Name));
                    }
                }

                if (Configuration.phriction?.showDocuments == true)
                {
                    informationalMessages.Add("");
                    informationalMessages.Add("The following wiki documents(s) are included:");
                    foreach (Phabricator.Data.Phriction downloadedPhrictionDocument in downloadedPhrictionDocuments.OrderBy(wiki => wiki.Path))
                    {
                        informationalMessages.Add(string.Format(" - {0}\r\n     => {1}", downloadedPhrictionDocument.Path, downloadedPhrictionDocument.Name));
                    }
                }

                ConsoleWriteStatus("");
            }
        }

        /// <summary>
        /// Executes the strip action
        /// </summary>
        private void ExecuteStrip()
        {
            Logging.SetLoggingEventHandler(LoggingEvent);

            Storage.Account accountStorage = new Storage.Account();
            Storage.File fileStorage = new Storage.File();
            Storage.Keyword keywordStorage = new Storage.Keyword();
            Storage.Phriction phrictionStorage = new Storage.Phriction();
            Storage.Stage stageStorage = new Storage.Stage();
            Controllers.Synchronization synchronizationController = new Controllers.Synchronization();  // will be further initialized later

            // enter credentials for accessing source Phabrico database
            string sourceDatabasePath = Storage.Database.DataSource;
            string sourcePhabricoUsername = ConsoleReadString("Enter user name of the source Phabrico: ");
            string sourcePhabricoPassword = ConsoleReadString("Enter password of the source Phabrico: ");

            // delete destination Phabrico database if existing
            if (System.IO.File.Exists(Configuration.destination))
            {
                System.IO.File.Delete(Configuration.destination);
            }
            string translationFile = System.IO.Path.GetDirectoryName(Configuration.destination) + "\\Phabrico.translation";
            if (System.IO.File.Exists(translationFile))
            {
                System.IO.File.Delete(translationFile);
            }

            // verify credentials
            // create account in new database
            Phabricator.Data.Account whoAmI;
            string sourceTokenHash = Encryption.GenerateTokenKey(sourcePhabricoUsername, sourcePhabricoPassword);  // tokenHash is stored in the database
            string sourcePublicEncryptionKey = Encryption.GenerateEncryptionKey(sourcePhabricoUsername, sourcePhabricoPassword);  // encryptionKey is not stored in database (except when security is disabled)
            string sourcePrivateEncryptionKey = Encryption.GeneratePrivateEncryptionKey(sourcePhabricoUsername, sourcePhabricoPassword);  // privateEncryptionKey is not stored in database
            using (Storage.Database database = new Storage.Database(null))
            {
                bool noUserConfigured;

                UInt64[] publicXorCipher = database.ValidateLogIn(sourceTokenHash, out noUserConfigured);
                if (publicXorCipher == null)
                {
                    throw new ArgumentException("Invalid username or password");
                }

                database.EncryptionKey = sourcePublicEncryptionKey;
                database.PrivateEncryptionKey = sourcePrivateEncryptionKey;
                whoAmI = accountStorage.Get(database, sourceTokenHash, Language.NotApplicable);
            }

            string destinationTokenHash = Encryption.GenerateTokenKey(Configuration.username, Configuration.password);  // tokenHash is stored in the database
            string destinationPublicEncryptionKey = Encryption.GenerateEncryptionKey(Configuration.username, Configuration.password);  // encryptionKey is not stored in database (except when security is disabled)
            string destinationPrivateEncryptionKey = Encryption.GeneratePrivateEncryptionKey(Configuration.username, Configuration.password);  // privateEncryptionKey is not stored in database

            // create account in new database
            Storage.Database.DataSource = Configuration.destination;
            Phabricator.Data.Account newAccount = new Phabricator.Data.Account();
            using (Storage.Database database = new Storage.Database(null))
            {
                bool noUserConfigured;
                UInt64[] publicXorCipher = database.ValidateLogIn(destinationTokenHash, out noUserConfigured);
                if (publicXorCipher != null || noUserConfigured == false)
                {
                    throw new System.Exception("New database was not correctly initialized");
                }

                newAccount.ConduitAPIToken = "";
                newAccount.PhabricatorUrl = "";
                newAccount.Token = destinationTokenHash;
                newAccount.UserName = Configuration.username;
                newAccount.Parameters = new Phabricator.Data.Account.Configuration();
                newAccount.Parameters.AccountType = Phabricator.Data.Account.AccountTypes.PrimaryUser;
                newAccount.Parameters.ColumnHeadersToHide = "".Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                newAccount.Parameters.UserToken = whoAmI.Parameters.UserToken;
                newAccount.Theme = "light";

                database.EncryptionKey = destinationPublicEncryptionKey;
                database.PrivateEncryptionKey = destinationPrivateEncryptionKey;

                accountStorage.Add(database, newAccount);

                // complete initialization synchronizationController
                Http.Server httpServer = new Http.Server(false, -1, "/", true);
                Miscellaneous.HttpListenerContext httpListenerContext = new Miscellaneous.HttpListenerContext();
                synchronizationController.browser = new Http.Browser(httpServer, httpListenerContext);
                synchronizationController.EncryptionKey = destinationPublicEncryptionKey;

                Http.SessionManager.Token token = synchronizationController.browser.HttpServer.Session.CreateToken(newAccount.Token, synchronizationController.browser);
                synchronizationController.browser.SetCookie("token", token.ID, true);
                token.EncryptionKey = Encryption.XorString(database.EncryptionKey, newAccount.PublicXorCipher);
                token.PrivateEncryptionKey = Encryption.XorString(database.PrivateEncryptionKey, newAccount.PrivateXorCipher);
                synchronizationController.TokenId = token.ID;
                synchronizationController.browser.ResetToken(token);

            }

            // copy Maniphest priorities and statuses
            ConsoleWriteStatus("Copying Maniphest priorities and statuses...");
            Storage.ManiphestPriority.Copy(sourceDatabasePath, sourcePhabricoUsername, sourcePhabricoPassword, Configuration.destination, Configuration.username, Configuration.password);
            Storage.ManiphestStatus.Copy(sourceDatabasePath, sourcePhabricoUsername, sourcePhabricoPassword, Configuration.destination, Configuration.username, Configuration.password);

            // copy all user and projects (will be filtered out later...)
            List<Phabricator.Data.User> destinationUsers = Storage.User.Copy(sourceDatabasePath, sourcePhabricoUsername, sourcePhabricoPassword, Configuration.destination, Configuration.username, Configuration.password);
            List<Phabricator.Data.Project> destinationProjects = Storage.Project.Copy(sourceDatabasePath, sourcePhabricoUsername, sourcePhabricoPassword, Configuration.destination, Configuration.username, Configuration.password);

            // convert configured project tags to project tokens
            Configuration.maniphest.projectTags = Configuration.maniphest?.projectTags
                                                                         ?.Select(project => destinationProjects.FirstOrDefault(p => p.InternalName.Equals(project)
                                                                                                                                  || p.Token.Equals(project)
                                                                                                                               )
                                                                                 )
                                                                          .Select(project => project.Token)
                                                                          .ToArray();
            Configuration.phriction.projectTags = Configuration.phriction?.projectTags
                                                                         ?.Select(project => destinationProjects.FirstOrDefault(p => p.InternalName.Equals(project)
                                                                                                                                  || p.Token.Equals(project)
                                                                                                                               )
                                                                                 )
                                                                          .Select(project => project.Token)
                                                                          .ToArray();

            // convert configured users to user tokens
            Configuration.maniphest.userTags = Configuration.maniphest?.userTags
                                                                      ?.Select(user => destinationUsers.FirstOrDefault(u => u.UserName.Equals(user)
                                                                                                                         || u.Token.Equals(user)
                                                                                                                      )
                                                                              )
                                                                       .Select(user => user.Token)
                                                                       .ToArray();
            Configuration.phriction.userTags = Configuration.phriction?.userTags
                                                                      ?.Select(user => destinationUsers.FirstOrDefault(u => u.UserName.Equals(user)
                                                                                                                         || u.Token.Equals(user)
                                                                                                                      )
                                                                              )
                                                                       .Select(user => user.Token)
                                                                       .ToArray();

            List<Phabricator.Data.Phriction> destinationWikis = new List<Phabricator.Data.Phriction>();
            List<Phabricator.Data.Maniphest> destinationTasks = new List<Phabricator.Data.Maniphest>();

            if (Configuration.maniphest != null)
            {
                // copy Maniphest tasks
                ConsoleWriteStatus("Copying Maniphest tasks...");
                destinationTasks = Storage.Maniphest.Copy(sourceDatabasePath, sourcePhabricoUsername, sourcePhabricoPassword,
                                       Configuration.destination, Configuration.username, Configuration.password,
                                       task => (Configuration.maniphest.projectTags != null
                                                && Configuration.maniphest.projectTags.Any()
                                                && Configuration.maniphest.projectTags.Any(project => task.Projects.Split(',').Contains(project))
                                               )
                                               ||
                                               (Configuration.maniphest.userTags != null
                                                && Configuration.maniphest.userTags.Any()
                                                && Configuration.maniphest.userTags.Any(user => task.Owner.Equals(user)
                                                                                             || task.Author.Equals(user)
                                                                                             || task.Subscribers.Split(',').Contains(user)
                                                                                       )
                                               )
                                    );
            }

            if (Configuration.phriction != null)
            {
                // copy all phriction documents (will be filtered out later)
                ConsoleWriteStatus("Copying Phriction wiki documents...");
                destinationWikis = Storage.Phriction.Copy(sourceDatabasePath, sourcePhabricoUsername, sourcePhabricoPassword,
                                                          Configuration.destination, Configuration.username, Configuration.password
                                                         );

                if (Configuration.phriction.projectTags != null)
                {
                    // search for wiki's which have at least one of the configured projects tagged
                    Phabricator.Data.Phriction[] projectTaggedWikis = destinationWikis.Where(wiki => string.IsNullOrEmpty(wiki.Projects) == false
                                                                                                  && wiki.Projects.Split(',')
                                                                                                          .Any(project => Configuration.phriction.projectTags.Contains(project))
                                                                                            )
                                                                                      .ToArray();

                    if (Configuration.phriction.combined)
                    {
                        if (Configuration.phriction.tree)
                        {
                            // remove wiki's from the list which don't have all tags in their (top) hierarchy tree
                            Dictionary<Phabricator.Data.Phriction, List<string>> combinedProjectTaggedWikis = projectTaggedWikis.ToDictionary(key => key,
                                                                                                                                             value => value.Projects.Split(',')
                                                                                                                                                           .Where(p => Configuration.phriction.projectTags.Contains(p))
                                                                                                                                                           .ToList()
                                                                                                                                            );
                            foreach (Phabricator.Data.Phriction wiki in combinedProjectTaggedWikis.OrderByDescending(key => key.Key.Path.Length)
                                                                                                  .Select(k => k.Key)
                                                                                                  .ToArray())
                            {
                                string[] missingProjects = Configuration.phriction.projectTags
                                                                                  .Where(missingProject => combinedProjectTaggedWikis[wiki].Contains(missingProject) == false)
                                                                                  .ToArray();
                                foreach (string missingProject in missingProjects)
                                {
                                    string rootUrlForMissingProject = combinedProjectTaggedWikis.OrderBy(key => key.Key.Path.Length)
                                                                                                .FirstOrDefault(key => key.Value.Contains(missingProject)
                                                                                                                    && wiki.Path.StartsWith(key.Key.Path)
                                                                                                               )
                                                                                                .Key?.Path;
                                    if (string.IsNullOrEmpty(rootUrlForMissingProject))
                                    {
                                        combinedProjectTaggedWikis.Remove(wiki);
                                        break;
                                    }
                                }
                            }

                            // remove Phriction records which are not in the 'keeping' list
                            Storage.Database.DataSource = Configuration.destination;
                            using (Storage.Database database = new Storage.Database(destinationPublicEncryptionKey))
                            {
                                List<Phabricator.Data.Phriction> invalidWikiPages = destinationWikis.Where(wiki => combinedProjectTaggedWikis.Keys.All(w => wiki.Path.StartsWith(w.Path) == false))
                                                                                                    .ToList();
                                foreach (var invalidWikiPage in invalidWikiPages)
                                {
                                    phrictionStorage.Remove(database, invalidWikiPage);
                                    destinationWikis.Remove(invalidWikiPage);
                                }
                            }
                        }
                        else
                        {
                            // remove Phriction records which do not have all project tags
                            Storage.Database.DataSource = Configuration.destination;
                            using (Storage.Database database = new Storage.Database(destinationPublicEncryptionKey))
                            {
                                List<Phabricator.Data.Phriction> invalidWikiPages = destinationWikis.Where(wiki => Configuration.phriction.projectTags.Any(project => wiki.Projects.Contains(project) == false))
                                                                                                    .ToList();
                                foreach (var invalidWikiPage in invalidWikiPages)
                                {
                                    phrictionStorage.Remove(database, invalidWikiPage);
                                    destinationWikis.Remove(invalidWikiPage);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (Configuration.phriction.tree)
                        {
                            // TODO
                        }
                        else
                        {
                            // TODO
                        }
                    }

                    // check if root wiki document is available
                    if (destinationWikis.Any(wiki => wiki.Path.Equals("/")) == false)
                    {
                        // no root document -> create alias
                        Storage.Database.DataSource = Configuration.destination;
                        using (Storage.Database database = new Storage.Database(destinationPublicEncryptionKey))
                        {
                            int minimumLength = destinationWikis.Min(wiki => wiki.Path.Split('/').Length);
                            IEnumerable<Phabricator.Data.Phriction> rootDocuments = phrictionStorage.Get(database, Language.NotApplicable)
                                                                                                    .Where(document => document.Path.Split('/').Length == minimumLength);
                            if (rootDocuments.Count() == 1)
                            {
                                Phabricator.Data.Phriction linkedDocument = rootDocuments.FirstOrDefault();
                                phrictionStorage.AddAlias(database, "/", linkedDocument);
                            }
                            else
                            {
                                Phabricator.Data.Phriction coverPage = new Phabricator.Data.Phriction();
                                coverPage.Path = "/";
                                coverPage.Content = "";
                                coverPage.Token = Phabricator.Data.Phriction.PrefixCoverPage + "HOMEPAGE";
                                phrictionStorage.Add(database, coverPage);

                                foreach (Phabricator.Data.Phriction rootDocument in rootDocuments)
                                {
                                    database.DescendTokenFrom(coverPage.Token, rootDocument.Token);
                                }
                            }
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(Configuration.phriction?.translation) == false)
                {
                    // copy translations
                    ConsoleWriteStatus("Copying translation(s)...");

                    string[] destinationPhrictionTokens = destinationWikis.Select(wiki => wiki.Token).ToArray();
                    Phabrico.Storage.Content.Copy(sourceDatabasePath, sourcePhabricoUsername, sourcePhabricoPassword,
                                                  Configuration.destination, Configuration.username, Configuration.password,
                                                  translation => destinationPhrictionTokens.Contains(translation.Token)
                                                 );
                }

                // copy hierachies
                ConsoleWriteStatus("Copying Phriction wiki hierarchies...");
                using (Storage.Database database = new Storage.Database(destinationPublicEncryptionKey))
                {
                    database.CopyObjectHierarchyInfo(sourceDatabasePath, sourcePhabricoUsername, sourcePhabricoPassword,
                                                     Configuration.destination, Configuration.username, Configuration.password
                                                    );
                }
            }

            if (Configuration.phriction != null || Configuration.maniphest != null)
            {
                ConsoleWriteStatus("Generating search info...");
                Storage.Database.DataSource = Configuration.destination;
                using (Storage.Database database = new Storage.Database(destinationPublicEncryptionKey))
                {
                    database.PrivateEncryptionKey = destinationPrivateEncryptionKey;

                    foreach (Phabricator.Data.Maniphest destinationTask in destinationTasks)
                    {
                        // get all available words from maniphest task and save it into search database
                        keywordStorage.AddPhabricatorObject(synchronizationController, database, destinationTask);
                    }
                    foreach (Phabricator.Data.Phriction destintionWiki in destinationWikis)
                    {
                        // get all available words from phriction document and save it into search database
                        keywordStorage.AddPhabricatorObject(synchronizationController, database, destintionWiki);
                    }
                }
            }

            // copying referenced files
            ConsoleWriteStatus("Copying file objects...");
            List<int> referencedFileIDs = destinationWikis.Select(wiki => wiki.Content)
                                                          .Concat(destinationTasks.Select(task => task.Description))
                                                          .Concat(destinationTasks.SelectMany(task => task.Transactions.Select(transaction => transaction.NewValue)))
                                                          .SelectMany(remarkup => RegexSafe.Matches(remarkup, "{F(-?[0-9]+)([^}]*)}", RegexOptions.None)
                                                                                           .OfType<Match>()
                                                                                           .Select(m => m.Groups.OfType<Group>()
                                                                                                         .Skip(1)
                                                                                                         .FirstOrDefault()
                                                                                                         ?.Value)
                                                                     )
                                                          .Distinct()
                                                          .Select(fileID => Int32.Parse(fileID))
                                                          .ToList();
            List<Phabricator.Data.File> files = new List<Phabricator.Data.File>();
            Storage.Database.DataSource = null;
            using (Storage.Database database = new Storage.Database(sourcePublicEncryptionKey))
            {
                foreach (int fileID in referencedFileIDs)
                {
                    Phabricator.Data.File file;
                    if (fileID > 0)
                    {
                        file = fileStorage.GetByID(database, fileID, false);
                    }
                    else
                    {
                        file = stageStorage.Get<Phabricator.Data.File>(database, Language.NotApplicable, Phabricator.Data.File.Prefix, fileID, true);
                    }

                    if (file == null)
                    {
                        informationalMessages.Add(string.Format("WARNING: Invalid referenced file found: \"F{0}\"", fileID));
                    }
                    else
                    {
                        files.Add(file);
                    }
                }
            }

            Storage.Database.DataSource = Configuration.destination;
            using (Storage.Database database = new Storage.Database(destinationPublicEncryptionKey))
            {
                foreach (Phabricator.Data.File file in files)
                {
                    if (file.ID > 0)
                    {
                        fileStorage.Add(database, file);
                    }
                    else
                    {
                        stageStorage.Create(database, synchronizationController.browser, file);
                    }
                }
            }

            // copy relations
            ConsoleWriteStatus("Copying object relations...");
            using (Storage.Database database = new Storage.Database(destinationPublicEncryptionKey))
            {
                database.CopyObjectRelationInfo(sourceDatabasePath, sourcePhabricoUsername, sourcePhabricoPassword,
                                                Configuration.destination, Configuration.username, Configuration.password
                                               );
            }
        }

        /// <summary>
        /// Returns true if a given file path is in a valid format
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private bool FilePathIsValid(string filePath)
        {
            System.IO.FileInfo fi = null;
            try
            {
                fi = new System.IO.FileInfo(filePath);
            }
            catch (ArgumentException)
            {
                // ignore exception
            }
            catch (System.IO.PathTooLongException)
            {
                // ignore exception
            }
            catch (NotSupportedException)
            {
                // ignore exception
            }

            return fi != null;
        }

        /// <summary>
        /// This method is executed when Phabrico is logging
        /// In case of Commander, all logging should be ignored
        /// </summary>
        /// <param name="message"></param>
        private void LoggingEvent(string message)
        {
            // do nothing
        }

        /// <summary>
        /// Show which command line arguments can be used for Phabrico
        /// </summary>
        private void ShowUsage()
        {
            string programName = System.IO.Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);

            Console.WriteLine("To download data from Phabricator and create a Phabrico database file:");
            Console.WriteLine("  {0} /download  [json-config-file]  /token:[phabricator-conduit-token]", programName);
            Console.WriteLine("");
            Console.WriteLine("To show this help:");
            Console.WriteLine("  {0} /?", programName);
        }
    }
}
