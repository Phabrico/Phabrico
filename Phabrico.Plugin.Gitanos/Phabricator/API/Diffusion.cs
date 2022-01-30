using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Phabricator.API;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Phabrico.Plugin.Phabricator.API
{
    class Diffusion
    {

        /// <summary>
        /// Downloads some Maniphest tasks from Phabricator based on some filter constraints and since a given timestamp
        /// </summary>
        /// <param name="database">SQLite database</param>
        /// <param name="conduit">Reference to Conduit API</param>
        /// <param name="constraints">COnstraints to filter the list of Maniphest tasks to be downloaded</param>
        /// <param name="modifiedSince">Timestamp since when the Maniphest tasks need to be downloaded</param>
        /// <returns></returns>
        public IEnumerable<Data.Diffusion> GetModifiedRepositories(Database database, Conduit conduit, Constraint[] constraints, DateTimeOffset modifiedSince)
        {
            double minimumDateTime = modifiedSince.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;

            string firstItemId = "";
            bool searchForModifications = true;
            while (searchForModifications)
            {
                // get list of maniphest tasks
                string json = conduit.Query("diffusion.repository.search",
                                            constraints,
                                            new Attachment[] {
                                                new Attachment("uris")
                                            },
                                            "newest",
                                            firstItemId
                                           );
                JObject diffusionRepositorySearchData = JsonConvert.DeserializeObject(json) as JObject;
                if (diffusionRepositorySearchData == null) break;

                List<JObject> diffusionRepositoryChanges = diffusionRepositorySearchData["result"]["data"].OfType<JObject>().ToList();
                if (diffusionRepositoryChanges.Any() == false) break;

                foreach (JObject diffusionRepositoryChange in diffusionRepositoryChanges)
                {
                    double unixTimeStamp = (double)diffusionRepositoryChange["fields"]["dateModified"];
                    if (unixTimeStamp < minimumDateTime)
                    {
                        searchForModifications = false;
                        break;
                    }


                    Data.Diffusion newRepositoryChange = new Data.Diffusion();
                    newRepositoryChange.CallSign = diffusionRepositoryChange["fields"]["callsign"].ToString();
                    newRepositoryChange.Description = diffusionRepositoryChange["fields"]["description"]["raw"].ToString();
                    newRepositoryChange.Name = diffusionRepositoryChange["fields"]["name"].ToString();
                    newRepositoryChange.ShortName = diffusionRepositoryChange["fields"]["shortName"].ToString();
                    newRepositoryChange.Status = diffusionRepositoryChange["fields"]["status"].ToString();
                    newRepositoryChange.DateModified = DateTimeOffset.FromUnixTimeSeconds(long.Parse(diffusionRepositoryChange["fields"]["dateModified"].ToString()));

                    JToken[] uris = diffusionRepositoryChange["attachments"]["uris"]["uris"].ToArray();
                    JToken defaultURI = uris.FirstOrDefault(uri => uri["fields"]["io"]["raw"].ToString().Equals("none") == false
                                                                &&  (uri["fields"]["display"]["raw"].ToString().Equals("always") ||
                                                                     uri["fields"]["display"]["raw"].ToString().Equals("default")
                                                                    )
                                                           );
                    if (defaultURI is null) continue;

                    try
                    {
                        newRepositoryChange.URI = defaultURI["fields"]["uri"]["effective"].ToString();
                    }
                    catch
                    {
                        continue;
                    }

                    yield return newRepositoryChange;
                }

                if (searchForModifications)
                {
                    string lastPageId = diffusionRepositoryChanges.Select(c => c.SelectToken("id").Value<string>()).LastOrDefault();

                    firstItemId = lastPageId;
                }
            }
        }
    }
}
