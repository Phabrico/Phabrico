using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using Newtonsoft.Json;

using Phabrico.Http;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;
using Phabrico.Storage;

namespace Phabrico.Controllers
{
    /// <summary>
    /// Represents the controller being used for the search-functionality in Phabrico
    /// </summary>
    public class Keyword : Controller
    {
        /// <summary>
        /// Model for table rows in the client backend
        /// </summary>
        public class SearchResult
        {
            /// <summary>
            /// Title or description of the Maniphest Task or Phriction document
            /// </summary>
            public string Description { get; set; }

            /// <summary>
            /// Path that can be read in the search results.
            /// The real path can be found in URL
            /// </summary>
            public string Path { get; set; }

            /// <summary>
            /// Order of importance of search result.
            /// Higer value means closer to the top.
            /// </summary>
            public int Priority { get; set; }

            /// <summary>
            /// Path to phriction document or maniphest task
            /// </summary>
            public string URL { get; set; }
        }

        /// <summary>
        /// This method is executed as soon as the user enters some characters in the search field in the upper right corner.
        /// A JSONified array of SearchResult items is returned to visualize the search results
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/search")]
        public void HttpGetLoadParameters(Http.Server httpServer, Browser browser, ref JsonMessage jsonMessage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.HideSearch) throw new Phabrico.Exception.HttpNotFound();

            if (parameters.Any())
            {
                Storage.Keyword keywordStorage = new Storage.Keyword();
                Storage.Maniphest maniphestStorage = new Storage.Maniphest();
                Storage.Phriction phrictionStorage = new Storage.Phriction();

                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    List<SearchResult> searchResults = new List<SearchResult>();

                    // in case multiple words are entered as search criteria, split them all up as separate filter values
                    parameters = HttpUtility.UrlDecode(parameters.FirstOrDefault()).Split(' ').ToArray();

                    // loop through all tokens which can relate to the given filter values
                    foreach (string token in keywordStorage.GetTokensByWords(database, parameters))
                    {
                        SearchResult searchResult = null;

                        switch (token)
                        {
                            case string maniphestTask when maniphestTask.StartsWith(Phabricator.Data.Maniphest.Prefix):
                                searchResult = ProcessManiphestTask(httpServer, database, maniphestStorage, token, parameters);
                                break;

                            case string newPhabricatorObject when newPhabricatorObject.StartsWith("PHID-NEWTOKEN-"):
                                searchResult = ProcessPhrictionDocument(httpServer, database, phrictionStorage, token, parameters);
                                if (searchResult == null)
                                {
                                    searchResult = ProcessManiphestTask(httpServer, database, maniphestStorage, token, parameters);
                                }
                                break;

                            case string phrictionDocument when phrictionDocument.StartsWith(Phabricator.Data.Phriction.Prefix):
                                searchResult = ProcessPhrictionDocument(httpServer, database, phrictionStorage, token, parameters);
                                break;

                            default:
                                continue;
                        }

                        if (searchResult == null || string.IsNullOrEmpty(searchResult.URL))
                        {
                            // url not found as a Phriction document or Maniphest task -> take next keyword storage record
                            continue;
                        }

                        if (searchResult.URL.EndsWith("//"))
                        {
                            searchResult.URL = searchResult.URL.Substring(0, searchResult.URL.Length - 1);
                        }

                        searchResults.Add(searchResult);

                        // show maximum 10 results
                        if (searchResults.Count == 10) break;
                    }

                    string result = JsonConvert.SerializeObject(searchResults.OrderByDescending(r => r.Priority));
                    jsonMessage = new JsonMessage(result);
                }
            }
        }

        /// <summary>
        /// Creates a new SearchResult record for a given Maniphest task
        /// </summary>
        /// <param name="database">reference to Http.Server</param>
        /// <param name="database">SQLite database</param>
        /// <param name="maniphestStorage">Maniphest storage</param>
        /// <param name="token">Maniphest task token</param>
        /// <param name="parameters">Extra parameters found in Remarkup code</param>
        /// <returns>SearchResult object or null</returns>
        private SearchResult ProcessManiphestTask(Http.Server httpServer, Database database, Storage.Maniphest maniphestStorage, string token, string[] parameters)
        {
            SearchResult searchResult = null;

            if (httpServer.Customization.HideManiphest == false)
            {
                Phabricator.Data.Maniphest data = maniphestStorage.Get(database, token);
                if (data != null)
                {
                    searchResult = new SearchResult();
                    searchResult.Description = string.Format("T{0}: {1}", data.ID, data.Name);
                    searchResult.Path = "T" + data.ID;
                    searchResult.URL = "maniphest/" + searchResult.Path;

                    // if the word is in the task's title, increase the priority of the keyword
                    string title = searchResult.Path + " " + data.Name.ToUpper();
                    if (parameters.All(word => title.Split(' ').Contains(word.ToUpper())))
                    {
                        searchResult.Priority += 5;
                    }

                    // if the word is partially in the task's title, increase the priority of the keyword
                    if (parameters.All(word => title.Split(' ').Any(w => w.StartsWith(word.ToUpper()))))
                    {
                        searchResult.Priority++;
                    }
                }
            }

            return searchResult;
        }

        /// <summary>
        /// Creates a new SearchResult record for a given Phriction document
        /// </summary>
        /// <param name="database">reference to Http.Server</param>
        /// <param name="database">SQLite database</param>
        /// <param name="phrictionStorage">Phriction storage</param>
        /// <param name="token">Phriction document token</param>
        /// <param name="parameters">Extra parameters found in Remarkup code</param>
        /// <returns>SearchResult object or null</returns>
        private SearchResult ProcessPhrictionDocument(Http.Server httpServer, Database database, Storage.Phriction phrictionStorage, string token, string[] parameters)
        {
            SearchResult searchResult = null;

            if (httpServer.Customization.HidePhriction == false)
            {
                Phabricator.Data.Phriction data = phrictionStorage.Get(database, token);
                if (data != null)
                {
                    searchResult = new SearchResult();
                    searchResult.Description = data.Name;
                    searchResult.Path = data.Path;
                    searchResult.URL = "w/" + data.Path;
                    searchResult.Priority = 0;

                    // if the word is in the document's title, increase the priority of the keyword
                    if (parameters.All(word => data.Name.ToUpper().Split(' ').Contains(word.ToUpper())))
                    {
                        searchResult.Priority++;
                    }

                    // if the word is partial in the document's title increase the priority of the keyword
                    if (parameters.All(word => data.Name.ToUpper().Split(' ').Any(partial => partial.StartsWith(word.ToUpper()))))
                    {
                        searchResult.Priority++;
                    }
                }
            }

            return searchResult;
        }
    }
}