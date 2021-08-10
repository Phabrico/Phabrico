using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phabrico.Phabricator.API
{
    /// <summary>
    /// Represent some Maniphest task priority based Phabricator Conduit API wrappers
    /// </summary>
    class ManiphestPriority
    {
        /// <summary>
        /// Downloads all available Maniphest task priorities
        /// </summary>
        /// <param name="database">SQLite database</param>
        /// <param name="conduit">Reference to Conduit API</param>
        /// <returns>A collection of ManiphestPriority objects</returns>
        public IEnumerable<Data.ManiphestPriority> GetAll(Database database, Conduit conduit)
        {
            // get list of maniphest priorities
            string json = conduit.Query("maniphest.priority.search");
            JObject maniphestDataData = JsonConvert.DeserializeObject(json) as JObject;

            foreach (JObject maniphestTask in maniphestDataData["result"]["data"].OfType<JObject>())
            {
                Data.ManiphestPriority newManiphestPriority = new Data.ManiphestPriority();
                newManiphestPriority.Name = maniphestTask["name"].ToString();
                newManiphestPriority.Identifier = maniphestTask["keywords"][0].ToString();
                newManiphestPriority.Priority = Int32.Parse( maniphestTask["value"].ToString() );
                newManiphestPriority.Color = maniphestTask["color"].ToString();
                yield return newManiphestPriority;
            }
        }
    }
}
