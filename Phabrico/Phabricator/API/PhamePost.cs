using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phabrico.Phabricator.API
{
    /// <summary>
    /// Represent some Phame based Phabricator Conduit API wrappers
    /// </summary>
    class PhamePost
    {
        /// <summary>
        /// Time difference between the Phabricator server and the local (Phabrico) computer.
        /// There shouldn't be any clock difference, but just in case...
        /// Just to be clear: time zone differences are not meant with this 'time difference'
        /// It's just for meant for times which are slightly off
        /// </summary>
        public TimeSpan TimeDifferenceBetweenPhabricatorAndLocalComputer { get; set; } = new TimeSpan();

        /// <summary>
        /// Downloads some Phame posts from Phabricator based on some filter constraints and since a given timestamp
        /// </summary>
        /// <param name="database">SQLite database</param>
        /// <param name="conduit">Reference to Conduit API</param>
        /// <param name="constraints">COnstraints to filter the list of Phame posts to be downloaded</param>
        /// <param name="modifiedSince">Timestamp since when the Phame posts need to be downloaded</param>
        /// <returns></returns>
        public IEnumerable<Data.PhamePost> GetAll(Database database, Conduit conduit, Constraint[] constraints, DateTimeOffset modifiedSince)
        {
            Storage.PhamePost phamePostStorage = new Storage.PhamePost();
            List<Data.PhamePost> newPhamePosts = new List<Data.PhamePost>();

            // ignore modifiedSince from parameter list, take last record from database instead
            modifiedSince = phamePostStorage.Get(database, Language.NotApplicable)
                                            .DefaultIfEmpty()
                                            .Max(post => post?.DateModified ?? new DateTimeOffset(DateTime.Now.AddYears(-1)));
            double minimumDateTime = modifiedSince.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;

            string firstItemId = "";
            int nbrSearchCyclesForModifications = 10;
            while (nbrSearchCyclesForModifications > 0)
            {
                // get list of Phame posts
                string json = conduit.Query("phame.post.search",
                                            null,
                                            null,
                                            "newest",  // TODO: Conduit does not support "updated" (=sort by dateModified instead of dateCreated)
                                            firstItemId
                                           );
                JObject phamePostData = JsonConvert.DeserializeObject(json) as JObject;
                if (phamePostData == null) break;

                List<JObject> phamePosts = phamePostData["result"]["data"].OfType<JObject>().ToList();
                if (phamePosts.Any() == false) break;

                foreach (JObject phamePost in phamePosts)
                {
                    double unixTimeStamp = (double)phamePost["fields"]["dateModified"];
                    if (unixTimeStamp <= minimumDateTime)
                    {
                        nbrSearchCyclesForModifications--;
                        if (nbrSearchCyclesForModifications == 0) break;

                        continue;
                    }

                    string phamePostID = phamePost["id"].ToString();
                    string phameBlogToken = phamePost["fields"]["blogPHID"].ToString();
                    string phamePostToken = phamePost["phid"].ToString();
                    string title = phamePost["fields"]["title"].ToString();
                    string authorToken = phamePost["fields"]["authorPHID"].ToString();
                    string content = phamePost["fields"]["body"].ToString();

                    Data.PhamePost newPhamePost = new Data.PhamePost();
                    newPhamePost.Author = authorToken;
                    newPhamePost.Content = content;
                    newPhamePost.DateModified = DateTimeOffset.FromUnixTimeSeconds((long)unixTimeStamp);
                    newPhamePost.ID = phamePostID;
                    newPhamePost.Title = title;
                    newPhamePost.Token = phamePostToken;
                    newPhamePost.Blog = phameBlogToken;

                    // in case there's a time difference between Phabricator and Phabrico: correct timestamp from Phabricator
                    newPhamePost.DateModified = newPhamePost.DateModified.Subtract(TimeDifferenceBetweenPhabricatorAndLocalComputer);


                    newPhamePosts.Add(newPhamePost);
                }

                firstItemId = phamePosts.Select(c => c.SelectToken("id").Value<string>()).LastOrDefault();
            }

            foreach (string phameBlogToken in newPhamePosts.Select(post => post.Blog).Distinct().ToArray())
            {
                // get Phame Blog
                string json = conduit.Query("phame.blog.search",
                                            new Constraint[] {
                                                new Constraint("phids", new string[] { phameBlogToken })
                                                
                                            },
                                            null,
                                            "newest",
                                            ""
                                           );
                JObject phameBlogtData = JsonConvert.DeserializeObject(json) as JObject;
                if (phameBlogtData == null) break;

                string blogTitle = phameBlogtData["result"]["data"][0]["fields"]["name"].ToString();

                foreach (Data.PhamePost phamePost in newPhamePosts.Where(post => post.Blog.Equals(phameBlogToken)).ToArray())
                {
                    phamePost.Blog = blogTitle;
                }
            }

            return newPhamePosts;
        }
    }
}