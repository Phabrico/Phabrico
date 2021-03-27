using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Phabrico.Storage;

namespace Phabrico.Phabricator.API
{
    /// <summary>
    /// Represent some Maniphest task status based Phabricator Conduit API wrappers
    /// </summary>
    class ManiphestStatus
    {
        /// <summary>
        /// Downloads all available Maniphest task states
        /// </summary>
        /// <param name="database">SQLite database</param>
        /// <param name="conduit">Reference to Conduit API</param>
        /// <returns>A collection of ManiphestStatus objects</returns>
        public IEnumerable<Data.ManiphestStatus> GetAll(Database database, Conduit conduit)
        {
            // get list of maniphest priorities
            string json = conduit.Query("maniphest.status.search");
            JObject maniphestDataData = JsonConvert.DeserializeObject(json) as JObject;

            foreach (JObject maniphestTask in maniphestDataData["result"]["data"].OfType<JObject>())
            {
                Data.ManiphestStatus newManiphestStatus = new Data.ManiphestStatus();
                newManiphestStatus.Name = maniphestTask["name"].ToString();
                newManiphestStatus.Value = maniphestTask["value"].ToString();
                newManiphestStatus.Closed = maniphestTask["closed"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
                yield return newManiphestStatus;
            }
        }
    }
}
