using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Http;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

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
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/search")]
        public void HttpGetLoadParameters(Http.Server httpServer, ref JsonMessage jsonMessage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.HideSearch) throw new Phabrico.Exception.HttpNotFound("/search");

            if (parameters.Any())
            {
                Storage.Account accountStorage = new Storage.Account();
                Storage.Keyword keywordStorage = new Storage.Keyword();
                Storage.Maniphest maniphestStorage = new Storage.Maniphest();
                Storage.PhamePost phamePostStorage = new Storage.PhamePost();
                Storage.Phriction phrictionStorage = new Storage.Phriction();

                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    List<SearchResult> searchResults = new List<SearchResult>();

                    Phabricator.Data.Account currentAccount = accountStorage.WhoAmI(database, browser);

                    // in case the search-filter contains a '/', we have a parameters array with more than 1 element -> combine all elements
                    parameters = new string[] { string.Join("/", parameters) };

                    // in case multiple words are entered as search criteria, split them all up as separate filter values
                    parameters = HttpUtility.UrlDecode(parameters.FirstOrDefault()).Split(' ').ToArray();

                    // loop through all tokens which can relate to the given filter values
                    foreach (string token in keywordStorage.GetTokensByWords(database, parameters, "", browser.Session.Locale))
                    {
                        SearchResult searchResult = null;

                        switch (token)
                        {
                            case string maniphestTask when maniphestTask.StartsWith(Phabricator.Data.Maniphest.Prefix):
                                searchResult = ProcessManiphestTask(httpServer, database, maniphestStorage, token, parameters);
                                break;

                            case string newPhabricatorObject when newPhabricatorObject.StartsWith("PHID-NEWTOKEN-"):
                                searchResult = ProcessPhrictionDocument(httpServer, database, phrictionStorage, currentAccount, token, parameters);
                                if (searchResult == null)
                                {
                                    searchResult = ProcessManiphestTask(httpServer, database, maniphestStorage, token, parameters);
                                }
                                break;

                            case string phrictionDocument when phrictionDocument.StartsWith(Phabricator.Data.Phriction.Prefix):
                                searchResult = ProcessPhrictionDocument(httpServer, database, phrictionStorage, currentAccount, token, parameters);
                                break;

                            case string blogPost when blogPost.StartsWith(Phabricator.Data.PhamePost.Prefix):
                                searchResult = ProcessPhameBlogPost(httpServer, database, phamePostStorage, token, parameters);
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

                        // collect a limit of search results, so we can filter out later the 10 best results
                        if (searchResults.Count(r => r.Priority > 3) == 10
                            || searchResults.Count(r => r.Priority > 2) == 30
                            || searchResults.Count == 50) break;
                    }

                    // return maximum 10 results
                    string result = JsonConvert.SerializeObject(searchResults.OrderByDescending(r => r.Priority).Take(10));
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
                Phabricator.Data.Maniphest data = maniphestStorage.Get(database, token, Language.NotApplicable);
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
        /// Creates a new SearchResult record for a given Phame blog post
        /// </summary>
        /// <param name="database">reference to Http.Server</param>
        /// <param name="database">SQLite database</param>
        /// <param name="phrictionStorage">Phriction storage</param>
        /// <param name="token">Phame blog post token</param>
        /// <param name="parameters">Extra parameters found in Remarkup code</param>
        /// <returns>SearchResult object or null</returns>
        private SearchResult ProcessPhameBlogPost(Server httpServer, Database database, PhamePost phamePostStorage, string token, string[] parameters)
        {
            SearchResult searchResult = null;

            if (httpServer.Customization.HidePhame == false)
            {
                Phabricator.Data.PhamePost data = phamePostStorage.Get(database, token, Language.NotApplicable);
                if (data != null)
                {
                    if (httpServer.ValidUserRoles(database, browser, data) == false)
                    {
                        return null;
                    }

                    searchResult = new SearchResult();
                    searchResult.Description = data.Title;
                    searchResult.Path = "J" + data.ID;
                    searchResult.URL = "phame/post/" + data.ID;
                    searchResult.Priority = 0;

                    // if the word is in the blog post's title, increase the priority of the keyword
                    if (parameters.All(word => data.Title.ToUpper().Split(' ').Contains(word.ToUpper())))
                    {
                        searchResult.Priority++;
                    }

                    // if the word is partial in the blog post's title increase the priority of the keyword
                    if (parameters.All(word => data.Title.ToUpper().Split(' ').Any(partial => partial.StartsWith(word.ToUpper()))))
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
        private SearchResult ProcessPhrictionDocument(Http.Server httpServer, Database database, Storage.Phriction phrictionStorage, Phabricator.Data.Account currentAccount, string token, string[] parameters)
        {
            SearchResult searchResult = null;

            if (httpServer.Customization.HidePhriction == false)
            {
                Phabricator.Data.Phriction phrictionDocument = phrictionStorage.Get(database, token, browser.Session.Locale);
                if (phrictionDocument != null)
                {
                    if (httpServer.ValidUserRoles(database, browser, phrictionDocument) == false)
                    {
                        return null;
                    }

                    if (phrictionStorage.IsHiddenFromSearchResults(database, browser, phrictionDocument.Path, currentAccount.UserName))
                    {
                        return null;
                    }

                    string urlCrumbs = Controllers.Phriction.GenerateCrumbs(database, phrictionDocument, browser.Session.Locale);
                    JArray crumbs = JsonConvert.DeserializeObject(urlCrumbs) as JArray;
                    urlCrumbs = string.Join("/", crumbs.Where(t => ((JValue)t["inexistant"]).Value.Equals(false))
                                                       .Select(t => ((JValue)t["name"]).Value).ToArray()
                                           );

                    searchResult = new SearchResult();
                    searchResult.Description = phrictionDocument.Name;
                    searchResult.Path = urlCrumbs;
                    searchResult.URL = "w/" + phrictionDocument.Path;
                    searchResult.Priority = 0;

                    // if the word is in the document's title, increase the priority of the keyword
                    if (parameters.All(word => phrictionDocument.Name.ToUpper().Split(' ').Contains(word.ToUpper())))
                    {
                        searchResult.Priority++;
                    }

                    // if the word is partial in the document's title increase the priority of the keyword
                    if (parameters.All(word => phrictionDocument.Name.ToUpper().Split(' ').Any(partial => partial.StartsWith(word.ToUpper()))))
                    {
                        searchResult.Priority++;
                    }
                }
            }

            return searchResult;
        }
    }
}