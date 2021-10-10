using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phabrico.Phabricator.API
{
    /// <summary>
    /// Represent some User account based Phabricator Conduit API wrappers
    /// </summary>
    class User
    {
        /// <summary>
        /// Downloads all user accounts since a given timestamp
        /// </summary>
        /// <param name="database">The SQLite database connection</param>
        /// <param name="conduit">The Phabricator API connection</param>
        /// <param name="modifiedSince">The minimum timestamp</param>
        /// <returns></returns>
        public IEnumerable<Data.User> GetAll(Database database, Conduit conduit, DateTimeOffset modifiedSince)
        {
            double minimumDateTime = modifiedSince.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;

            Storage.Project projectStorage = new Storage.Project();
            Data.Project[] activatedProjects = projectStorage.Get(database).Where(project => project.Selected == Data.Project.Selection.Selected).ToArray();

            string firstItemId = "";
            bool searchForModifications = true;
            while (searchForModifications)
            {
                string json = conduit.Query("user.search",
                                            null,
                                            null,
                                            "newest",
                                            firstItemId
                                           );
                JObject projectData = JsonConvert.DeserializeObject(json) as JObject;

                List<JObject> userModifications = projectData["result"]["data"].OfType<JObject>().ToList();
                if (userModifications.Any() == false) break;

                foreach (JObject userModification in userModifications)
                {
                    double unixTimeStamp = (double)userModification["fields"]["dateModified"];
                    if (unixTimeStamp < minimumDateTime)
                    {
                        searchForModifications = false;
                        break;
                    }

                    string[] userRoles = userModification["fields"]["roles"]
                                            .OfType<JValue>()
                                            .Select(role => (string)role.Value)
                                            .ToArray();

                    Data.User newUser = new Data.User();
                    newUser.Token = userModification["phid"].ToString();
                    newUser.RealName = userModification["fields"]["realName"].ToString();
                    newUser.UserName = userModification["fields"]["username"].ToString();
                    newUser.IsBot = userRoles.Contains("bot");
                    newUser.IsDisabled = userRoles.Contains("disabled");
                    yield return newUser;
                }

                if (searchForModifications)
                {
                    string lastItemId = userModifications.Select(c => c.SelectToken("id").Value<string>()).LastOrDefault();

                    firstItemId = lastItemId;
                }
            }
        }

        /// <summary>
        /// Returns the Phabricator account used to log into Phabrico
        /// </summary>
        /// <param name="database">The SQLite database connection</param>
        /// <param name="conduit">The Phabricator API connection</param>
        /// <returns>Phabricator account</returns>
        public Data.User WhoAmI(Database database, Conduit conduit)
        {
            string json = conduit.Query("user.whoami");
            JToken userData = JsonConvert.DeserializeObject(json) as JToken;
            
            Data.User user = new Data.User();
            user.RealName = userData["result"]["realName"].ToString();
            user.Token = userData["result"]["phid"].ToString();

            return user;
        }
    }
}
