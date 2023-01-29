using Newtonsoft.Json;
using Phabrico.Http;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;
using Phabrico.Parsers.Remarkup;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Phabrico.Controllers
{
    /// <summary>
    /// Represents the controller being used for the Maniphest-Task-functionality in Phabrico
    /// </summary>
    public class Maniphest : Controller
    {
        static Dictionary<int, Phabricator.Data.ManiphestPriority> _maniphestPriorities = null;
        static Dictionary<string, Phabricator.Data.ManiphestStatus> _maniphestStatuses = null;

        /// <summary>
        /// Represents a group of tasks (e.g. Task per user or per project)
        /// </summary>
        private class TaskGroup
        {
            public Phabricator.Data.PhabricatorObject TaskGroupInfo { get; set; }
            public IEnumerable<Phabricator.Data.Maniphest> Tasks { get; set; }
        }

        /// <summary>
        /// Converts a list of project tokens into a list of readable project names
        /// </summary>
        /// <param name="projectToken"></param>
        /// <returns></returns>
        private string getProjectToken(string projectToken)
        {
            if (string.IsNullOrEmpty(projectToken))
            {
                return Phabricator.Data.Project.Unknown;
            }
            else
            {
                return string.Join(",", projectToken.Split(',')
                                                    .OrderBy(token => getProjectName(token))
                                                    .ToArray());
            }
        }

        /// <summary>
        /// Stores the metadata of a maniphest task (e.g. priority, status, ...)
        /// </summary>
        /// <param name="database"></param>
        /// <param name="token"></param>
        private void CreateNewManiphestTaskMetadata(Database database, SessionManager.Token token)
        {
            Storage.Account accountStorage = new Storage.Account();
            Storage.Maniphest maniphestStorage = new Storage.Maniphest();
            Storage.Stage stageStorage = new Storage.Stage();

            string maniphestTaskToken = browser.Session.FormVariables[browser.Request.RawUrl]["token"];
            bool stagedManiphestTask = true;
            Phabricator.Data.Maniphest maniphestTask = stageStorage.Get<Phabricator.Data.Maniphest>(database, maniphestTaskToken, browser.Session.Locale);
            if (maniphestTask == null)
            {
                stagedManiphestTask = false;
                maniphestTask = maniphestStorage.Get(database, maniphestTaskToken, browser.Session.Locale);
            }

            if (maniphestTask != null)
            {
                string tokenWhoAmI = "";
                Phabricator.Data.Account existingAccount = accountStorage.Get(database, token);
                if (existingAccount != null)
                {
                    tokenWhoAmI = existingAccount.Parameters.UserToken;
                }

                foreach (string transactionType in new string[] { "project", "subscriber" })
                {
                    string newValues;
                    if (browser.Session.FormVariables[browser.Request.RawUrl].TryGetValue(transactionType, out newValues))
                    {
                        string[] newValueArray = newValues.Split(',').ToArray();

                        if (transactionType.Equals("project")) maniphestTask.Projects = "";
                        if (transactionType.Equals("subscriber")) maniphestTask.Subscribers = "";

                        for (int v = 0; v < newValueArray.Length; v++)
                        {
                            Phabricator.Data.Transaction newTransaction = new Phabricator.Data.Transaction();
                            newTransaction.Type = string.Format("{0}-{1}", transactionType, v);
                            newTransaction.NewValue = newValueArray[v];
                            newTransaction.Author = tokenWhoAmI;
                            newTransaction.DateModified = DateTimeOffset.UtcNow;
                            newTransaction.Token = maniphestTaskToken;
                            newTransaction.ID = maniphestTask.Transactions
                                                             .Select(task => Int32.Parse(task.ID) + 1)
                                                             .DefaultIfEmpty()
                                                             .Max()
                                                             .ToString();
                            newTransaction.OldValue = maniphestTask.Transactions
                                                                   .Where(tran => tran.Type.Equals(transactionType))
                                                                   .OrderBy(tran => tran.DateModified)
                                                                   .LastOrDefault()?.NewValue;
                            stageStorage.Modify(database, newTransaction, browser);

                            if (transactionType.Equals("project")) maniphestTask.Projects += "," + newTransaction.NewValue;
                            if (transactionType.Equals("subscriber")) maniphestTask.Subscribers += "," + newTransaction.NewValue;
                        }

                        if (stagedManiphestTask)
                        {
                            maniphestTask.Projects = maniphestTask.Projects.TrimStart(',');  // remove first comma (if existing)
                            maniphestTask.Subscribers = maniphestTask.Subscribers.TrimStart(',');  // remove first comma (if existing)

                            stageStorage.Modify(database, maniphestTask, browser);
                        }
                    }
                }

                foreach (string transactionType in new string[] { "owner", "priority", "status", "comment" })
                {
                    string newValue;
                    if (browser.Session.FormVariables[browser.Request.RawUrl].TryGetValue(transactionType, out newValue))
                    {
                        if (maniphestTaskToken.StartsWith("PHID-NEWTOKEN-"))
                        {
                            switch (transactionType)
                            {
                                case "owner":
                                    maniphestTask.Owner = newValue;
                                    break;

                                case "priority":
                                    maniphestTask.Priority = newValue;
                                    break;

                                case "status":
                                    maniphestTask.Status = newValue;

                                    // load all status names
                                    if (_maniphestStatuses == null)
                                    {
                                        Storage.ManiphestStatus maniphestStatusStorage = new Storage.ManiphestStatus();
                                        _maniphestStatuses = maniphestStatusStorage.Get(database, Language.NotApplicable)
                                                                                   .ToDictionary(key => key.Value, value => value);
                                    }

                                    maniphestTask.IsOpen = (_maniphestStatuses[maniphestTask.Status].Closed == false);
                                    break;

                                case "comment":
                                    // don't do anything: comments cannot be added on a new item
                                    break;

                                default:
                                    break;
                            }

                            stageStorage.Modify(database, maniphestTask, browser);
                        }
                        else
                        {
                            if (transactionType.Equals("comment") && string.IsNullOrEmpty(newValue))
                            {
                                continue;
                            }

                            Phabricator.Data.Transaction newTransaction = new Phabricator.Data.Transaction();
                            newTransaction.Author = tokenWhoAmI;
                            newTransaction.Type = transactionType;
                            newTransaction.ID = maniphestTask.Transactions
                                                                .Select(task => Int32.Parse(task.ID) + 1)
                                                                .DefaultIfEmpty(1)
                                                                .Max(id => id)
                                                                .ToString();
                            newTransaction.OldValue = maniphestTask.Transactions
                                                                   .Where(tran => tran.Type.Equals(transactionType))
                                                                   .OrderBy(tran => tran.DateModified)
                                                                   .LastOrDefault()?.NewValue;

                            newTransaction.NewValue = newValue;
                            newTransaction.DateModified = DateTimeOffset.UtcNow;
                            newTransaction.Token = maniphestTaskToken;

                            if (newTransaction.OldValue == null) newTransaction.OldValue = "";

                            if (stagedManiphestTask  ||  newTransaction.OldValue.Equals(newTransaction.NewValue) == false)
                            {
                                // add new transaction only if old value and new value are different
                                stageStorage.Modify(database, newTransaction, browser);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Stages a new maniphest task
        /// </summary>
        /// <param name="database"></param>
        /// <param name="token"></param>
        private void CreateNewManiphestTask(Storage.Database database, SessionManager.Token token)
        {
            Storage.Account accountStorage = new Storage.Account();
            Storage.Keyword keywordStorage = new Storage.Keyword();

            string tokenWhoAmI = "";
            Phabricator.Data.Account existingAccount = accountStorage.Get(database, token);
            if (existingAccount != null)
            {
                tokenWhoAmI = existingAccount.Parameters.UserToken;
            }

            Phabricator.Data.Maniphest newManiphestTask = new Phabricator.Data.Maniphest();
            newManiphestTask.Description = browser.Session.FormVariables[browser.Request.RawUrl]["textarea"];
            newManiphestTask.Name = browser.Session.FormVariables[browser.Request.RawUrl]["title"];
            newManiphestTask.Owner = browser.Session.FormVariables[browser.Request.RawUrl]["assigned"];
            newManiphestTask.Priority = browser.Session.FormVariables[browser.Request.RawUrl]["priority"];
            newManiphestTask.Projects = browser.Session.FormVariables[browser.Request.RawUrl]["tags"];
            newManiphestTask.Subscribers = browser.Session.FormVariables[browser.Request.RawUrl]["subscribers"];
            newManiphestTask.Author = tokenWhoAmI;
            newManiphestTask.IsOpen = true;
            newManiphestTask.DateModified = DateTimeOffset.UtcNow;
            newManiphestTask.Status = "open";
            newManiphestTask.Language = Language.NotApplicable;

            Stage newStage = new Stage();
            newManiphestTask.Token = newStage.Create(database, browser, newManiphestTask);
            newManiphestTask.ID = string.Format("-{0}", Int32.Parse(newManiphestTask.Token.Substring("PHID-NEWTOKEN-".Length)));
            newStage.Modify(database, newManiphestTask, browser);

            keywordStorage.AddPhabricatorObject(this, database, newManiphestTask);

            // (re)assign dependent Phabricator objects
            database.ClearAssignedTokens(newManiphestTask.Token, Language.NotApplicable);
            RemarkupParserOutput remarkupParserOutput;
            ConvertRemarkupToHTML(database, "/", newManiphestTask.Description, out remarkupParserOutput, false);
            foreach (Phabricator.Data.PhabricatorObject linkedPhabricatorObject in remarkupParserOutput.LinkedPhabricatorObjects)
            {
                database.AssignToken(newManiphestTask.Token, linkedPhabricatorObject.Token, Language.NotApplicable);
            }
        }

        /// <summary>
        /// This method is fired when the user opens the Maniphest screen (overview or a specific task)
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="viewPage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/maniphest", HtmlViewPageOptions = Http.Response.HtmlViewPage.ContentOptions.HideGlobalTreeView)]
        public void HttpGetLoadParameters(Http.Server httpServer, ref HtmlViewPage viewPage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.HideManiphest) throw new Phabrico.Exception.HttpNotFound("/maniphest");

            int firstIndex = 0;
            int previousIndex;
            int nextIndex;
            int nbrTasksToShow = 10;
            int nbrTasksShown = 0;
            bool firstTaskVisible;
            bool lastTaskVisible = true;
            string parameter = "assigned";
            string category = "";
            string filterCategory = "";
            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException("/maniphest", "session expired");

            if (parameterActions != null && parameterActions.Any())
            {
                Dictionary<string,string> actions = parameterActions.Split('&').Select(actionString => actionString.Split('=')).ToDictionary(key => key[0], value => value[1]);

                string firstIndexValue;
                if (actions.TryGetValue("after", out firstIndexValue))
                {
                    Int32.TryParse(firstIndexValue, out firstIndex);

                    parameters = parameters.Where(p => p.StartsWith("?after=") == false)
                                           .Where(p => p.StartsWith("&after=") == false)
                                           .ToArray();
                }
            }

            if (parameters.Any())
            {
                parameter = parameters.First();
            }

            if (RegexSafe.IsMatch(parameter, @"^(T-?[0-9]*|\?action=new)$", System.Text.RegularExpressions.RegexOptions.None))
            {
                // load task details
                LoadTaskParameters(httpServer, ref viewPage, parameters, parameterActions);
                return;
            }

            Storage.Account accountStorage = new Storage.Account();
            Storage.Maniphest maniphestStorage = new Storage.Maniphest();
            Storage.Project projectStorage = new Storage.Project();
            Storage.Stage stageStorage = new Storage.Stage();
            Storage.User userStorage = new Storage.User();

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                IEnumerable<Phabricator.Data.Maniphest> visibleManiphestTasks;

                // set private encryption key
                database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                // load all priority names
                if (_maniphestPriorities == null)
                {
                    Storage.ManiphestPriority maniphestPriorityStorage = new Storage.ManiphestPriority();
                    _maniphestPriorities = maniphestPriorityStorage.Get(database, Language.NotApplicable)
                                                                   .ToDictionary(key => key.Priority, value => value);
                }

                // load all status names
                if (_maniphestStatuses == null)
                {
                    Storage.ManiphestStatus maniphestStatusStorage = new Storage.ManiphestStatus();
                    _maniphestStatuses = maniphestStatusStorage.Get(database, Language.NotApplicable)
                                                               .ToDictionary(key => key.Value, value => value);
                }

                // determine the user's account record
                Phabricator.Data.Account whoAmI = accountStorage.WhoAmI(database, browser);
                if (whoAmI == null)
                {
                    // this should not happen
                    visibleManiphestTasks = new Phabricator.Data.Maniphest[0];
                }
                else
                {
                    // load and filter all maniphest tasks
                    List<Phabricator.Data.Maniphest> stagedTasks = stageStorage.Get<Phabricator.Data.Maniphest>(database, browser.Session.Locale).ToList();
                    visibleManiphestTasks = stagedTasks.Concat(maniphestStorage.Get(database, browser.Session.Locale)
                                                                               .Where(task => stagedTasks.All(stagedTask => stagedTask.Token.Equals(task.Token) == false)))
                                                                               .Where(task => httpServer.ValidUserRoles(database, browser, task))
                                                                               .ToList();

                    foreach (Phabricator.Data.Maniphest stagedTask in visibleManiphestTasks)
                    {
                        // load staged transactions (e.g. new owner, new priority, ...) into maniphestTask
                        maniphestStorage.LoadStagedTransactionsIntoManiphestTask(database, stagedTask, browser.Session.Locale);
                    }

                    switch (parameter)
                    {
                        case "assigned":
                            IEnumerable<string> visibleManiphestStates = database.GetConfigurationParameter("VisibleManiphestStates")?.Split('\t');
                            if (visibleManiphestStates == null)
                            {
                                visibleManiphestTasks = visibleManiphestTasks.Where(task => task.Owner != null
                                                                                         && task.Owner.Equals(whoAmI.Parameters.UserToken)
                                                                                         && task.IsOpen).ToList();
                            }
                            else
                            {
                                visibleManiphestTasks = visibleManiphestTasks.Where(task => task.Owner != null
                                                                                         && task.Owner.Equals(whoAmI.Parameters.UserToken)
                                                                                         && task.IsOpen
                                                                                         && visibleManiphestStates.Contains(task.Status)).ToList();
                            }
                            break;

                        case "authored":
                            visibleManiphestTasks = visibleManiphestTasks.Where(task => task.Author != null
                                                                                     && task.Author.Equals(whoAmI.Parameters.UserToken)
                                                                                     && task.IsOpen);
                            break;

                        case "subscribed":
                            visibleManiphestTasks = visibleManiphestTasks.Where(task => task.Subscribers != null
                                                                                     && task.Subscribers
                                                                                            .Split(',')
                                                                                            .Any(subscriber => subscriber.Equals(whoAmI.Parameters.UserToken))
                                                                                     && task.IsOpen);
                            break;

                        case "opentasks":
                            visibleManiphestTasks = visibleManiphestTasks.Where(task => task.IsOpen);
                            if (parameters.Length > 1)
                            {
                                category = parameters[1];
                                parameter = parameter + "-" + category;
                            }
                            if (parameters.Length > 2)
                            {
                                filterCategory = parameters[2];
                            }
                            break;

                        default:
                            break;
                    }
                }

                // load all projects again in case there's a task with an unknown project token
                if (visibleManiphestTasks.Any(task => task.Projects != null
                                                   && ProjectByToken.ContainsKey(task.Projects) == false
                                             ))
                {
                    ProjectByToken = projectStorage.Get(database, Language.NotApplicable)
                                                   .ToDictionary(key => key.Token, value => value);
                }

                // load all users again in case there's a task with an unknown author user token
                if (visibleManiphestTasks.Any(task => AccountByToken.ContainsKey(task.Author) == false))
                {
                    AccountByToken = userStorage.Get(database, Language.NotApplicable)
                                                 .ToDictionary(key => key.Token, value => value);
                }

                // group  and limit result list
                firstTaskVisible = (firstIndex <= 0);
                int nbrTasksToHide = firstIndex;
                List<Phabricator.Data.PhabricatorObject> taskGroups = new List<Phabricator.Data.PhabricatorObject>();
                switch (category)
                {
                    case "perproject":
                        List<string> projectTokens;
                        if (string.IsNullOrWhiteSpace(filterCategory))
                        {
                            projectTokens = visibleManiphestTasks.SelectMany(task => task.Projects
                                                                                         .Split(','))
                                                                                         .Distinct()
                                                                                         .ToList();
                        }
                        else
                        {
                            projectTokens = new List<string>( new string[] { filterCategory } );
                        }

                        // collect all projects for all the maniphest tasks into a TaskGroup list
                        var projectInfo = projectStorage.Get(database, Language.NotApplicable)
                                                        .Where(project => projectTokens.Contains(project.Token))
                                                        .OrderBy(project => project.Name)
                                                        .Select(project => new TaskGroup {
                                                            TaskGroupInfo = project,
                                                            Tasks = visibleManiphestTasks.Where(task => task.Projects != null
                                                                                                     && task.Projects.Contains(project.Token)
                                                                                               )
                                                        })
                                                        .ToList();

                        // check if there are any Maniphest tasks without a project assigned
                        if (projectTokens.Any(projectToken => projectToken.Any() == false))
                        {
                            // insert dummy project into TaskGroup
                            projectInfo.Insert(0, new TaskGroup
                            {
                                TaskGroupInfo = new Phabricator.Data.Project
                                {
                                    Name = Locale.TranslateText("No projects assigned", browser.Session.Locale),
                                    Token = ""
                                },
                                Tasks = visibleManiphestTasks.Where(task => task.Projects.Any() == false)
                            });
                        }

                        // calculate index for 'Previous' navigation button at the bottom of the Maniphest list
                        previousIndex = 0;
                        int nbrProjectTasksHidden = 0;
                        TaskGroup currentTaskProject = null;
                        foreach (var project in projectInfo)
                        {
                            nbrProjectTasksHidden += project.Tasks.Count();
                            if (nbrProjectTasksHidden < nbrTasksToHide)
                            {
                                currentTaskProject = project;
                                continue;
                            }

                            break;
                        }

                        if (currentTaskProject != null)
                        {
                            var previousProjects = projectInfo.TakeWhile(p => p != currentTaskProject).Reverse<TaskGroup>();
                            previousIndex = previousProjects.Sum(project => project.Tasks.Count());
                            foreach (var project in previousProjects)
                            {
                                if (firstIndex - previousIndex >= nbrTasksToShow ||
                                    nbrTasksToShow - (firstIndex - previousIndex) < project.Tasks.Count()
                                   )
                                {
                                    break;
                                }

                                previousIndex -= project.Tasks.Count();
                            }
                        }

                        // loop through all maniphest tasks per project and hide all projects for which no tasks should be shown
                        nextIndex = firstIndex;
                        foreach (var project in projectInfo.ToList())
                        {
                            if (nbrTasksToHide == 0) break;

                            int nbrProjectTasksToHide = nbrTasksToHide < project.Tasks.Count() ? nbrTasksToHide : project.Tasks.Count();

                            var projectTasks = visibleManiphestTasks.Where(task => project.TaskGroupInfo.Token.Any() 
                                                                                   && task.Projects.Contains(project.TaskGroupInfo.Token)
                                                                                   || task.Projects.Any() == false)
                                                                    .OrderByDescending(task => Int32.Parse(task.Priority))
                                                                    .ThenByDescending(task => task.DateModified);
                            if (projectTasks.Count() > nbrTasksToHide)
                            {
                                break;
                            }

                            visibleManiphestTasks = projectTasks.Skip(nbrProjectTasksToHide)
                                                            .Concat(visibleManiphestTasks.Where(task => task.Projects.Contains(project.TaskGroupInfo.Token) == false
                                                                                                     || task.Projects.Count() > 1
                                                                                               ));
                            nbrTasksToHide = nbrTasksToHide - nbrProjectTasksToHide;

                            if (nbrTasksToHide >= 0)
                            {
                                // all tasks of the current project were removed from the list => remove project also from the list
                                projectInfo.Remove(project);
                            }
                        }

                        // initialize project list
                        taskGroups.AddRange(projectInfo.Select(project => project.TaskGroupInfo));

                        // reinitialize list of tasks to show
                        visibleManiphestTasks = visibleManiphestTasks.Where(task => taskGroups.Any(taskProject => task.Projects != null
                                                                                                               && task.Projects.Contains(taskProject.Token))
                                                                                                  );

                        // remove all collected projects which have no tasks to be shown
                        taskGroups.RemoveAll(group => visibleManiphestTasks.All(task => task.Projects.Contains(group.Token) == false));

                        // overwrite firstIndex again so we won't skip tasks (we already skipped them)
                        firstIndex = 0;
                        break;

                    case "peruser":
                        List<string> userTokens;
                        if (string.IsNullOrWhiteSpace(filterCategory))
                        {
                            userTokens = visibleManiphestTasks.Select(task => task.Owner)
                                                              .Distinct()
                                                              .ToList();
                        }
                        else
                        {
                            userTokens = new List<string>( new string[] { filterCategory } );
                        }

                        var userInfo = userStorage.Get(database, Language.NotApplicable)
                                                  .Where(user => userTokens.Contains(user.Token))
                                                  .OrderBy(user => user.RealName)
                                                  .Select(user => new TaskGroup {
                                                      TaskGroupInfo = user,
                                                      Tasks = visibleManiphestTasks.Where(task => task.Owner != null
                                                                                               && task.Owner.Equals(user.Token)
                                                                                         )
                                                  })
                                                  .ToList();

                        // check if there are any Maniphest tasks without a user assigned
                        if (userTokens.Any(userToken => userToken.Any() == false))
                        {
                            // insert dummy user into TaskGroup
                            userInfo.Insert(0, new TaskGroup
                            {
                                TaskGroupInfo = new Phabricator.Data.User
                                {
                                    RealName = Locale.TranslateText("No users assigned", browser.Session.Locale),
                                    Token = ""
                                },
                                Tasks = visibleManiphestTasks.Where(task => string.IsNullOrEmpty(task.Owner) )
                            });
                        }

                        // calculate index for 'Previous' navigation button at the bottom of the Maniphest list
                        previousIndex = 0;
                        int nbrUserTasksHidden = 0;
                        TaskGroup currentTaskOwner = null;
                        foreach (var user in userInfo)
                        {
                            nbrUserTasksHidden += user.Tasks.Count();
                            if (nbrUserTasksHidden < nbrTasksToHide)
                            {
                                currentTaskOwner = user;
                                continue;
                            }

                            break;
                        }

                        if (currentTaskOwner != null)
                        {
                            var previousOwners = userInfo.TakeWhile(p => p != currentTaskOwner).Reverse<TaskGroup>();
                            previousIndex = previousOwners.Sum(user => user.Tasks.Count());
                            foreach (var owner in previousOwners)
                            {
                                if (firstIndex - previousIndex >= nbrTasksToShow ||
                                    nbrTasksToShow - (firstIndex - previousIndex) < owner.Tasks.Count()
                                   )
                                {
                                    break;
                                }

                                previousIndex -= owner.Tasks.Count();
                            }
                        }

                        // loop through all maniphest tasks per task-owner and hide all task-owners for which no tasks should be shown
                        nextIndex = firstIndex;
                        foreach (var owner in userInfo.ToList())
                        {
                            if (nbrTasksToHide == 0) break;

                            int nbrOwnerTasksToHide = nbrTasksToHide < owner.Tasks.Count() ? nbrTasksToHide : owner.Tasks.Count();

                            var ownerTasks = visibleManiphestTasks.Where(task => owner.TaskGroupInfo.Token.Any() 
                                                                                 && task.Owner.Equals(owner.TaskGroupInfo.Token)
                                                                                 || task.Owner.Any() == false)
                                                                  .OrderByDescending(task => Int32.Parse(task.Priority))
                                                                  .ThenByDescending(task => task.DateModified);
                            if (ownerTasks.Count() > nbrTasksToHide)
                            {
                                break;
                            }

                            visibleManiphestTasks = ownerTasks.Skip(nbrOwnerTasksToHide)
                                                              .Concat(visibleManiphestTasks.Where(task => task.Owner.Equals(owner.TaskGroupInfo.Token) == false));
                            nbrTasksToHide = nbrTasksToHide - nbrOwnerTasksToHide;

                            if (nbrTasksToHide >= 0)
                            {
                                // all tasks of the current task-owner were removed from the list => remove owner also from the list
                                userInfo.Remove(owner);
                            }
                        }

                        // initialize task-owner list
                        taskGroups.AddRange(userInfo.Select(user => user.TaskGroupInfo));

                        // reinitialize list of tasks to show
                        visibleManiphestTasks = visibleManiphestTasks.Where(task => taskGroups.Any(taskOwner => task.Owner != null
                                                                                                             && task.Owner.Equals(taskOwner.Token))
                                                                                                  );

                        // remove all collected owners which have no tasks to be shown
                        taskGroups.RemoveAll(group => visibleManiphestTasks.All(task => task.Owner != null
                                                                                     && task.Owner.Equals(group.Token) == false)
                                                                               );

                        // overwrite firstIndex again so we won't skip tasks (we already skipped them)
                        firstIndex = 0;
                        break;

                    default:
                        // correct firstIndex in case it is too far
                        if (firstIndex + 1 > visibleManiphestTasks.Count())
                        {
                            firstIndex = visibleManiphestTasks.Count() - nbrTasksToShow;
                        }
                        if (firstIndex < nbrTasksToShow)
                        {
                            firstIndex = 0;
                        }

                        // set index-positions for previous and next buttons
                        previousIndex = firstIndex - nbrTasksToShow;
                        nextIndex = firstIndex + nbrTasksToShow;

                        taskGroups.Add(new Phabricator.Data.PhabricatorObject());
                        lastTaskVisible = visibleManiphestTasks.OrderByDescending(task => Int32.Parse(task.Priority))
                                                               .Skip(firstIndex + nbrTasksToShow)
                                                               .Any() == false;
                        break;
                }

                if (string.IsNullOrWhiteSpace(filterCategory))
                {
                    viewPage.SetText("SHOW-TOC", "");
                }
                else
                {
                    viewPage.SetText("SHOW-TOC", "no-toc");
                }

                if (string.IsNullOrWhiteSpace(parameter.Split('?').FirstOrDefault()))
                {
                    viewPage.SetText("MANIPHESTFILTER", "assigned");
                }
                else
                {
                    viewPage.SetText("MANIPHESTFILTER", parameter);
                }

                if (visibleManiphestTasks.Any())
                {
                    viewPage.SetText("TASKS-AVAILABLE", "yes");
                }
                else
                {
                    viewPage.SetText("TASKS-AVAILABLE", "no");
                }

                // get all staged task data
                Stage.Data[] stagedData = stageStorage.Get(database, Language.NotApplicable)
                                                      .Where(data => data.TokenPrefix.Equals("PHID-TASK-")
                                                                  || data.TokenPrefix.Equals("PHID-TRAN-")
                                                            )
                                                      .ToArray();

                // load maniphest overview
                foreach (Phabricator.Data.PhabricatorObject taskGroup in taskGroups.OrderBy(group => group))
                {
                    HtmlPartialViewPage taskGroupHeader = null;

                    if (taskGroups.Count > 1)
                    {
                        lastTaskVisible = taskGroups.LastOrDefault().Equals(taskGroup);
                    }
                    
                    Phabricator.Data.Project tasksProject = taskGroup as Phabricator.Data.Project;
                    Phabricator.Data.User tasksOwner = taskGroup as Phabricator.Data.User;
                    IEnumerable<Phabricator.Data.Maniphest> filteredManiphestTasks;

                    if (tasksProject != null)
                    {
                        filteredManiphestTasks = visibleManiphestTasks.Where(task => task.Projects.Split(',').Contains(tasksProject.Token));

                        taskGroupHeader = viewPage.GetPartialView("TASK-GROUP");
                        taskGroupHeader.SetText("TASK-GROUP-NAME", getProjectName(taskGroup.Token));
                    }
                    else
                    if (tasksOwner != null)
                    {
                        filteredManiphestTasks = visibleManiphestTasks.Where(task => task.Owner.Equals(tasksOwner.Token));

                        taskGroupHeader = viewPage.GetPartialView("TASK-GROUP");
                        taskGroupHeader.SetText("TASK-GROUP-NAME", getAccountName(taskGroup.Token));
                    }
                    else
                    {
                        filteredManiphestTasks = visibleManiphestTasks;

                        taskGroupHeader = viewPage.GetPartialView("TASK-GROUP");
                        taskGroupHeader.SetText("TASK-GROUP-NAME", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    }

                    var tasksPerPriority = filteredManiphestTasks.GroupBy(task => task.Priority)
                                                                 .Select(task => new
                                                                 {
                                                                     Priority = Int32.Parse(task.Key),
                                                                     Tasks = task.GroupBy(priorityTask => priorityTask.Token)
                                                                                 .Select(priorityTask => priorityTask.OrderByDescending(t => t.DateModified)
                                                                                                                     .FirstOrDefault())
                                                                                 .OrderByDescending(priorityTask => priorityTask.DateModified)
                                                                                 .ToArray()
                                                                 });

                    int currentTaskIndex = 0;
                    foreach (var priorityTasks in tasksPerPriority.OrderByDescending(header => header.Priority))
                    {
                        if (firstIndex >= currentTaskIndex + priorityTasks.Tasks.Length)
                        {
                            currentTaskIndex += priorityTasks.Tasks.Length;
                            continue;
                        }

                        HtmlPartialViewPage taskPriorityHeader = taskGroupHeader.GetPartialView("TASK");

                        string priorityHeaderName = Miscellaneous.Locale.TranslateText( "ManiphestPriority." + _maniphestPriorities[priorityTasks.Priority].Name, browser.Session.Locale);
                        if (priorityHeaderName.StartsWith("ManiphestPriority."))
                        {
                            // no translation found -> take original text instead
                            priorityHeaderName = _maniphestPriorities[priorityTasks.Priority].Name;
                        }

                        taskPriorityHeader.SetText("PRIORITY-HEADER", priorityHeaderName);

                        for (int t = 0; t < priorityTasks.Tasks.Length; t++)
                        {
                            currentTaskIndex++;

                            if (firstIndex >= currentTaskIndex)
                            {
                                continue;
                            }

                            nbrTasksShown++;
                            if (taskGroups.Count > 1)
                            {
                                nextIndex++;
                            }

                            HtmlPartialViewPage task = taskPriorityHeader.GetPartialView("TASKDETAIL");

                            Phabricator.Data.Maniphest maniphestTask = priorityTasks.Tasks[t];

                            // if task was modified, draw a flame and take the task info from the staging area
                            string taskState;
                            bool isStaged = false;
                            if (stagedData.Any(data => data.Token.Equals(maniphestTask.Token)))
                            {
                                if (stageStorage.IsFrozen(database, browser, maniphestTask.Token))
                                {
                                    taskState = "frozen";
                                }
                                else
                                {
                                    taskState = "unfrozen";
                                }

                                isStaged = true;

                                Phabricator.Data.Maniphest stagedManiphestTask = stageStorage.Get<Phabricator.Data.Maniphest>(database, maniphestTask.Token, browser.Session.Locale);
                                if (stagedManiphestTask != null)
                                {
                                    maniphestTask = stagedManiphestTask;
                                }
                            }
                            else
                            {
                                taskState = "";
                            }

                            task.SetText("TASK-ID", maniphestTask.ID);
                            task.SetText("TASK-USER", getAccountName(maniphestTask.Owner));
                            task.SetText("TASK-USER-TOKEN", maniphestTask.Owner);
                            task.SetText("TASK-TIMESTAMP", FormatDateTimeOffset(maniphestTask.DateModified, browser.Session.Locale), HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                            task.SetText("PROJECT-TOKENS", getProjectToken(maniphestTask.Projects));
                            task.SetText("TASK-TITLE", maniphestTask.Name);
                            task.SetText("PRIORITY-COLOR", _maniphestPriorities[Int32.Parse(maniphestTask.Priority)].Color);
                            task.SetText("TASK-UNSYNCED", taskState, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                            task.SetText("PRIORITY-HEADER", _maniphestPriorities[priorityTasks.Priority].Name);
                            task.SetText("TASK-STATUS", _maniphestStatuses[maniphestTask.Status].Name);
                            task.SetText("TASK-STAGED", isStaged ? "staged" : "");

                            if (string.IsNullOrEmpty(maniphestTask.Projects))
                            {
                                task.RemovePartialView("TASKDETAIL-PROJECTS");
                            }
                            else
                            {
                                foreach (string projectToken in getProjectToken(maniphestTask.Projects).Split(','))
                                {
                                    HtmlPartialViewPage projectPartialViewPage = task.GetPartialView("TASKDETAIL-PROJECTS");

                                    string rgbColor = "rgb(0, 128, 255)";
                                    Phabricator.Data.Project project = projectStorage.Get(database, projectToken, browser.Session.Locale);
                                    if (project != null && string.IsNullOrWhiteSpace(project.Color) == false)
                                    {
                                        rgbColor = project.Color;
                                    }

                                    string style = string.Format("background: {0}; color: {1}; border-color: {1}",
                                                        rgbColor,
                                                        ColorFunctionality.WhiteOrBlackTextOnBackground(rgbColor));

                                    projectPartialViewPage.SetText("PROJECT-TOKEN", projectToken);
                                    projectPartialViewPage.SetText("PROJECT-STYLE", style);
                                    projectPartialViewPage.SetText("PROJECT-NAME", getProjectName(projectToken));
                                }
                            }

                            if (category.Equals("perproject") || category.Equals("peruser")) continue;
                            if (nbrTasksShown >= nbrTasksToShow) break;
                        }

                        if (category.Equals("perproject") || category.Equals("peruser")) continue;
                        if (nbrTasksShown >= nbrTasksToShow) break;
                    }

                    if (nbrTasksShown >= nbrTasksToShow) break;
                }
            }

            viewPage.Merge();

            // visualize navigation buttons at the bottom of the maniphest-task list
            string navigationPosition = "";
            if (firstTaskVisible) navigationPosition += "FirstTaskVisible ";
            if (lastTaskVisible) navigationPosition += "LastTaskVisible ";
            if (nextIndex < nbrTasksShown) nextIndex = nbrTasksShown;
            viewPage.SetText("NAVIGATION-POSITION", navigationPosition);
            viewPage.SetText("NAVIGATION-PREVIOUS-POSITION", previousIndex.ToString());
            viewPage.SetText("NAVIGATION-NEXT-POSITION", nextIndex.ToString());
        }

        /// <summary>
        /// Returns the number of tasks that are assigned to the WhoAmI user
        /// This number is shown in the navigator menu in the homepage
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/maniphest/count", HtmlViewPageOptions = Http.Response.HtmlViewPage.ContentOptions.HideGlobalTreeView)]
        public void HttpGetTaskCount(Http.Server httpServer, ref JsonMessage jsonMessage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.HideManiphest) throw new Phabrico.Exception.HttpNotFound("/maniphest/count");

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException("/maniphest/count", "session expired");

            Storage.Account accountStorage = new Storage.Account();
            Storage.Maniphest maniphestStorage = new Storage.Maniphest();
            Storage.Stage stageStorage = new Stage();
            IEnumerable<Phabricator.Data.Maniphest> availableManiphestTasks;

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // set private encryption key
                database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                // determine the user's account record
                Phabricator.Data.Account whoAmI = accountStorage.WhoAmI(database, browser);
                if (whoAmI == null)
                {
                    // this should not happen
                    availableManiphestTasks = new Phabricator.Data.Maniphest[0];
                }
                else
                {
                    // load and filter all maniphest tasks
                    List<Phabricator.Data.Maniphest> stagedTasks = stageStorage.Get<Phabricator.Data.Maniphest>(database, browser.Session.Locale).ToList();
                    foreach (Phabricator.Data.Maniphest stagedTask in stagedTasks)
                    {
                        // load staged transactions (e.g. new owner, new priority, ...) into maniphestTask
                        maniphestStorage.LoadStagedTransactionsIntoManiphestTask(database, stagedTask, browser.Session.Locale);
                    }

                    availableManiphestTasks = stagedTasks.Concat(maniphestStorage.Get(database, browser.Session.Locale)
                                                                                 .Where(task => stagedTasks.All(stagedTask => stagedTask.Token.Equals(task.Token) == false)))
                                                                                 .ToList();

                    IEnumerable<string> visibleManiphestStates = database.GetConfigurationParameter("VisibleManiphestStates")?.Split('\t');
                    if (visibleManiphestStates == null)
                    {
                        availableManiphestTasks = availableManiphestTasks.Where(task => task.Owner != null
                                                                                     && task.Owner.Equals(whoAmI.Parameters.UserToken)
                                                                                     && task.IsOpen);
                    }
                    else
                    {
                        availableManiphestTasks = availableManiphestTasks.Where(task => task.Owner != null
                                                                                     && task.Owner.Equals(whoAmI.Parameters.UserToken)
                                                                                     && task.IsOpen
                                                                                     && visibleManiphestStates.Contains(task.Status));
                    }

                    availableManiphestTasks = availableManiphestTasks.Where(task => httpServer.ValidUserRoles(database, browser, task))
                                                                     .ToList();
                }

                // return number of open tasks
                string jsonData = JsonConvert.SerializeObject(new
                {
                    Count = availableManiphestTasks.Count()
                });

                jsonMessage = new JsonMessage(jsonData);
            }
        }

        /// <summary>
        /// This method is fired when the user modifies a Maniphest task (conten and/or metadata)
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/maniphest")]
        public HttpMessage HttpPostSave(Http.Server httpServer, string[] parameters)
        {
            if (browser.InvalidCSRF(browser.Request.RawUrl)) throw new Phabrico.Exception.InvalidCSRFException();
            if (httpServer.Customization.HideManiphest) throw new Phabrico.Exception.HttpNotFound("/maniphest");

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // set private encryption key
                database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                // invalidate cache for current URL
                string url = browser.Request.RawUrl.Split('?')[0].TrimEnd('/');
                httpServer.InvalidateNonStaticCache(EncryptionKey, url);

                // start performing action
                string action = parameters.FirstOrDefault(parameter => parameter.StartsWith("?action="));
                if (action == null)
                {
                    action = "";
                }
                else
                {
                    action = action.Substring("?action=".Length);
                }

                if (action.Equals("cancel"))
                {
                    Storage.Stage stageStorage = new Storage.Stage();
                    Storage.Maniphest maniphestStorage = new Storage.Maniphest();

                    string maniphestTaskToken = browser.Session.FormVariables[browser.Request.RawUrl]["token"];
                    Phabricator.Data.Maniphest originalManiphestTask = stageStorage.Get<Phabricator.Data.Maniphest>(database, maniphestTaskToken, browser.Session.Locale);
                    if (originalManiphestTask == null)
                    {
                        originalManiphestTask = maniphestStorage.Get(database, maniphestTaskToken, browser.Session.Locale);
                    }

                    List<int> referencedFileIDs = browser.Session.FormVariables[browser.Request.RawUrl]["referencedFiles"]
                                                                 .Split(',')
                                                                 .Where(fileID => string.IsNullOrEmpty(fileID) == false)
                                                                 .Select(fileID => Int32.Parse(fileID))
                                                                 .ToList();

                    if (originalManiphestTask != null)
                    {
                        // analyze original task description and remove all the file-references from
                        // the current task description that were also found in the original one
                        Regex matchFileAttachments = new Regex("{F(-?[0-9]+)[^}]*}");
                        foreach (Match match in matchFileAttachments.Matches(originalManiphestTask.Description).OfType<Match>().ToArray())
                        {
                            int fileID;

                            if (Int32.TryParse(match.Groups[1].Value, out fileID))
                            {
                                referencedFileIDs.Remove(fileID);
                            }
                        }
                    }

                    Phabricator.Data.File[] stagedFiles = stageStorage.Get<Phabricator.Data.File>(database, browser.Session.Locale).ToArray();

                    foreach (int unreferencedFileID in referencedFileIDs)
                    {
                        Phabricator.Data.File unreferencedFile = stagedFiles.FirstOrDefault(stagedFile => stagedFile.ID == unreferencedFileID);
                        if (unreferencedFile != null)
                        {
                            stageStorage.Remove(database, browser, unreferencedFile);
                        }
                    }

                    return null;
                }
                else
                {
                    switch (browser.Session.FormVariables[browser.Request.RawUrl]["operation"])
                    {
                        case "new":
                            CreateNewManiphestTask(database, token);
                            return new Http.Response.HttpRedirect(httpServer, browser, "maniphest/authored");

                        case "comment":
                            CreateNewManiphestTaskMetadata(database, token);
                            string maniphestRootPath = Http.Server.RootPath + url.Substring(0, url.Length - parameters[0].Length).Trim('/');
                            httpServer.InvalidateNonStaticCache(EncryptionKey, maniphestRootPath);  // clear also cache of maniphest home page to refresh the staged icon of this task
                            return new Http.Response.HttpRedirect(httpServer, browser, "maniphest/" + parameters[0] + "/");

                        case "edit":
                            ModifyExistingManiphestTask(database, token);
                            return null;

                        default:
                            throw new Phabrico.Exception.AccessDeniedException("/maniphest", "invalid url");
                    }
                }
            }
        }

        /// <summary>
        /// Show the ViewPage for a given Maniphest task
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="viewPage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        private void LoadTaskParameters(Http.Server httpServer, ref HtmlViewPage viewPage, string[] parameters, string parameterActions)
        {
            string operation = "";
            RemarkupParserOutput remarkupParserOutput;
            Storage.ManiphestPriority maniphestPriorityStorage = new Storage.ManiphestPriority();
            Storage.Account accountStorage = new Storage.Account();

            viewPage = new Http.Response.HtmlViewPage(httpServer, browser, true, "ManiphestTask", parameters);

            if (parameterActions != null &&
                parameterActions.StartsWith("action="))
            {
                switch (parameterActions.Substring("action=".Length).ToLower())
                {
                    case "edit":
                        viewPage = new Http.Response.HtmlViewPage(httpServer, browser, true, "ManiphestTaskEdit", parameters);
                        operation = "edit";
                        break;

                    case "new":
                        viewPage = new Http.Response.HtmlViewPage(httpServer, browser, true, "ManiphestTaskEdit", parameters);
                        operation = "new";
                        break;

                    case "cancel":
                    case "save":
                    case "":
                        operation = "";
                        break;

                    default:
                        string invalidUrl = "/maniphest" + string.Join("/", parameters);
                        invalidUrl = invalidUrl.Replace("//", "/").Replace("/?", "?");
                        throw new Phabrico.Exception.AccessDeniedException(invalidUrl, "invalid URL");
                }
            }

            viewPage.SetText("OPERATION", operation);

            // add Diagram icon to toolbar if DiagramsNet plugin is installed
            if (Http.Server.Plugins.Any(plugin => plugin.GetType().FullName.Equals("Phabrico.Plugin.DiagramsNet")))
            {
                viewPage.SetText("PLUGIN-DIAGRAM-AVAILABLE", "yes", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
            }
            else
            {
                viewPage.SetText("PLUGIN-DIAGRAM-AVAILABLE", "no", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
            }

            if (operation.Equals("new"))
            {
                viewPage.SetText("TASK-ID", "maniphestTask.ID");
                viewPage.SetText("TASK-HEADER", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                viewPage.SetText("TASK-RAW-DESCRIPTION", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                viewPage.SetText("TASK-DESCRIPTION", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                viewPage.SetText("TASK-TOKEN", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                viewPage.SetText("TASK-ASSIGNED-TOKEN", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                viewPage.SetText("TASK-TAGS", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                viewPage.SetText("TASK-SUBSCRIBERS", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);

                // complete Priority combobox
                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    // set private encryption key
                    database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                    foreach (Phabricator.Data.ManiphestPriority maniphestPriority in maniphestPriorityStorage.Get(database, Language.NotApplicable).OrderByDescending(priority => priority.Priority))
                    {
                        HtmlPartialViewPage priorityData = viewPage.GetPartialView("TASK-PRIORITIES");
                        if (priorityData != null)
                        {
                            priorityData.SetText("TASK-PRIORITIES-PRIORITY-TOKEN", maniphestPriority.Priority.ToString());

                            string priorityName = Miscellaneous.Locale.TranslateText( "ManiphestPriority." + maniphestPriority.Name, browser.Session.Locale);
                            if (priorityName.StartsWith("ManiphestPriority."))
                            {
                                // no translation found -> take original text instead
                                priorityName = maniphestPriority.Name;
                            }

                            priorityData.SetText("TASK-PRIORITIES-PRIORITY-NAME", priorityName);
                            priorityData.SetText("TASK-PRIORITIES-PRIORITY-SELECTED", "");
                        }
                    }
                }
            }
            else
            {
                Storage.Maniphest maniphest = new Storage.Maniphest();
                Storage.Project projectStorage = new Storage.Project();
                Storage.User userStorage = new Storage.User();
                Storage.ManiphestStatus storageManiphestStatus = new Storage.ManiphestStatus();

                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    // set private encryption key
                    database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                    // load maniphest task
                    Phabricator.Data.Maniphest maniphestTask = null;
                    string taskId = parameters[0].TrimStart('T').Split('?').FirstOrDefault();
                    if (string.IsNullOrEmpty(taskId))
                    {
                        // newly created task -> take last staged maniphest task
                        Storage.Stage stage = new Storage.Stage();
                        maniphestTask = stage.Get<Phabricator.Data.Maniphest>(database, browser.Session.Locale).OrderByDescending(s => s.DateModified).FirstOrDefault();
                    }
                    else
                    {
                        Storage.Stage stageStorage = new Storage.Stage();
                        maniphestTask = stageStorage.Get<Phabricator.Data.Maniphest>(database, browser.Session.Locale)
                                                    .FirstOrDefault(task => task.ID.Equals(taskId));

                        if (maniphestTask == null)
                        {
                            // no staged maniphest task found -> search for a downloaded maniphest task
                            maniphestTask = maniphest.Get(database, taskId, browser.Session.Locale);
                        }

                        if (maniphestTask != null)
                        {
                            // load staged transactions (e.g. new owner, new priority, ...) into maniphestTask
                            Storage.Maniphest maniphestStorage = new Storage.Maniphest();
                            maniphestStorage.LoadStagedTransactionsIntoManiphestTask(database, maniphestTask, browser.Session.Locale);
                        }
                    }

                    if (maniphestTask != null)
                    {
                        // if task was modified, draw a flame and take the task info from the staging area
                        string taskState;
                        Storage.Stage stageStorage = new Storage.Stage();
                        bool isStaged = stageStorage.Get(database, Language.NotApplicable)
                                                    .Any(data => data.Token.Equals(maniphestTask.Token));

                        if (isStaged)
                        {
                            if (stageStorage.IsFrozen(database, browser, maniphestTask.Token))
                            {
                                taskState = "frozen";
                            }
                            else
                            {
                                taskState = "unfrozen";
                            }
                        }
                        else
                        {
                            taskState = "";
                        }

                        Phabricator.Data.ManiphestPriority maniphestTaskPriority = maniphestPriorityStorage.Get(database, maniphestTask.Priority, browser.Session.Locale);
                        if (maniphestTaskPriority != null)
                        {
                            string statusText = "";
                            Phabricator.Data.ManiphestStatus maniphestTaskStatus = storageManiphestStatus.Get(database, maniphestTask.Status, browser.Session.Locale);
                            if (maniphestTaskStatus != null)
                            {
                                string translatedStatusText = Locale.TranslateText("ManiphestStatus." + maniphestTaskStatus.Name, browser.Session.Locale);
                                if (translatedStatusText.StartsWith("ManiphestStatus."))
                                {
                                    // no translation found -> take original text instead
                                    translatedStatusText = maniphestTaskStatus.Name;
                                }

                                statusText += translatedStatusText + ", ";
                            }

                            string translatedPriorityText = Locale.TranslateText("ManiphestPriority." + maniphestTaskPriority.Name, browser.Session.Locale);
                            if (translatedPriorityText.StartsWith("ManiphestPriority."))
                            {
                                // no translation found -> take original text instead
                                translatedPriorityText = maniphestTaskPriority.Name;
                            }
                            statusText += translatedPriorityText;

                            string statusIcon;
                            if (maniphestTask.IsOpen)
                            {
                                statusIcon = "fa-square-o";
                            }
                            else
                            {
                                statusIcon = "fa-check-square-o";
                            }

                            string stagedComment = "";
                            Phabricator.Data.Transaction stagedCommentTransaction = stageStorage.Get<Phabricator.Data.Transaction>(database, maniphestTask.Token, "comment");
                            if (stagedCommentTransaction != null)
                            {
                                stagedComment = stagedCommentTransaction.NewValue;
                            }

                            // collect linked projects
                            string projectTokens = "";
                            List<Phabricator.Data.Project> projects = maniphestTask.Projects
                                                                                   .Split(',')
                                                                                   .Where(token => string.IsNullOrEmpty(token) == false)
                                                                                   .Select(token => projectStorage.Get(database, token, browser.Session.Locale))
                                                                                   .Where(p => p != null)
                                                                                   .OrderBy(p => p.Name)
                                                                                   .ToList();

                            // collect linked subscribers
                            var subscribers = maniphestTask.Subscribers
                                                           .Split(',')
                                                           .Where(token => string.IsNullOrEmpty(token) == false)
                                                           .Select(token => userStorage.Get(database, token, browser.Session.Locale))
                                                           .Where(s => s != null)
                                                           .Select(s => new { Token = s.Token, Name = s.RealName, Icon = "fa-user" })
                                                           .ToList();
                            subscribers.AddRange(maniphestTask.Subscribers
                                                               .Split(',')
                                                               .Where(token => string.IsNullOrEmpty(token) == false)
                                                               .Select(token => projectStorage.Get(database, token, browser.Session.Locale))
                                                               .Where(s => s != null)
                                                               .Select(s => new { Token = s.Token, Name = s.Name, Icon = "fa-briefcase" })
                                                               .ToList());

                            viewPage.SetText("TASK-ID", maniphestTask.ID);
                            viewPage.SetText("TASK-HEADER", maniphestTask.Name);
                            viewPage.SetText("TASK-STATUS-DESCRIPTION", statusText);
                            viewPage.SetText("TASK-STATUS-ICON", statusIcon);
                            viewPage.SetText("TASK-ASSIGNED-TOKEN", maniphestTask.Owner);
                            viewPage.SetText("TASK-ASSIGNED-NAME", getAccountName(maniphestTask.Owner));
                            viewPage.SetText("TASK-RAW-DESCRIPTION", maniphestTask.Description, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                            viewPage.SetText("TASK-DESCRIPTION", ConvertRemarkupToHTML(database, "maniphest/" + maniphestTask.ID + "/", maniphestTask.Description, out remarkupParserOutput, true), HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                            viewPage.SetText("TASK-AUTHOR-NAME", getAccountName(maniphestTask.Author));
                            viewPage.SetText("TASK-AUTHOR-TOKEN", maniphestTask.Author);
                            viewPage.SetText("TASK-DATE", FormatDateTimeOffset(maniphestTask.DateModified, browser.Session.Locale), HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                            viewPage.SetText("TASK-PRIORITY-COLOR", maniphestTaskPriority.Color);
                            viewPage.SetText("TASK-PRIORITY-DESCRIPTION", translatedPriorityText);
                            viewPage.SetText("TASK-UNSYNCED", taskState, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                            viewPage.SetText("TASK-TOKEN", maniphestTask.Token);
                            viewPage.SetText("TASK-TAGS", maniphestTask.Projects);
                            viewPage.SetText("TASK-TAGS-JSON",
                                             JsonConvert.SerializeObject(projects.Select(project => new
                                             {
                                                 Name = project.Name,
                                                 Token = project.Token
                                             })),
                                             HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                            viewPage.SetText("TASK-SUBSCRIBERS", maniphestTask.Subscribers);
                            viewPage.SetText("TASK-SUBSCRIBERS-JSON",
                                             JsonConvert.SerializeObject(subscribers),
                                             HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                            viewPage.SetText("TASK-STAGED-COMMENT", stagedComment, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                            if (maniphestTask.Token.StartsWith("PHID-NEWTOKEN-"))
                            {
                                viewPage.SetText("TASK-NEW-TOKEN", "maniphest-new-task");
                            }
                            else
                            {
                                viewPage.SetText("TASK-NEW-TOKEN", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                            }

                            if (Int32.Parse(maniphestTask.ID) < 0)
                            {
                                viewPage.SetText("TASK-CONFIRMATION-UNDO-LOCAL-CHANGES", "Are you sure you want to delete this task ?");
                            }
                            else
                            {
                                viewPage.SetText("TASK-CONFIRMATION-UNDO-LOCAL-CHANGES", "Are you sure you want to undo all your local changes for this task ?");
                            }

                            Phabricator.Data.Account currentAccount = accountStorage.WhoAmI(database, browser);
                            if (currentAccount == null) throw new Phabrico.Exception.InvalidWhoAmIException();

                            viewPage.SetText("PHABRICATOR-URL", currentAccount.PhabricatorUrl.TrimEnd('/') + "/", HtmlViewPage.ArgumentOptions.NoHtmlEncoding);

                            string whoAmiToken = currentAccount.Parameters.UserToken;
                            if (string.IsNullOrWhiteSpace(whoAmiToken))
                            {
                                viewPage.SetText("COMMENT-AUTHOR", "I");
                            }
                            else
                            {
                                Phabricator.Data.User whoAmI = userStorage.Get(database,
                                                                               accountStorage.WhoAmI(database, browser).Parameters.UserToken,
                                                                               browser.Session.Locale
                                                                              );
                                viewPage.SetText("COMMENT-AUTHOR", whoAmI.RealName);
                            }

                            foreach (Plugin.PluginBase plugin in Server.Plugins)
                            {
                                Plugin.PluginTypeAttribute pluginType = plugin.GetType()
                                                                              .GetCustomAttributes(typeof(Plugin.PluginTypeAttribute), true)
                                                                              .OfType<Plugin.PluginTypeAttribute>()
                                                                              .FirstOrDefault(pluginTypeAttribute => pluginTypeAttribute.Usage == Plugin.PluginTypeAttribute.UsageType.ManiphestTask);
                                if (pluginType == null) continue;

                                if (plugin.IsVisibleInApplication(database, browser, maniphestTask.Token)
                                    && (httpServer.Customization.HidePlugins.ContainsKey(plugin.GetType().Name) == false
                                        || httpServer.Customization.HidePlugins[plugin.GetType().Name] == false
                                        )
                                   )
                                {
                                    HtmlPartialViewPage maniphestTaskPluginData = viewPage.GetPartialView("MANIPHEST-TASK-PLUGINS");
                                    if (maniphestTaskPluginData == null) break;  // we're in edit-mode, no need for plugins

                                    maniphestTaskPluginData.SetText("MANIPHEST-TASK-PLUGIN-URL", plugin.URL, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                    maniphestTaskPluginData.SetText("MANIPHEST-TASK-PLUGIN-ICON", plugin.Icon, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                    maniphestTaskPluginData.SetText("MANIPHEST-TASK-PLUGIN-NAME", plugin.GetName(browser.Session.Locale));
                                    maniphestTaskPluginData.SetText("MANIPHEST-TASK-PLUGIN-KEYBOARD-SHORTCUT", pluginType.KeyboardShortcut);
                                }

                                foreach (Plugin.PluginWithoutConfigurationBase pluginExtension in plugin.Extensions
                                                                                                        .Where(ext => ext.IsVisibleInApplication(database, browser, maniphestTask.Token)
                                                                                                                   && (httpServer.Customization.HidePlugins.ContainsKey(ext.GetType().Name) == false
                                                                                                                       || httpServer.Customization.HidePlugins[ext.GetType().Name] == false
                                                                                                                      )
                                                                                                              )

                                        )
                                {
                                    if (pluginExtension.State == Plugin.PluginBase.PluginState.Loaded)
                                    {
                                        pluginExtension.Database = new Storage.Database(database.EncryptionKey);
                                        pluginExtension.Initialize();
                                        pluginExtension.State = Plugin.PluginBase.PluginState.Initialized;
                                    }

                                    HtmlPartialViewPage htmlPluginNavigatorMenuItem = viewPage.GetPartialView("MANIPHEST-TASK-PLUGINS");
                                    if (htmlPluginNavigatorMenuItem != null)
                                    {
                                        htmlPluginNavigatorMenuItem.SetText("MANIPHEST-PLUGIN-URL", pluginExtension.URL, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                        htmlPluginNavigatorMenuItem.SetText("MANIPHEST-PLUGIN-ICON", pluginExtension.Icon, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                        htmlPluginNavigatorMenuItem.SetText("MANIPHEST-PLUGIN-NAME", pluginExtension.GetName(browser.Session.Locale), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                    }
                                }
                            }

                            foreach (Phabricator.Data.Project project in projects)
                            {
                                HtmlPartialViewPage projectData = viewPage.GetPartialView("PROJECTS");
                                if (projectData != null)
                                {
                                    string rgbColor = "rgb(0, 128, 255)";
                                    if (project != null && string.IsNullOrWhiteSpace(project.Color) == false)
                                    {
                                        rgbColor = project.Color;
                                    }

                                    string style = string.Format("background: {0}; color: {1}; border-color: {1}",
                                                        rgbColor,
                                                        ColorFunctionality.WhiteOrBlackTextOnBackground(rgbColor));

                                    projectData.SetText("TASK-PROJECT-TOKEN", project.Token);
                                    projectData.SetText("TASK-PROJECT-STYLE", style);
                                    projectData.SetText("TASK-PROJECT-NAME", project.Name);
                                }

                                projectTokens += "," + project.Token;
                            }

                            if (projects.Any() == false)
                            {
                                HtmlPartialViewPage projectData = viewPage.GetPartialView("PROJECTS");
                                if (projectData != null)
                                {
                                    projectData.Content = Locale.TranslateText("No projects assigned", browser.Session.Locale);
                                }
                            }

                            foreach (var subscriber in subscribers.OrderBy(s => s.Name))
                            {
                                HtmlPartialViewPage subscriberData = viewPage.GetPartialView("SUBSCRIBERS");
                                if (subscriberData == null) break;

                                string tokenType = subscriber.Token.Split('-')[1].ToLower();

                                subscriberData.SetText("TASK-SUBSCRIBER-TOKEN-TYPE", tokenType);
                                subscriberData.SetText("TASK-SUBSCRIBER-TOKEN", subscriber.Token);
                                subscriberData.SetText("TASK-SUBSCRIBER-NAME", subscriber.Name);

                                if (tokenType.Equals("proj"))
                                {
                                    string rgbColor = "rgb(0, 128, 255)";
                                    Phabricator.Data.Project project = projectStorage.Get(database, subscriber.Token, browser.Session.Locale);
                                    if (project != null && string.IsNullOrWhiteSpace(project.Color) == false)
                                    {
                                        rgbColor = project.Color;
                                    }

                                    string style = string.Format("background: {0}; color: {1}; border-color: {1}",
                                                        rgbColor,
                                                        ColorFunctionality.WhiteOrBlackTextOnBackground(rgbColor));

                                    subscriberData.SetText("TASK-SUBSCRIBER-STYLE", style);
                                }
                            }

                            if (subscribers.Any() == false)
                            {
                                HtmlPartialViewPage subscriberData = viewPage.GetPartialView("SUBSCRIBERS");
                                if (subscriberData != null)
                                {
                                    subscriberData.SetText("TASK-SUBSCRIBER-TOKEN-TYPE", "");
                                    subscriberData.SetText("TASK-SUBSCRIBER-TOKEN", "");
                                    subscriberData.SetText("TASK-SUBSCRIBER-NAME", "");
                                }
                            }

                            Storage.Transaction transactionStorage = new Storage.Transaction();
                            maniphestTask.Transactions = transactionStorage.GetAll(database, maniphestTask.Token, browser.Session.Locale);

                            string[] validTransactionTypes = new string[] { "owner", "priority", "status", "comment", "subscriber-0", "project-0" };
                            foreach (var maniphestTransaction in maniphestTask.Transactions)
                            {
                                if (validTransactionTypes.Contains(maniphestTransaction.Type) == false)
                                {
                                    continue;
                                }

                                HtmlPartialViewPage transactionData = viewPage.GetPartialView("TRANSACTIONS");
                                if (transactionData == null) break;
                                transactionData.SetText("TASK-TRANSACTION-TYPE", maniphestTransaction.Type);
                                transactionData.SetText("TASK-TRANSACTION-DATE", FormatDateTimeOffset(maniphestTransaction.DateModified, browser.Session.Locale), HtmlViewPage.ArgumentOptions.NoHtmlEncoding);

                                switch (maniphestTransaction.Type)
                                {
                                    case "owner":
                                        SetTransactionDataOwner(transactionData, maniphestTransaction, maniphestTask);
                                        break;

                                    case "comment":
                                        SetTransactionDataComment(transactionData, maniphestTransaction, maniphestTask.ID, database);
                                        break;

                                    case "status":
                                        SetTransactionDataStatus(transactionData, maniphestTransaction, maniphestTask, database, storageManiphestStatus);
                                        break;

                                    case "priority":
                                        SetTransactionDataPriority(transactionData, maniphestTransaction, maniphestTask, database, maniphestPriorityStorage);
                                        break;

                                    case "project-0":
                                        SetTransactionDataProject(transactionData, maniphestTransaction, maniphestTask, database, projectStorage);
                                        break;

                                    case "subscriber-0":
                                        if (SetTransactionDataSubscriber(transactionData, maniphestTransaction, maniphestTask, database, projectStorage, userStorage) == false)
                                        {
                                            continue;
                                        }
                                        break;

                                    default:
                                        // don't do anything
                                        continue;
                                }
                            }

                            if (maniphestTask.Transactions.Any() == false)
                            {
                                viewPage.RemovePartialView("TRANSACTIONS");
                            }
                        }


                        foreach (Phabricator.Data.ManiphestStatus maniphestStatus in storageManiphestStatus.Get(database, Language.NotApplicable).OrderBy(status => status.Name))
                        {
                            HtmlPartialViewPage priorityData = viewPage.GetPartialView("TASK-STATUSES");
                            if (priorityData == null) break;

                            string translatedStatusText = Locale.TranslateText("ManiphestStatus." + maniphestStatus.Name, browser.Session.Locale);
                            if (translatedStatusText.StartsWith("ManiphestStatus."))
                            {
                                // no translation found -> take original text instead
                                translatedStatusText = maniphestStatus.Name;
                            }

                            priorityData.SetText("TASK-STATUSES-STATUS-TOKEN", maniphestStatus.Value.ToString());
                            priorityData.SetText("TASK-STATUSES-STATUS-NAME", translatedStatusText);
                            if (maniphestTask.Status.Equals(maniphestStatus.Value.ToString()))
                            {
                                priorityData.SetText("TASK-STATUSES-STATUS-SELECTED", "selected");
                            }
                            else
                            {
                                priorityData.SetText("TASK-STATUSES-STATUS-SELECTED", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                            }
                        }


                        foreach (Phabricator.Data.ManiphestPriority maniphestPriority in maniphestPriorityStorage.Get(database, Language.NotApplicable).OrderBy(priority => priority.Priority))
                        {
                            HtmlPartialViewPage priorityData = viewPage.GetPartialView("TASK-PRIORITIES");
                            if (priorityData == null) break;

                            string translatedPriorityText = Locale.TranslateText("ManiphestPriority." + maniphestPriority.Name, browser.Session.Locale);
                            if (translatedPriorityText.StartsWith("ManiphestPriority."))
                            {
                                // no translation found -> take original text instead
                                translatedPriorityText = maniphestPriority.Name;
                            }

                            priorityData.SetText("TASK-PRIORITIES-PRIORITY-TOKEN", maniphestPriority.Priority.ToString());
                            priorityData.SetText("TASK-PRIORITIES-PRIORITY-NAME", translatedPriorityText);
                            if (maniphestTask.Priority.Equals(maniphestPriority.Priority.ToString()))
                            {
                                priorityData.SetText("TASK-PRIORITIES-PRIORITY-SELECTED", "selected");
                            }
                            else
                            {
                                priorityData.SetText("TASK-PRIORITIES-PRIORITY-SELECTED", "");
                            }
                        }
                    }
                    else
                    {
                        viewPage = null;
                    }
                }
            }

            if (viewPage != null)
            {
                viewPage.Merge();
            }
        }

        /// <summary>
        /// Stages some modification of a Maniphest task, which also exists in Phabricator
        /// </summary>
        /// <param name="database"></param>
        /// <param name="token"></param>
        private void ModifyExistingManiphestTask(Database database, SessionManager.Token token)
        {
            Storage.Account accountStorage = new Storage.Account();
            Storage.Maniphest maniphestStorage = new Storage.Maniphest();
            Storage.Keyword keywordStorage = new Storage.Keyword();

            string maniphestTaskToken = browser.Session.FormVariables[browser.Request.RawUrl]["token"];
			if (maniphestTaskToken is null) throw new Phabrico.Exception.AccessDeniedException("/maniphest", "Invalid token");
            Phabricator.Data.Maniphest originalManiphestTask = maniphestStorage.Get(database, maniphestTaskToken, browser.Session.Locale);
            if (originalManiphestTask == null)
            {
                Storage.Stage stage = new Storage.Stage();
                originalManiphestTask = stage.Get<Phabricator.Data.Maniphest>(database, maniphestTaskToken, browser.Session.Locale);
            }

            if (originalManiphestTask != null)
            {
                string tokenWhoAmI = "";
                Phabricator.Data.Account existingAccount = accountStorage.Get(database, token);
                if (existingAccount != null)
                {
                    tokenWhoAmI = existingAccount.Parameters.UserToken;
                }


                Phabricator.Data.Maniphest modifiedManiphestTask = new Phabricator.Data.Maniphest(originalManiphestTask);
                modifiedManiphestTask.Name = browser.Session.FormVariables[browser.Request.RawUrl]["title"];
                modifiedManiphestTask.Description = browser.Session.FormVariables[browser.Request.RawUrl]["textarea"];
                modifiedManiphestTask.Owner = browser.Session.FormVariables[browser.Request.RawUrl]["assigned"];
                modifiedManiphestTask.Author = tokenWhoAmI;
                modifiedManiphestTask.Priority = browser.Session.FormVariables[browser.Request.RawUrl]["priority"];
                modifiedManiphestTask.Projects = browser.Session.FormVariables[browser.Request.RawUrl]["tags"];
                modifiedManiphestTask.Subscribers = browser.Session.FormVariables[browser.Request.RawUrl]["subscribers"];
                modifiedManiphestTask.DateModified = DateTimeOffset.UtcNow;

                Stage stageStorage = new Stage();
                stageStorage.Modify(database, modifiedManiphestTask, browser);

                bool doFreezeReferencedFiles = stageStorage.Get(database, browser.Session.Locale)
                                                           .FirstOrDefault(stagedObject => stagedObject.Token.Equals(modifiedManiphestTask.Token))
                                                           .Frozen;

                keywordStorage.DeletePhabricatorObject(database, originalManiphestTask);
                keywordStorage.AddPhabricatorObject(this, database, modifiedManiphestTask);

                // (re)assign dependent Phabricator objects
                List<Phabricator.Data.PhabricatorObject> referencedObjects = database.GetReferencedObjects(modifiedManiphestTask.Token, browser.Session.Locale).ToList();
                database.ClearAssignedTokens(modifiedManiphestTask.Token, Language.NotApplicable);
                RemarkupParserOutput remarkupParserOutput;
                List<Phabricator.Data.PhabricatorObject> linkedPhabricatorObjects;
                ConvertRemarkupToHTML(database, "/", modifiedManiphestTask.Description, out remarkupParserOutput, false);
                linkedPhabricatorObjects = remarkupParserOutput.LinkedPhabricatorObjects;
                ConvertRemarkupToHTML(database, "/", originalManiphestTask.Description, out remarkupParserOutput, false);  // remember also references in original content, so we can always undo our modifications
                linkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                foreach (Phabricator.Data.PhabricatorObject linkedPhabricatorObject in linkedPhabricatorObjects)
                {
                    database.AssignToken(modifiedManiphestTask.Token, linkedPhabricatorObject.Token, Language.NotApplicable);

                    Phabricator.Data.File linkedFile = linkedPhabricatorObject as Phabricator.Data.File;
                    if (linkedFile != null && linkedFile.ID < 0)  // linkedFile.ID < 0: file is staged
                    {
                        stageStorage.Freeze(database, browser, linkedFile.Token, doFreezeReferencedFiles);
                    }

                    referencedObjects.RemoveAll(obj => obj.Token.Equals(linkedPhabricatorObject.Token));
                }

                // delete all unreferenced Phabricator objects from staging area (if existant)
                foreach (Phabricator.Data.PhabricatorObject oldReferencedObject in referencedObjects)
                {
                    if (database.GetReferencedObjects(oldReferencedObject.Token, browser.Session.Locale).Any() == false)
                    {
                        stageStorage.Remove(database, browser, oldReferencedObject);
                    }
                }
            }
        }

        /// <summary>
        /// Shows the Comment data for a given Maniphest task
        /// </summary>
        /// <param name="transactionData"></param>
        /// <param name="maniphestTransaction"></param>
        /// <param name="maniphestTaskID"></param>
        private void SetTransactionDataComment(HtmlPartialViewPage transactionData, Phabricator.Data.Transaction maniphestTransaction, string maniphestTaskID, Database database)
        {
            RemarkupParserOutput remarkupParserOutput;

            transactionData.SetText("TASK-TRANSACTION-TEXT",
                Locale.TranslateText("@@COMMENT-AUTHOR@@ added a comment.", browser.Session.Locale)
                      .Replace("@@COMMENT-AUTHOR@@", getAccountName(maniphestTransaction.Author)));
            transactionData.SetText("TASK-TRANSACTION-DETAIL",
                    ConvertRemarkupToHTML(database, "maniphest/T" + maniphestTaskID, maniphestTransaction.NewValue, out remarkupParserOutput, false),
                    HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
        }

        /// <summary>
        /// Shows the Owner for a given Maniphest task
        /// </summary>
        /// <param name="transactionData"></param>
        /// <param name="maniphestTransaction"></param>
        private void SetTransactionDataOwner(HtmlPartialViewPage transactionData, Phabricator.Data.Transaction maniphestTransaction, Phabricator.Data.Maniphest maniphestTask)
        {
            if (string.IsNullOrEmpty(maniphestTransaction.OldValue) || maniphestTask.Token.StartsWith("PHID-NEWTOKEN-"))
            {
                transactionData.SetText("TASK-TRANSACTION-TEXT",
                    Locale.TranslateText("@@PERSON1@@ assigned the task to @@PERSON2@@", browser.Session.Locale)
                            .Replace("@@PERSON1@@", string.Format("<a href='user/info/{0}'>{1}</a>",
                                                                    maniphestTransaction.Author,
                                                                    getAccountName(maniphestTransaction.Author)
                                                                 )
                                    )
                            .Replace("@@PERSON2@@", string.Format("<a href='user/info/{0}'>{1}</a>",
                                                                    maniphestTransaction.NewValue,
                                                                    getAccountName(maniphestTransaction.NewValue)
                                                                 )
                                    ),
                    HtmlViewPage.ArgumentOptions.NoHtmlEncoding
                );

                transactionData.SetText("TASK-TRANSACTION-DETAIL", "", HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
            }
            else
            {
                transactionData.SetText("TASK-TRANSACTION-TEXT",
                    Locale.TranslateText("@@PERSON1@@ reassigned the task from @@PERSON2@@ to @@PERSON3@@", browser.Session.Locale)
                          .Replace("@@PERSON1@@", string.Format("<a href='user/info/{0}'>{1}</a>",
                                                                    maniphestTransaction.Author,
                                                                    getAccountName(maniphestTransaction.Author)
                                                               )
                                  )
                          .Replace("@@PERSON2@@", string.Format("<a href='user/info/{0}'>{1}</a>",
                                                                    maniphestTransaction.OldValue,
                                                                    getAccountName(maniphestTransaction.OldValue)
                                                               )
                                  )
                          .Replace("@@PERSON3@@", string.Format("<a href='user/info/{0}'>{1}</a>",
                                                                    maniphestTransaction.NewValue,
                                                                    getAccountName(maniphestTransaction.NewValue)
                                                               )
                                  ),
                    HtmlViewPage.ArgumentOptions.NoHtmlEncoding
                );

                transactionData.SetText("TASK-TRANSACTION-DETAIL", "", HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
            }
        }

        /// <summary>
        /// Shows the Priority for a given Maniphest task
        /// </summary>
        /// <param name="transactionData"></param>
        /// <param name="maniphestTransaction"></param>
        /// <param name="database"></param>
        /// <param name="maniphestPriorityStorage"></param>
        private void SetTransactionDataPriority(HtmlPartialViewPage transactionData, Phabricator.Data.Transaction maniphestTransaction, Phabricator.Data.Maniphest maniphestTask, Storage.Database database, Storage.ManiphestPriority maniphestPriorityStorage)
        {
            Phabricator.Data.ManiphestPriority oldPriority = null;
            if (maniphestTransaction.OldValue != null)
            {
                oldPriority = maniphestPriorityStorage.Get(database, maniphestTransaction.OldValue, browser.Session.Locale);
            }
            Phabricator.Data.ManiphestPriority newPriority = maniphestPriorityStorage.Get(database, maniphestTransaction.NewValue, browser.Session.Locale);

            if (oldPriority != null && maniphestTask.Token.StartsWith("PHID-NEWTOKEN-") == false)
            {
                string translatedOldPriorityText = Locale.TranslateText("ManiphestPriority." + oldPriority.Name, browser.Session.Locale);
                if (translatedOldPriorityText.StartsWith("ManiphestPriority."))
                {
                    // no translation found -> take original text instead
                    translatedOldPriorityText = oldPriority.Name;
                }

                string translatedNewPriorityText = Locale.TranslateText("ManiphestPriority." + newPriority.Name, browser.Session.Locale);
                if (translatedNewPriorityText.StartsWith("ManiphestPriority."))
                {
                    // no translation found -> take original text instead
                    translatedNewPriorityText = newPriority.Name;
                }

                transactionData.SetText("TASK-TRANSACTION-TEXT",
                    Locale.TranslateText("@@PERSON@@ changed the task priority from @@PRIORITY1@@ to @@PRIORITY2@@", browser.Session.Locale)
                          .Replace("@@PERSON@@", string.Format("<a href='user/info/{0}'>{1}</a>",
                                                                   maniphestTransaction.Author,
                                                                   getAccountName(maniphestTransaction.Author)
                                                              )
                                  )
                          .Replace("@@PRIORITY1@@", string.Format("<b>{0}</b>", translatedOldPriorityText))
                          .Replace("@@PRIORITY2@@", string.Format("<b>{0}</b>", translatedNewPriorityText)),
                    HtmlViewPage.ArgumentOptions.NoHtmlEncoding
                );

                transactionData.SetText("TASK-TRANSACTION-DETAIL", "", HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
            }
            else
            {
                string translatedNewPriorityText = Locale.TranslateText("ManiphestPriority." + newPriority.Name, browser.Session.Locale);
                if (translatedNewPriorityText.StartsWith("ManiphestPriority."))
                {
                    // no translation found -> take original text instead
                    translatedNewPriorityText = newPriority.Name;
                }

                transactionData.SetText("TASK-TRANSACTION-TEXT",
                    Locale.TranslateText("@@PERSON@@ changed the task priority to @@PRIORITY@@", browser.Session.Locale)
                          .Replace("@@PERSON@@", string.Format("<a href='user/info/{0}'>{1}</a>",
                                                                   maniphestTransaction.Author,
                                                                   getAccountName(maniphestTransaction.Author)
                                                              )
                                  )
                          .Replace("@@PRIORITY@@", string.Format("<b>{0}</b>", translatedNewPriorityText)),
                    HtmlViewPage.ArgumentOptions.NoHtmlEncoding
                );

                transactionData.SetText("TASK-TRANSACTION-DETAIL", "", HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
            }
        }

        /// <summary>
        /// Shows the Project data for a given Maniphest task
        /// </summary>
        /// <param name="transactionData"></param>
        /// <param name="maniphestTransaction"></param>
        /// <param name="maniphestTask"></param>
        /// <param name="database"></param>
        /// <param name="projectStorage"></param>
        private void SetTransactionDataProject(HtmlPartialViewPage transactionData, Phabricator.Data.Transaction maniphestTransaction, Phabricator.Data.Maniphest maniphestTask, Database database, Storage.Project projectStorage)
        {
            string projectList = "";
            IEnumerable<Phabricator.Data.Transaction> taskProjectTransactions = maniphestTask.Transactions
                                                                                             .Where(tran => tran.Type.StartsWith("project-"));
            foreach (var projectTransaction in taskProjectTransactions)
            {
                string currentProjectHTML = "";
                bool isFirstProject = taskProjectTransactions.FirstOrDefault().Equals(projectTransaction) == true;
                bool isLastProject = taskProjectTransactions.LastOrDefault().Equals(projectTransaction) == true;

                Phabricator.Data.Project project = projectStorage.Get(database, projectTransaction.NewValue, browser.Session.Locale);
                if (project != null)
                {
                    // translate project-token to project/tag name
                    currentProjectHTML = string.Format("<a href='project/info/{0}'>{1}</a>", project.Token, project.Name);
                }
                else
                {
                    // should not happen
                    continue;
                }

                if (isFirstProject)
                {
                    projectList = currentProjectHTML;
                }
                else
                if (isLastProject)
                {
                    projectList += " and " + currentProjectHTML;
                }
                else
                {
                    projectList += ", " + currentProjectHTML;
                }
            }

            if (taskProjectTransactions.Skip(1).Any())
            {
                // multiple projects assigned
                transactionData.SetText("TASK-TRANSACTION-TEXT",
                    Locale.TranslateText("@@PERSON@@ assigned the following projects: @@PROJECT-NAMES@@", browser.Session.Locale)
                          .Replace("@@PERSON@@", string.Format("<a href='user/info/{0}'>{1}</a>",
                                                                   maniphestTransaction.Author,
                                                                   getAccountName(maniphestTransaction.Author)
                                                              )
                                  )
                          .Replace("@@PROJECT-NAMES@@", projectList),
                    HtmlViewPage.ArgumentOptions.NoHtmlEncoding
                );

                transactionData.SetText("TASK-TRANSACTION-DETAIL", "", HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
            }
            else
            {
                // only 1 project assigned
                transactionData.SetText("TASK-TRANSACTION-TEXT",
                    Locale.TranslateText("@@PERSON@@ assigned the following project: @@PROJECT-NAME@@", browser.Session.Locale)
                          .Replace("@@PERSON@@", string.Format("<a href='user/info/{0}'>{1}</a>",
                                                                   maniphestTransaction.Author,
                                                                   getAccountName(maniphestTransaction.Author)
                                                              )
                                  )
                          .Replace("@@PROJECT-NAME@@", projectList),
                    HtmlViewPage.ArgumentOptions.NoHtmlEncoding
                );

                transactionData.SetText("TASK-TRANSACTION-DETAIL", "", HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
            }
        }

        /// <summary>
        /// Shows the Status for a given Maniphest task
        /// </summary>
        /// <param name="transactionData"></param>
        /// <param name="maniphestTransaction"></param>
        /// <param name="database"></param>
        /// <param name="storageManiphestStatus"></param>
        private void SetTransactionDataStatus(HtmlPartialViewPage transactionData, Phabricator.Data.Transaction maniphestTransaction, Phabricator.Data.Maniphest maniphestTask, Storage.Database database, Storage.ManiphestStatus storageManiphestStatus)
        {
            Phabricator.Data.ManiphestStatus oldStatus = null;
            if (maniphestTransaction.OldValue != null)
            {
                oldStatus = storageManiphestStatus.Get(database, maniphestTransaction.OldValue, browser.Session.Locale);
            }
            Phabricator.Data.ManiphestStatus newStatus = storageManiphestStatus.Get(database, maniphestTransaction.NewValue, browser.Session.Locale);

            if (oldStatus != null && maniphestTask.Token.StartsWith("PHID-NEWTOKEN-") == false)
            {
                string translatedOldStatusText = Locale.TranslateText("ManiphestStatus." + oldStatus.Name, browser.Session.Locale);
                if (translatedOldStatusText.StartsWith("ManiphestStatus."))
                {
                    // no translation found -> take original text instead
                    translatedOldStatusText = oldStatus.Name;
                }

                string translatedNewStatusText = Locale.TranslateText("ManiphestStatus." + newStatus.Name, browser.Session.Locale);
                if (translatedNewStatusText.StartsWith("ManiphestStatus."))
                {
                    // no translation found -> take original text instead
                    translatedNewStatusText = newStatus.Name;
                }

                transactionData.SetText("TASK-TRANSACTION-TEXT",
                        Locale.TranslateText("@@PERSON@@ marked this task from @@OLDSTATE@@ to @@NEWSTATE@@", browser.Session.Locale)
                              .Replace("@@PERSON@@", string.Format("<i class='fa {0} {1}'></i><a href='user/info/{2}'>{3}</a>",
                                                                    newStatus.Icon,
                                                                    newStatus.Closed ? "closed" : "open",
                                                                    maniphestTransaction.Author,
                                                                    getAccountName(maniphestTransaction.Author)
                                                                  )
                                      )
                              .Replace("@@OLDSTATE@@", string.Format("<b>{0}</b>", translatedOldStatusText))
                              .Replace("@@NEWSTATE@@", string.Format("<b>{0}</b>", translatedNewStatusText)),
                    HtmlViewPage.ArgumentOptions.NoHtmlEncoding);

                transactionData.SetText("TASK-TRANSACTION-DETAIL", "", HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
            }
            else
            {
                string translatedNewStatusText = Locale.TranslateText("ManiphestStatus." + newStatus.Name, browser.Session.Locale);
                if (translatedNewStatusText.StartsWith("ManiphestStatus."))
                {
                    // no translation found -> take original text instead
                    translatedNewStatusText = newStatus.Name;
                }

                transactionData.SetText("TASK-TRANSACTION-TEXT",
                        Locale.TranslateText("@@PERSON@@ marked this task as @@STATE@@", browser.Session.Locale)
                              .Replace("@@PERSON@@", string.Format("<i class='fa {0} {1}'></i><a href='user/info/{2}'>{3}</a>",
                                                                    newStatus.Icon,
                                                                    newStatus.Closed ? "closed" : "open",
                                                                    maniphestTransaction.Author,
                                                                    getAccountName(maniphestTransaction.Author)
                                                                  )
                                      )
                              .Replace("@@STATE@@", string.Format("<b>{0}</b>", 
                                                                    translatedNewStatusText
                                                                 )
                                      ),
                    HtmlViewPage.ArgumentOptions.NoHtmlEncoding);

                transactionData.SetText("TASK-TRANSACTION-DETAIL", "", HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
            }
        }

        /// <summary>
        /// Shows the Subscriber data for a given Maniphest task
        /// </summary>
        /// <param name="transactionData"></param>
        /// <param name="maniphestTransaction"></param>
        /// <param name="maniphestTask"></param>
        /// <param name="database"></param>
        /// <param name="projectStorage"></param>
        /// <param name="userStorage"></param>
        /// <returns></returns>
        private bool SetTransactionDataSubscriber(HtmlPartialViewPage transactionData, Phabricator.Data.Transaction maniphestTransaction, Phabricator.Data.Maniphest maniphestTask, Database database, Storage.Project projectStorage, Storage.User userStorage)
        {
            string subscriberList = "";
            IEnumerable<Phabricator.Data.Transaction> taskSubscriberTransactions = maniphestTask.Transactions
                                                                                     .Where(tran => tran.Type.StartsWith("subscriber-"));
            foreach (var subscriberTransaction in taskSubscriberTransactions)
            {
                string currentSubscriberHTML = "";
                bool isFirstSubscriber = taskSubscriberTransactions.FirstOrDefault().Equals(subscriberTransaction) == true;
                bool isLastSubscriber = taskSubscriberTransactions.LastOrDefault().Equals(subscriberTransaction) == true;

                Phabricator.Data.User subscriberUser = userStorage.Get(database, subscriberTransaction.NewValue, browser.Session.Locale);
                if (subscriberUser != null)
                {
                    // translate subscriber-token to username
                    currentSubscriberHTML = string.Format("<a href='user/info/{0}'>{1}</a>", subscriberUser.Token, subscriberUser.RealName);
                }
                else
                {
                    Phabricator.Data.Project subscriberProject = projectStorage.Get(database, subscriberTransaction.NewValue, browser.Session.Locale);
                    if (subscriberProject != null)
                    {
                        // translate subscriber-token to project/tag name
                        currentSubscriberHTML = string.Format(", <a href='project/info/{0}'>{1}</a>", subscriberProject.Token, subscriberProject.Name);
                    }
                    else
                    {
                        // should not happen
                        continue;
                    }
                }

                if (isFirstSubscriber)
                {
                    subscriberList = currentSubscriberHTML;
                }
                else
                if (isLastSubscriber)
                {
                    subscriberList += " " 
                                   + Locale.TranslateText("and", browser.Session.Locale) 
                                   + " " 
                                   + currentSubscriberHTML;
                }
                else
                {
                    subscriberList += ", " + currentSubscriberHTML;
                }
            }

            if (string.IsNullOrEmpty(subscriberList))
            {
                // should not happen
                return false;
            }

            transactionData.SetText("TASK-TRANSACTION-TEXT",
                        Locale.TranslateText("@@PERSON@@ arranged for @@PERSONS@@ to be subscribed", browser.Session.Locale)
                              .Replace("@@PERSON@@", string.Format("<a href='user/info/{0}'>{1}</a>",
                                                                        maniphestTransaction.Author,
                                                                        getAccountName(maniphestTransaction.Author)
                                                                  )
                                      )
                              .Replace("@@PERSONS@@", subscriberList),
                        HtmlViewPage.ArgumentOptions.NoHtmlEncoding
            );

            transactionData.SetText("TASK-TRANSACTION-DETAIL", "", HtmlViewPage.ArgumentOptions.NoHtmlEncoding);

            return true;
        }
    }
}
