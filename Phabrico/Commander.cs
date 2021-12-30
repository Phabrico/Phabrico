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
            }

            public class Phriction
            {
                public string[] projectTags { get; set; }
                public string[] userTags { get; set; }
                public bool combined { get; set; } = true;
                public bool tree { get; set; } = true;
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
            Nothing
        }

        /// <summary>
        /// Is used by the ConsoleWriteStatus to clear the previous message from the console before writing the new one
        /// </summary>
        private string lastConsoleStatusMessage = "";

        /// <summary>
        /// List of messages which should be shown at the end of the Commander execution
        /// </summary>
        private List<string> informationalMessages = new List<string>();

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

                            Action = CommanderAction.Download;
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
        /// Writes a message to the command prompt on the same line as the previous message
        /// </summary>
        /// <param name="message"></param>
        public void ConsoleWriteStatus(string message)
        {
            Console.Write("\r");
            foreach (char c in lastConsoleStatusMessage) Console.Write(" ");
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
                synchronizationParameters.existingAccount.Parameters.Synchronization = (Configuration.maniphest.projectTags != null && Configuration.maniphest.projectTags.Any()) ? Phabricator.Data.Account.SynchronizationMethod.ManiphestSelectedProjectsOnly
                                                                                     : (Configuration.maniphest.userTags != null && Configuration.maniphest.userTags.Any()) ? Phabricator.Data.Account.SynchronizationMethod.ManiphestSelectedUsersOnly
                                                                                     : Phabricator.Data.Account.SynchronizationMethod.None;
                synchronizationParameters.existingAccount.Parameters.Synchronization |= (Configuration.phriction.combined && Configuration.phriction.projectTags != null && Configuration.phriction.projectTags.Any()) ? Phabricator.Data.Account.SynchronizationMethod.PhrictionAllSelectedProjectsOnly
                                                                                      : (Configuration.phriction.combined == false && Configuration.phriction.projectTags != null && Configuration.phriction.projectTags.Any()) ? Phabricator.Data.Account.SynchronizationMethod.PhrictionSelectedProjectsOnly
                                                                                      : (Configuration.phriction.userTags != null && Configuration.phriction.userTags.Any()) ? Phabricator.Data.Account.SynchronizationMethod.PhrictionSelectedUsersOnly
                                                                                      : Phabricator.Data.Account.SynchronizationMethod.None;
                synchronizationParameters.existingAccount.Parameters.Synchronization |= (Configuration.phriction.tree && Configuration.phriction.combined) ? Phabricator.Data.Account.SynchronizationMethod.PhrictionAllSelectedProjectsOnlyIncludingDocumentTree
                                                                                      : (Configuration.phriction.tree && Configuration.phriction.combined == false) ? Phabricator.Data.Account.SynchronizationMethod.PhrictionSelectedProjectsOnlyIncludingDocumentTree
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

                    string[] maniphestProjectTags = Configuration.maniphest.projectTags ?? new string[0];
                    string[] phrictionProjectTags = Configuration.phriction.projectTags ?? new string[0];

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
                if ((Configuration.maniphest.projectTags != null && Configuration.maniphest.projectTags.Any()) || 
                    (Configuration.maniphest.userTags != null && Configuration.maniphest.userTags.Any()))
                {
                    ConsoleWriteStatus("Downloading Maniphest priorities and states...");
                    synchronizationController.ProgressMethod_DownloadManiphestPrioritiesAndStates(synchronizationParameters, 0, 0);
                    ConsoleWriteStatus("Downloading Maniphest tasks...");
                    synchronizationController.ProgressMethod_DownloadManiphestTasks(synchronizationParameters, 0, 0);

                }

                if ((Configuration.phriction.projectTags != null && Configuration.phriction.projectTags.Any()) || 
                    (Configuration.phriction.userTags != null && Configuration.phriction.userTags.Any()))
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
                Phabricator.Data.Maniphest[] downloadedManiphestTasks = maniphestStorage.Get(database, Language.NotApplicable).ToArray();
                Phabricator.Data.Phriction[] downloadedPhrictionDocuments = phrictionStorage.Get(database, Language.NotApplicable).ToArray();

                synchronizationParameters.existingAccount.PhabricatorUrl = "";
                accountStorage.Add(database, synchronizationParameters.existingAccount);

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

                        foreach (string referencedProject in phriction.Projects.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (referencedTokens.Contains(referencedProject) == false)
                            {
                                referencedTokens.Add(referencedProject);
                            }
                        }

                        foreach (string referencedSubscriber in phriction.Subscribers.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (referencedTokens.Contains(referencedSubscriber) == false)
                            {
                                referencedTokens.Add(referencedSubscriber);
                            }
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
                    foreach (RuleReferenceUser ruleReferenceUser in remarkupParserOutput.TokenList.OfType<RuleReferenceUser>().Distinct().ToArray())
                    {
                        if (referencedTokens.Contains(ruleReferenceUser.UserToken) == false)
                        {
                            Phabricator.Data.User user = userStorage.Get(database, ruleReferenceUser.UserToken, Language.NotApplicable);
                            if (user != null)
                            {
                                userStorage.Remove(database, user);
                            }
                        }
                    }

                    // collect all referenced projects in Remarkup content
                    foreach (RuleReferenceProject ruleReferenceProject in remarkupParserOutput.TokenList.OfType<RuleReferenceProject>().Distinct().ToArray())
                    {
                        if (referencedTokens.Contains(ruleReferenceProject.ProjectToken) == false)
                        {
                            Phabricator.Data.Project project = projectStorage.Get(database, ruleReferenceProject.ProjectToken, Language.NotApplicable);
                            if (project != null)
                            {
                                projectStorage.Remove(database, project);
                            }
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
                foreach (Phabricator.Data.User user in userStorage.Get(database, Language.NotApplicable).ToArray())
                {
                    if (referencedTokens.Contains(user.Token) == false)
                    {
                        userStorage.Remove(database, user);
                    }
                }

                // remove all projects which are not referenced
                foreach (Phabricator.Data.Project project in projectStorage.Get(database, Language.NotApplicable).ToArray())
                {
                    if (referencedTokens.Contains(project.Token) == false)
                    {
                        projectStorage.Remove(database, project);
                    }
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

                ConsoleWriteStatus("");
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
            catch (ArgumentException) { }
            catch (System.IO.PathTooLongException) { }
            catch (NotSupportedException) { }

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
