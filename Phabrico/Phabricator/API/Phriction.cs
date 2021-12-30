using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Http;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phabrico.Phabricator.API
{
    /// <summary>
    /// Represent some Phriction based Phabricator Conduit API wrappers
    /// </summary>
    class Phriction
    {
        /// <summary>
        /// Time difference between the Phabricator server and the local (Phabrico) computer.
        /// There shouldn't be any clock difference, but just in case...
        /// Just to be clear: time zone differences are not meant with this 'time difference'
        /// It's just for meant for times which are slightly off
        /// </summary>
        public TimeSpan TimeDifferenceBetweenPhabricatorAndLocalComputer { get; set; } = new TimeSpan();

        /// <summary>
        /// Downloads some Phriction documents from Phabricator based on some filter constraints and since a given timestamp
        /// </summary>
        /// <param name="database">SQLite database</param>
        /// <param name="conduit">Reference to Conduit API</param>
        /// <param name="constraints">COnstraints to filter the list of Phriction documents to be downloaded</param>
        /// <param name="modifiedSince">Timestamp since when the Phriction documents need to be downloaded</param>
        /// <returns></returns>
        public IEnumerable<Data.Phriction> GetAll(Database database, Conduit conduit, Constraint[] constraints, DateTimeOffset modifiedSince)
        {
            double minimumDateTime = modifiedSince.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;
            Dictionary<string, string> dateModifiedPerPhrictionDocumentToken = new Dictionary<string, string>();

            string firstItemId = "";
            bool searchForModifications = true;
            while (searchForModifications)
            {
                // get list of phriction documents
                string json = conduit.Query("phriction.content.search",
                                            null,
                                            null,
                                            "newest",
                                            firstItemId
                                           );
                JObject phrictionData = JsonConvert.DeserializeObject(json) as JObject;

                List<JObject> phrictionDocumentChanges = phrictionData["result"]["data"].OfType<JObject>().ToList();
                if (phrictionDocumentChanges.Any() == false) break;

                foreach (JObject phrictionDocumentChange in phrictionDocumentChanges)
                {
                    string phrictionDocumentToken = phrictionDocumentChange["fields"]["documentPHID"].ToString();
                    double unixTimeStamp = (double)phrictionDocumentChange["fields"]["dateModified"];
                    if (unixTimeStamp < minimumDateTime)
                    {
                        searchForModifications = false;
                        break;
                    }

                    if (dateModifiedPerPhrictionDocumentToken.ContainsKey(phrictionDocumentToken) == false)
                    {
                        dateModifiedPerPhrictionDocumentToken[phrictionDocumentToken] = unixTimeStamp.ToString();
                    }
                }

                if (searchForModifications)
                {
                    string lastPageId = phrictionDocumentChanges.Select(c => c.SelectToken("id").Value<string>()).LastOrDefault();

                    firstItemId = lastPageId;
                }
            }


            firstItemId = "";
            for (int chunk = 0; ; chunk += 99)
            {
                string[] phrictionDocumentsPhids = dateModifiedPerPhrictionDocumentToken.Keys.Skip(chunk).Take(99).ToArray();
                if (phrictionDocumentsPhids.Any() == false) break;

                List<Constraint> internalConstraints = new List<Constraint>();
                if (constraints != null)
                {
                    internalConstraints.AddRange(constraints);
                }
                internalConstraints.Add(new Constraint("phids", phrictionDocumentsPhids));

                // get list of phriction documents
                string json = conduit.Query("phriction.document.search",
                                            internalConstraints.ToArray(),
                                            new Attachment[] {
                                                new Attachment("content"),
                                                new Attachment("projects"),
                                                new Attachment("subscribers")
                                            },
                                            "oldest",
                                            firstItemId
                                           );
                JObject phrictionData = JsonConvert.DeserializeObject(json) as JObject;

                foreach (JObject phrictionDocument in phrictionData["result"]["data"].OfType<JObject>())
                {
                    string phrictionToken = phrictionDocument["phid"].ToString();

                    if (dateModifiedPerPhrictionDocumentToken.ContainsKey(phrictionToken) == false) continue;

                    Data.Phriction newPhriction = new Data.Phriction();
                    newPhriction.Token = phrictionToken;
                    newPhriction.Content = phrictionDocument["attachments"]["content"]["content"]["raw"].ToString();
                    newPhriction.Name = phrictionDocument["attachments"]["content"]["title"].ToString();
                    newPhriction.Author = phrictionDocument["attachments"]["content"]["authorPHID"].ToString();
                    newPhriction.Subscribers = string.Join(",", phrictionDocument["attachments"]["subscribers"]["subscriberPHIDs"].Select(c => c.ToString()));
                    newPhriction.DateModified = DateTimeOffset.FromUnixTimeSeconds(long.Parse(dateModifiedPerPhrictionDocumentToken[newPhriction.Token]));

                    Data.Transaction latestTransaction = Transaction.Get(database, conduit, newPhriction.Token).FirstOrDefault();
                    if (latestTransaction != null)
                    {
                        newPhriction.LastModifiedBy = latestTransaction.Author;
                    }

                    newPhriction.Projects = string.Join(",", phrictionDocument["attachments"]["projects"]["projectPHIDs"].Select(c => c.ToString()));
                    newPhriction.Path = phrictionDocument["fields"]["path"].ToString();

                    // make sure we don't have any trailing spaces (to make the diff-functionality a little easier)
                    string[] lines = newPhriction.Content.Split('\n');
                    newPhriction.Content = string.Join( "\n", 
                                                        lines.Select(line =>
                                                        {
                                                            // in case we have a table, do not trim spaces at the end
                                                            if (line.StartsWith("|"))
                                                            {
                                                                return line.TrimEnd('\r', '\t');
                                                            }
                                                            else
                                                            {
                                                                return line.TrimEnd(' ', '\r', '\t');
                                                            }
                                                        })
                                                      );

                    // in case there's a time difference between Phabricator and Phabrico: correct timestamp from Phabricator
                    newPhriction.DateModified = newPhriction.DateModified.Subtract(TimeDifferenceBetweenPhabricatorAndLocalComputer);

                    yield return newPhriction;
                }
            }
        }

        /// <summary>
        /// Downloads all Phriction documents based on constraints (e.g. projects or users)
        /// Unlike the GetAll() method, this GetPhrictionDocuments() method does not work with incremental downloads
        /// </summary>
        /// <param name="database"></param>
        /// <param name="conduit"></param>
        /// <param name="constraints"></param>
        /// <returns></returns>
        public IEnumerable<Data.Phriction> GetPhrictionDocuments(Database database, Conduit conduit, Constraint[] constraints)
        {
            string firstItemId = "";

            bool searchForModifications = true;
            while (searchForModifications)
            {
                // get list of phriction documents
                string jsonDocument = conduit.Query("phriction.document.search",
                                            constraints,
                                            new Attachment[] {
                                                new Attachment("content"),
                                                new Attachment("projects"),
                                                new Attachment("subscribers")
                                            },
                                            "oldest",
                                            firstItemId
                                           );
                JObject phrictionData = JsonConvert.DeserializeObject(jsonDocument) as JObject;
                JObject[] phrictionDocuments = phrictionData["result"]["data"].OfType<JObject>().ToArray();
                searchForModifications = phrictionDocuments.Length == 100;

                foreach (JObject phrictionDocument in phrictionDocuments)
                {
                    string phrictionToken = phrictionDocument["phid"].ToString();

                    Data.Phriction newPhriction = new Data.Phriction();
                    newPhriction.Token = phrictionToken;
                    newPhriction.Content = phrictionDocument["attachments"]["content"]["content"]["raw"].ToString();
                    newPhriction.Name = phrictionDocument["attachments"]["content"]["title"].ToString();
                    newPhriction.Author = phrictionDocument["attachments"]["content"]["authorPHID"].ToString();
                    newPhriction.Subscribers = string.Join(",", phrictionDocument["attachments"]["subscribers"]["subscriberPHIDs"].Select(c => c.ToString()));

                    Data.Transaction latestTransaction = Transaction.Get(database, conduit, newPhriction.Token).FirstOrDefault();
                    if (latestTransaction != null)
                    {
                        newPhriction.DateModified = latestTransaction.DateModified;
                        newPhriction.LastModifiedBy = latestTransaction.Author;
                    }
                    else
                    {
                        newPhriction.DateModified = DateTimeOffset.Now;
                    }

                    newPhriction.Projects = string.Join(",", phrictionDocument["attachments"]["projects"]["projectPHIDs"].Select(c => c.ToString()));
                    newPhriction.Path = phrictionDocument["fields"]["path"].ToString();

                    // make sure we don't have any trailing spaces (to make the diff-functionality a little easier)
                    string[] lines = newPhriction.Content.Split('\n');
                    newPhriction.Content = string.Join("\n",
                                                        lines.Select(line =>
                                                        {
                                                            // in case we have a table, do not trim spaces at the end
                                                            if (line.StartsWith("|"))
                                                            {
                                                                return line.TrimEnd('\r', '\t');
                                                            }
                                                            else
                                                            {
                                                                return line.TrimEnd(' ', '\r', '\t');
                                                            }
                                                        })
                                                      );
                    yield return newPhriction;
                }

                firstItemId = phrictionDocuments.Select(c => c.SelectToken("id").Value<string>()).LastOrDefault();
            }
        }

        /// <summary>
        /// Uploads a Phriction document to Phabricator
        /// </summary>
        /// <param name="browser">Reference to browser</param>
        /// <param name="database">SQLite database</param>
        /// <param name="conduit">Reference to Conduit API</param>
        /// <param name="phrictionDocument">Phriction document to be uploaded</param>
        public void Edit(Browser browser, Database database, Conduit conduit, Data.Phriction phrictionDocument)
        {
            JObject conduitResult;
            if (phrictionDocument.Token.StartsWith("PHID-NEWTOKEN-"))
            {
                conduitResult = conduit.Query("phriction.create", new JObject
                {
                    { "slug", phrictionDocument.Path },
                    { "title", phrictionDocument.Name },
                    { "content", phrictionDocument.Content.Replace("\r", "") }
                });
            }
            else
            {
                conduitResult = conduit.Query("phriction.edit", new JObject
                {
                    { "slug", phrictionDocument.Path },
                    { "title", phrictionDocument.Name },
                    { "content", phrictionDocument.Content.Replace("\r", "") }
                });
            }

            // rename all references to this uploaded document
            string newAbsoluteReference = (string)conduitResult["result"]["slug"];
            database.RenameReferences(browser, phrictionDocument, newAbsoluteReference);
        }
    }
}