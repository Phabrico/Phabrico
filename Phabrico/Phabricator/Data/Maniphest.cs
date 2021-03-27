using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Phabrico.Storage;

namespace Phabrico.Phabricator.Data
{
    /// <summary>
    /// Represents an ManiphestInfo record from the SQLite Phabrico database
    /// </summary>
    public class Maniphest : PhabricatorObject
    {
        /// <summary>
        /// Token prefix to identify Maniphest objects in the Phabrico database
        /// </summary>
        public const string Prefix = "PHID-TASK-";

        /// <summary>
        /// Author of the task
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Timestamp when the task was last modified
        /// </summary>
        public DateTimeOffset DateModified { get; set; }

        /// <summary>
        /// Timestamp when the task was created
        /// </summary>
        public DateTimeOffset DateCreated { get; set; }

        /// <summary>
        /// Content of the task
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// ID of the task (e.g. T123)
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// True if the task is not finished
        /// </summary>
        public bool IsOpen { get; set; }

        /// <summary>
        /// Token of the user who last modified this task
        /// </summary>
        public string LastModifiedBy
        {
            get
            {
                if (Transactions.Any() == false)
                {
                    return "";
                }

                return Transactions.FirstOrDefault().Author;
            }
        }

        /// <summary>
        /// Title of the task
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The user who's assigned to the task
        /// </summary>
        public string Owner { get; set; }

        /// <summary>
        /// Priority of the task
        /// </summary>
        public string Priority { get; set; }

        /// <summary>
        /// Projects linked to the task
        /// </summary>
        public string Projects { get; set; }

        /// <summary>
        /// Status of the task
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Users who're subscribed to the task
        /// </summary>
        public string Subscribers { get; set; }

        /// <summary>
        /// Is "TASK" for downloaded task from Phabricator
        /// Is "new" for newly created maniphest tasks in Phabrico (=not uploaded to Phabricator)
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Transactions linked to the task.
        /// These are the metadata you see at the bottom of the Maniphest task (e.g. comments, status changes, ...)
        /// </summary>
        [JsonIgnore] 
        public IEnumerable<Transaction> Transactions { get; set; }

        /// <summary>
        /// Initializes a new ManiphestInfo record
        /// </summary>
        public Maniphest()
        {
            IsOpen = false;
            TokenPrefix = Prefix;
            Transactions = new Transaction[0];
            Type = "new";
        }

        /// <summary>
        /// Clones a new ManiphestInfo record
        /// </summary>
        /// <param name="originalManiphestTask"></param>
        public Maniphest(Maniphest originalManiphestTask)
            : base(originalManiphestTask)
        {
            this.TokenPrefix = Prefix;

            this.Name = originalManiphestTask.Name;
            this.ID = originalManiphestTask.ID;
            this.Author = originalManiphestTask.Author;
            this.DateModified = originalManiphestTask.DateModified;
            this.DateCreated = originalManiphestTask.DateCreated;
            this.Description = originalManiphestTask.Description;
            this.Owner = originalManiphestTask.Owner;
            this.Priority = originalManiphestTask.Priority;
            this.Projects = originalManiphestTask.Projects;
            this.Status = originalManiphestTask.Status;
            this.Subscribers = originalManiphestTask.Subscribers;
            this.Type = originalManiphestTask.Type;
            this.IsOpen = originalManiphestTask.IsOpen;
            this.Transactions = originalManiphestTask.Transactions;
        }

        /// <summary>
        /// Incorporates some stage data into the current Maniphest object
        /// </summary>
        /// <param name="stageData"></param>
        /// <returns></returns>
        public override bool MergeStageData(Stage.Data stageData)
        {
            JObject stageInfo = JsonConvert.DeserializeObject(stageData.HeaderData) as JObject;

            if (stageData.Operation.Equals("owner"))
            {
                Owner = (string)stageInfo["NewValue"];

                stageData.HeaderData = JsonConvert.SerializeObject(this);
                return true;
            }

            if (stageData.Operation.Equals("priority"))
            {
                Priority = (string)stageInfo["NewValue"];

                stageData.HeaderData = JsonConvert.SerializeObject(this);
                return true;
            }

            if (stageData.Operation.Equals("status"))
            {
                Status = (string)stageInfo["NewValue"];

                stageData.HeaderData = JsonConvert.SerializeObject(this);
                return true;
            }

            if (stageData.Operation.StartsWith("project-"))
            {
                if (stageData.Operation.Equals("project-0"))
                {
                    // first project: clear all the previous existing ones
                    Projects = (string)stageInfo["NewValue"];
                }
                else
                {
                    Projects += "," + (string)stageInfo["NewValue"];
                }
                stageData.HeaderData = JsonConvert.SerializeObject(this);
                return true;
            }

            if (stageData.Operation.StartsWith("subscriber-"))
            {
                if (stageData.Operation.Equals("subscriber-0"))
                {
                    // first subscriber: clear all the previous existing ones
                    Subscribers = (string)stageInfo["NewValue"];
                }
                else
                {
                    Subscribers += "," + (string)stageInfo["NewValue"];
                }

                stageData.HeaderData = JsonConvert.SerializeObject(this);
                return true;
            }

            return false;
        }
    }
}
