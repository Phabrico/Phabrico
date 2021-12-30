using Phabrico.Miscellaneous;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phabrico.UnitTests.JsonConfiguration
{
    /// <summary>
    /// Basic class for JSON formatted unit tests.
    /// Each test contains an Execute and a Validate tag.
    /// The Execute tag is the configuration for the test input.
    /// The Validate tag represents the configuration for the expected test output
    /// 
    /// The Execute consists of a Method and an Arguments attribute to invoke a
    /// method from the Phabrico assembly via reflection.
    /// 
    /// The Validate tag can consist of:
    /// methodResult:
    ///     This represent the return code of the executed Phabrico method
    /// storage
    ///     This represents the SQLite database.
    ///     This tag can contain 1 or more sub-tags which represent the tables to be checked
    ///     The UnitTest will then check if there are records existing with some given column values.
    ///     
    /// For example:
    ///      {
    ///          "execute": {
    ///              "method": [
    ///                  "ProgressMethod_DownloadPhrictionDocuments",
    ///                  "ProgressMethod_DownloadFileObjects",
    ///                  "ProgressMethod_DownloadMacros"
    ///              ]
    ///          },
    ///          
    ///          "validate": {
    ///              "storage": {
    ///                  "file":  [{
    ///                          "token": "PHID-FILE-5amliyv5bk2g4oeinj3s",
    ///                          "macroname": "water"
    ///                      },{
    ///                          "token": "PHID-FILE-yezt4hwodadiyydp2yje",
    ///                          "macroname": "logo"
    ///                      }
    ///                  ]
    ///              }
    ///          }
    ///      }
    ///      
    /// will execute ProgressMethod_DownloadPhrictionDocuments, ProgressMethod_DownloadFileObjects and ProgressMethod_DownloadMacros
    /// and it expects that the FileInfo table contains 2 records with a given token and macroname
    /// </summary>
    public class UnitTest
    {
        public Execute execute { get; set; }
        public Validate validate { get; set; }
        public object result { get; set; }

        public Phabrico.Storage.Database Database { get; set; }

        public bool Success
        {
            get
            {
                if (validate != null)
                {
                    if (validate.methodResult != null)
                    {
                        if (result == null) return false;
                        if (result.GetType().Equals(validate.methodResult.GetType()) == false) return false;

                        if (result is Enumerable)
                        {
                            return Enumerable.SequenceEqual(result as IEnumerable<object>, validate.methodResult as IEnumerable<object>);
                        }
                        else
                        {
                            if (validate.methodResult is string)
                            {
                                string s1 = (result as string).Replace("\r", "");
                                string s2 = (validate.methodResult as string).Replace("\r", "");

                                if (s1 == null || s2 == null) return false;

                                return s1.Equals(s2);
                            }
                            else
                            {
                                return result.Equals(validate.methodResult);
                            }
                        }
                    }

                    if (validate.storage != null)
                    {
                        if (validate.storage.account != null)
                        {
                            Phabrico.Storage.Account accountStorage = new Phabrico.Storage.Account();

                            foreach (var account in validate.storage.account)
                            {
                                if (account.token == null)
                                {
                                    // token not defined in account-configuration
                                    return false;
                                }

                                if (accountStorage.Get(Database, account.token) == null)
                                {
                                    // token not found in database
                                    return false;
                                }
                            }
                        }

                        if (validate.storage.file != null)
                        {
                            Phabrico.Storage.File fileStorage = new Phabrico.Storage.File();

                            foreach (var file in validate.storage.file)
                            {
                                if (file.token == null)
                                {
                                    // value not defined in file-configuration
                                    return false;
                                }

                                Phabricator.Data.File fileObject = fileStorage.Get(Database, file.token, Language.NotApplicable);
                                if (fileObject == null)
                                {
                                    // token not found in database
                                    return false;
                                }

                                if ((file.data != null && Convert.ToBase64String(fileObject.Data).Equals(file.data) == false) ||
                                    (file.filename != null && fileObject.FileName.Equals(file.filename) == false) ||
                                    (file.id != null && fileObject.ID.Equals(file.id) == false) ||
                                    (file.size != null && fileObject.Size.Equals(file.size) == false) ||
                                    (file.macroname != null && fileObject.MacroName.Equals(file.macroname) == false))
                                {
                                    // some non-key properties are different as expected
                                    return false;
                                }
                            }
                        }

                        if (validate.storage.maniphest != null)
                        {
                            Phabrico.Storage.Maniphest maniphestStorage = new Phabrico.Storage.Maniphest();

                            foreach (var maniphest in validate.storage.maniphest)
                            {
                                if (maniphest.token == null)
                                {
                                    // value not defined in maniphest-configuration
                                    return false;
                                }

                                Phabricator.Data.Maniphest maniphestTask = maniphestStorage.Get(Database, maniphest.token, Language.NotApplicable);
                                if (maniphest == null)
                                {
                                    // token not found in database
                                    return false;
                                }

                                if ((maniphest.author != null && maniphestTask.Author.Equals(maniphest.author) == false) ||
                                    (maniphest.description != null && maniphestTask.Description.Equals(maniphest.description) == false) ||
                                    (maniphest.name != null && maniphestTask.Name.Equals(maniphest.name) == false) ||
                                    (maniphest.owner != null && maniphestTask.Owner.Equals(maniphest.owner) == false) ||
                                    (maniphest.priority != null && maniphestTask.Priority.Equals(maniphest.priority) == false) ||
                                    (maniphest.projects != null && maniphestTask.Projects.Equals(maniphest.projects) == false) ||
                                    (maniphest.status != null && maniphestTask.Status.Equals(maniphest.status) == false) ||
                                    (maniphest.subscribers != null && maniphestTask.Subscribers.Equals(maniphest.subscribers) == false))
                                {
                                    // some non-key properties are different as expected
                                    return false;
                                }
                            }
                        }

                        if (validate.storage.maniphestpriority != null)
                        {
                            Phabrico.Storage.ManiphestPriority maniphestPriorityStorage = new Phabrico.Storage.ManiphestPriority();

                            foreach (var maniphestPriority in validate.storage.maniphestpriority)
                            {
                                if (maniphestPriority.value == null)
                                {
                                    // value not defined in maniphestPriority-configuration
                                    return false;
                                }

                                Phabricator.Data.ManiphestPriority priority = maniphestPriorityStorage.Get(Database, maniphestPriority.value.ToString(), Language.NotApplicable);
                                if (priority == null)
                                {
                                    // token not found in database
                                    return false;
                                }

                                if ((maniphestPriority.color != null && priority.Color.Equals(maniphestPriority.color) == false) ||
                                    (maniphestPriority.identifier != null && priority.Identifier.Equals(maniphestPriority.identifier) == false) ||
                                    (maniphestPriority.name != null && priority.Name.Equals(maniphestPriority.name) == false))
                                {
                                    // some non-key properties are different as expected
                                    return false;
                                }
                            }
                        }

                        if (validate.storage.manipheststatus != null)
                        {
                            Phabrico.Storage.ManiphestStatus maniphestStatusStorage = new Phabrico.Storage.ManiphestStatus();

                            foreach (var maniphestStatus in validate.storage.manipheststatus)
                            {
                                if (maniphestStatus.value == null)
                                {
                                    // token not defined in maniphestStatus-configuration
                                    return false;
                                }

                                Phabricator.Data.ManiphestStatus status = maniphestStatusStorage.Get(Database, maniphestStatus.value, Language.NotApplicable);
                                if (status == null)
                                {
                                    // token not found in database
                                    return false;
                                }

                                if ((maniphestStatus.closed != null && status.Closed.Equals(maniphestStatus.closed) == false) ||
                                    (maniphestStatus.icon != null && status.Icon.Equals(maniphestStatus.icon) == false) ||
                                    (maniphestStatus.name != null && status.Name.Equals(maniphestStatus.name) == false))
                                {
                                    // some non-key properties are different as expected
                                    return false;
                                }
                            }
                        }

                        if (validate.storage.phriction != null)
                        {
                            Phabrico.Storage.Phriction phrictionStorage = new Phabrico.Storage.Phriction();

                            foreach (var phriction in validate.storage.phriction)
                            {
                                if (phriction.token == null)
                                {
                                    // value not defined in phriction-configuration
                                    return false;
                                }

                                Phabricator.Data.Phriction phrictionDocument = phrictionStorage.Get(Database, phriction.token, Language.NotApplicable);
                                if (phrictionDocument == null)
                                {
                                    // token not found in database
                                    return false;
                                }

                                if ((phriction.author != null && phrictionDocument.Author.Equals(phriction.author) == false) ||
                                    (phriction.content != null && phrictionDocument.Content.Equals(phriction.content) == false) ||
                                    (phriction.name != null && phrictionDocument.Name.Equals(phriction.name) == false) ||
                                    (phriction.path != null && phrictionDocument.Path.Equals(phriction.path) == false) ||
                                    (phriction.projects != null && phrictionDocument.Projects.Equals(phriction.projects) == false) ||
                                    (phriction.subscribers != null && phrictionDocument.Subscribers.Equals(phriction.subscribers) == false))
                                {
                                    // some non-key properties are different as expected
                                    return false;
                                }
                            }
                        }

                        if (validate.storage.project != null)
                        {
                            Phabrico.Storage.Project projectStorage = new Phabrico.Storage.Project();

                            foreach (var project in validate.storage.project)
                            {
                                if (project.token == null)
                                {
                                    // token not defined in project-configuration
                                    return false;
                                }

                                if (projectStorage.Get(Database, project.token) == null)
                                {
                                    // token not found in database
                                    return false;
                                }
                            }
                        }

                        if (validate.storage.user != null)
                        {
                            Phabrico.Storage.User userStorage = new Phabrico.Storage.User();

                            foreach (var user in validate.storage.user)
                            {
                                if (user.token == null)
                                {
                                    // token not defined in user-configuration
                                    return false;
                                }

                                if (userStorage.Get(Database, user.token) == null)
                                {
                                    // token not found in database
                                    return false;
                                }
                            }
                        }
                    }
                }

                return true;
            }
        }
    }
}
