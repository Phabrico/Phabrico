using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phabrico.Phabricator.API
{
    /// <summary>
    /// Represent some Project based Phabricator Conduit API wrappers
    /// </summary>
    class Project
    {
        /// <summary>
        /// Downloads all projects since a given timestamp
        /// </summary>
        /// <param name="database">The SQLite database connection</param>
        /// <param name="conduit">The Phabricator API connection</param>
        /// <param name="modifiedSince">The minimum timestamp</param>
        /// <returns></returns>
        public IEnumerable<Data.Project> GetAll(Database database, Conduit conduit, DateTimeOffset modifiedSince)
        {
            double minimumDateTime = modifiedSince.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;
            string firstItemId = "";

            while (true)
            {
                string json = conduit.Query("project.search", 
                                            null,
                                            null,
                                            "oldest",
                                            firstItemId);
                JObject projectData = JsonConvert.DeserializeObject(json) as JObject;
                if (projectData == null) break;

                IEnumerable<JObject> projects = projectData["result"]["data"].OfType<JObject>();
                if (projects.Any() == false) break;

                foreach (JObject project in projects)
                {
                    double unixTimeStamp = (double)project["fields"]["dateModified"];
                    if (unixTimeStamp < minimumDateTime) continue;

                    Data.Project newProject = new Data.Project();
                    newProject.Token = project["phid"].ToString();
                    newProject.Name = project["fields"]["name"].ToString();
                    newProject.InternalName = project["fields"]["slug"].ToString();
                    newProject.Description = project["fields"]["description"].ToString();

                    yield return newProject;
                }

                string lastPageId = projects.Select(c => c.SelectToken("id")).LastOrDefault().Value<string>();

                firstItemId = lastPageId;
            }
        }
    }
}
