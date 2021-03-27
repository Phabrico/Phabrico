using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Phabrico.Http;
using Phabrico.Storage;

namespace Phabrico.Phabricator.API
{
    /// <summary>
    /// Represent some Maniphest task based Phabricator Conduit API wrappers
    /// </summary>
    class Maniphest
    {
        /// <summary>
        /// Time difference between the Phabricator server and the local (Phabrico) computer.
        /// There shouldn't be any clock difference, but just in case...
        /// Just to be clear: time zone differences are not meant with this 'time difference'
        /// It's just for meant for times which are slightly off
        /// </summary>
        public TimeSpan TimeDifferenceBetweenPhabricatorAndLocalComputer { get; set; } = new TimeSpan();

        /// <summary>
        /// Downloads some Maniphest tasks from Phabricator based on some filter constraints and since a given timestamp
        /// </summary>
        /// <param name="database">SQLite database</param>
        /// <param name="conduit">Reference to Conduit API</param>
        /// <param name="constraints">COnstraints to filter the list of Maniphest tasks to be downloaded</param>
        /// <param name="modifiedSince">Timestamp since when the Maniphest tasks need to be downloaded</param>
        /// <returns></returns>
        public IEnumerable<Data.Maniphest> GetAll(Database database, Conduit conduit, Constraint[] constraints, DateTimeOffset modifiedSince)
        {
            double minimumDateTime = modifiedSince.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;

            Storage.ManiphestStatus maniphestStatusStorage = new Storage.ManiphestStatus();
            Dictionary<string, Phabricator.Data.ManiphestStatus> maniphestStatuses = maniphestStatusStorage.Get(database)
                                                                                                           .ToDictionary(key => key.Value, value => value);

            List<Constraint> internalConstraints = constraints.ToList();
            if (minimumDateTime == 0) minimumDateTime = 1; // minimum Phabricator timestamp can not be 0
            internalConstraints.Add(new Constraint("modifiedStart", (Int64)minimumDateTime));

            string firstItemId = "";
            bool searchForModifications = true;
            while (searchForModifications)
            {
                // get list of maniphest tasks
                string json = conduit.Query("maniphest.search",
                                            internalConstraints.ToArray(),
                                            new Attachment[] {
                                                new Attachment("projects"),
                                                new Attachment("subscribers")
                                            },
                                            "updated",
                                            firstItemId
                                           );
                JObject maniphestDataData = JsonConvert.DeserializeObject(json) as JObject;

                List<JObject> maniphestTaskChanges = maniphestDataData["result"]["data"].OfType<JObject>().ToList();
                if (maniphestTaskChanges.Any() == false) break;

                List<string> processedManiphestTaskIDs = new List<string>();
                foreach (JObject maniphestTaskChange in maniphestTaskChanges)
                {
                    double unixTimeStamp = (double)maniphestTaskChange["fields"]["dateModified"];
                    if (unixTimeStamp < minimumDateTime)
                    {
                        searchForModifications = false;
                        break;
                    }

                    // we can have multiple updates for the same maniphest task -> take only the first found (=the most recent modification)
                    if (processedManiphestTaskIDs.Contains(maniphestTaskChange["id"].ToString()))
                    {
                        continue;
                    }

                    processedManiphestTaskIDs.Add(maniphestTaskChange["id"].ToString());

                    Data.Maniphest newManiphest = new Data.Maniphest();
                    newManiphest.Status = maniphestTaskChange["fields"]["status"]["value"].ToString();
                    Data.ManiphestStatus maniphestStatus;
                    if (maniphestStatuses.TryGetValue(newManiphest.Status, out maniphestStatus) == false)
                    {
                        // The description of the status of the task has not been found
                        // This can happen when the configuration of the Maniphest Statuses has been changed in Phabricator (see $PhabricatorURL/config/edit/maniphest.statuses/)
                        // Since this status does not exist (anymore) we skip downloading the task
                        continue;
                    }

                    newManiphest.Type = maniphestTaskChange["type"].ToString();
                    newManiphest.ID = maniphestTaskChange["id"].ToString();
                    newManiphest.Token = maniphestTaskChange["phid"].ToString();
                    newManiphest.Name = maniphestTaskChange["fields"]["name"].ToString();
                    newManiphest.Description = maniphestTaskChange["fields"]["description"]["raw"].ToString();
                    newManiphest.Author = maniphestTaskChange["fields"]["authorPHID"].ToString();
                    newManiphest.Owner = maniphestTaskChange["fields"]["ownerPHID"].ToString();
                    newManiphest.Priority = maniphestTaskChange["fields"]["priority"]["value"].ToString();
                    newManiphest.DateCreated = DateTimeOffset.FromUnixTimeSeconds(long.Parse(maniphestTaskChange["fields"]["dateCreated"].ToString()));
                    newManiphest.DateModified = DateTimeOffset.FromUnixTimeSeconds(long.Parse(maniphestTaskChange["fields"]["dateModified"].ToString()));
                    newManiphest.Projects = string.Join(",", maniphestTaskChange["attachments"]["projects"]["projectPHIDs"].Select(c => c.ToString()));
                    newManiphest.Subscribers = string.Join(",", maniphestTaskChange["attachments"]["subscribers"]["subscriberPHIDs"].Select(c => c.ToString()));
                    newManiphest.Transactions = Transaction.Get(database, conduit, newManiphest.Token)
                                                           .OrderByDescending(transaction => transaction.ID);
                    newManiphest.IsOpen = (maniphestStatus.Closed == false);

                    // make sure we don't have any trailing spaces (to make the diff-functionality a little easier)
                    string[] lines = newManiphest.Description.Split('\n');
                    newManiphest.Description = string.Join( "\n", 
                                                            lines.Select(line => line.TrimEnd(' ', '\r', '\t'))
                                                          );

                    // in case there's any time difference between Phabricator and Phabrico: correct timestamps from Phabricator
                    newManiphest.DateCreated = newManiphest.DateCreated.Subtract(TimeDifferenceBetweenPhabricatorAndLocalComputer);
                    newManiphest.DateModified = newManiphest.DateModified.Subtract(TimeDifferenceBetweenPhabricatorAndLocalComputer);

                    yield return newManiphest;
                }

                if (searchForModifications)
                {
                    string lastPageId = maniphestTaskChanges.Select(c => c.SelectToken("id").Value<string>()).LastOrDefault();

                    firstItemId = lastPageId;
                }
            }
        }

        /// <summary>
        /// Uploads some transactions (metadata) for a Maniphest task
        /// </summary>
        /// <param name="browser">Reference to browser</param>
        /// <param name="database">SQLite database</param>
        /// <param name="conduit">Reference to Conduit API</param>
        /// <param name="transactions">Transactions to be uploaded. All transactions belong to 1 Maniphest task</param>
        public void Edit(Browser browser, Database database, Conduit conduit, IEnumerable<Data.Transaction> transactions)
        {
            // merge transactions together
            bool maniphestTaskModified = false;
            Storage.Maniphest maniphestStorage = new Storage.Maniphest();
            Data.Maniphest maniphestTask = maniphestStorage.Get(database, transactions.FirstOrDefault().Token);

            IEnumerable<Data.Transaction> projectTransactions = transactions.Where(transaction => transaction.Type.StartsWith("project-"));
            if (projectTransactions.Any())
            {
                maniphestTaskModified = true;
                maniphestTask.Projects = string.Join(",", projectTransactions.Select(transaction => transaction.NewValue));
            }

            IEnumerable<Data.Transaction> subscriberTransactions = transactions.Where(transaction => transaction.Type.StartsWith("subscriber-"));
            if (subscriberTransactions.Any())
            {
                maniphestTaskModified = true;
                maniphestTask.Subscribers = string.Join(",", subscriberTransactions.Select(transaction => transaction.NewValue));
            }

            Data.Transaction ownerTransaction = transactions.FirstOrDefault(transaction => transaction.Type.Equals("owner"));
            if (ownerTransaction != null)
            {
                maniphestTaskModified = true;
                maniphestTask.Owner = ownerTransaction.NewValue;
            }

            Data.Transaction priorityTransaction = transactions.FirstOrDefault(transaction => transaction.Type.Equals("priority"));
            if (priorityTransaction != null)
            {
                maniphestTaskModified = true;
                maniphestTask.Priority = priorityTransaction.NewValue;
            }

            Data.Transaction statusTransaction = transactions.FirstOrDefault(transaction => transaction.Type.Equals("status"));
            if (statusTransaction != null)
            {
                maniphestTaskModified = true;
                maniphestTask.Status = statusTransaction.NewValue;
            }

            if (maniphestTaskModified)
            {
                Edit(browser, database, conduit, maniphestTask);
            }

            Data.Transaction commentTransaction = transactions.FirstOrDefault(transaction => transaction.Type.Equals("comment"));
            if (commentTransaction != null)
            {
                JObject conduitParameters = new JObject
                {
                    {  "transactions",  new JArray {
                                            new JObject {
                                                { "type", "comment" },
                                                { "value", commentTransaction.NewValue }
                                            }
                                        }
                    },
                    { "objectIdentifier",   maniphestTask.Token }
                };

                conduit.Query("maniphest.edit", conduitParameters);
            }
        }

        /// <summary>
        /// Uploads a Maniphest task to Phabricator
        /// </summary>
        /// <param name="browser">Reference to browser</param>
        /// <param name="database">SQLite database</param>
        /// <param name="conduit">Reference to Conduit API</param>
        /// <param name="maniphestTask">Maniphest task to be uploaded</param>
        public void Edit(Browser browser, Database database, Conduit conduit, Data.Maniphest maniphestTask)
        {
            Storage.ManiphestPriority maniphestPriority = new Storage.ManiphestPriority();
            string priorityName = maniphestPriority.Get(database, maniphestTask.Priority)?.Identifier;
            JObject conduitParameters = new JObject
            {
                {  "transactions",  new JArray {
                                        new JObject {
                                            { "type", "title" },
                                            { "value", maniphestTask.Name }
                                        },
                                        new JObject {
                                            { "type", "description" },
                                            { "value", maniphestTask.Description }
                                        },
                                        new JObject {
                                            { "type", "owner" },
                                            { "value", maniphestTask.Owner }
                                        },
                                        new JObject {
                                            { "type", "priority" },
                                            { "value", priorityName }
                                        },
                                        new JObject {
                                            { "type", "status" },
                                            { "value", maniphestTask.Status }
                                        },
                                        new JObject {
                                            { "type", "projects.set" },
                                            { "value", new JArray(maniphestTask.Projects.Split(',')) }
                                        },
                                        new JObject {
                                            { "type", "subscribers.set" },
                                            { "value", new JArray(maniphestTask.Subscribers.Split(',')) }
                                        }
                                    }
                },
                { "objectIdentifier",   "T" + maniphestTask.ID }
            };

            if (maniphestTask.ID.StartsWith("-"))
            {
                // this was an offline created task: remove offline (=negative) maniphest task identifier
                conduitParameters.Remove("objectIdentifier");
            }

            if (string.IsNullOrEmpty(maniphestTask.Projects))
            {
                // no project tags defined: remove projects.set parameter
                conduitParameters.SelectToken("transactions[?(@.type == 'projects.set')]").Remove();
            }

            if (string.IsNullOrEmpty(maniphestTask.Subscribers))
            {
                // no subscribers defined: remove subscribers.set parameter
                conduitParameters.SelectToken("transactions[?(@.type == 'subscribers.set')]").Remove();
            }

            if (string.IsNullOrEmpty(maniphestTask.Owner))
            {
                // no owner defined: remove owner parameter
                conduitParameters.SelectToken("transactions[?(@.type == 'owner')]").Remove();
            }
            if (string.IsNullOrEmpty(priorityName))
            {
                // no priority defined: remove priority parameter
                conduitParameters.SelectToken("transactions[?(@.type == 'priority')]").Remove();
            }

            JObject conduitResult = conduit.Query("maniphest.edit", conduitParameters);

            // rename all references to this uploaded task
            string newReference = (string)conduitResult["result"]["object"]["id"];
            database.RenameReferences(browser, maniphestTask, newReference);
        }
    }
}
