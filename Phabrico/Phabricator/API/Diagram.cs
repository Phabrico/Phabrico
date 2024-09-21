using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Parsers.Base64;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace Phabrico.Phabricator.API
{
    /// <summary>
    /// Represent some diagram based Phabricator Conduit API wrappers
    /// </summary>
    class Diagram
    {
        /// <summary>
        /// Downloads all requested diagrams from Phabricator
        /// </summary>
        /// <param name="database">SQLite database</param>
        /// <param name="conduit">Reference to conduit</param>
        /// <param name="diagramObjectIds">IDs of diagrams to download from Phabricator</param>
        /// <returns></returns>
        public IEnumerable<Data.Diagram> GetReferences(Database database, Conduit conduit, IEnumerable<int> diagramObjectIds)
        {
            List<Data.Diagram> result = new List<Data.Diagram>();
            if (diagramObjectIds.Any() == false)
            {
                // return empty list if no diagram object ids were given
                return result;
            }

            if (conduit.APIExists("diagram.search") == false)
            {
                // Diagram extension is not installed on Phorge instance
                return result;
            }

            string firstItemId = "";
            while (true)
            {
                string json = conduit.Query("diagram.search",
                                            null,
                                            null,
                                            "newest",
                                            firstItemId);
                JObject diagramData = JsonConvert.DeserializeObject(json) as JObject;
                if (diagramData == null) break;

                IEnumerable<JObject> diagrams = diagramData["result"]["data"].OfType<JObject>();

                if (diagrams.Any() == false) break;

                foreach (JObject diagram in diagrams)
                {
                    int diagramObjectId = Int32.Parse(diagram["id"].ToString());
                    if (diagramObjectIds.Contains(diagramObjectId) == false) continue;

                    Data.Diagram newDiagram = new Data.Diagram();
                    newDiagram.Token = diagram["phid"].ToString();
                    newDiagram.FileName = string.Format("Diagram{0}.png", diagramObjectId);
                    newDiagram.ID = diagramObjectId;
                    newDiagram.DataStream = null;
                    newDiagram.Size = Int32.Parse(diagram["fields"]["byteSize"].ToString());
                    newDiagram.DateModified = DateTimeOffset.FromUnixTimeSeconds(long.Parse(diagram["fields"]["dateModified"].ToString()));
                    result.Add(newDiagram);
                }

                if (diagramObjectIds.Count() == result.Count()) break;

                string lastPageId = diagrams.Select(c => c.SelectToken("id")).LastOrDefault().Value<string>();

                firstItemId = lastPageId;
            }

            return result;
        }
        
        /// <summary>
        /// Downloads a diagram from Phabricator.
        /// </summary>
        /// <param name="conduit"></param>
        /// <param name="diagramToken"></param>
        /// <returns></returns>
        public IEnumerable<int> GetModifiedDiagramIDs(Conduit conduit, DateTimeOffset modifiedSince)
        {
            if (conduit.APIExists("diagram.search") == false)
            {
                // Diagram extension is not installed on Phorge instance
                return new int[0];
            }

            double minimumDateTime = modifiedSince.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;
            if (minimumDateTime < 0.9) minimumDateTime = 1.0; // minimum Phabricator timestamp can not be 0

            Base64EIDOStream base64EIDOStream = new Base64EIDOStream();
            string json = conduit.Query("diagram.search",
                                        new Constraint[] {
                                            new Constraint("createdStart", minimumDateTime)
                                        });
            JObject diagramData = JsonConvert.DeserializeObject(json) as JObject;
            if (diagramData != null)
            {
                System.Exception exception = null;
                HttpWebResponse response = null;
                JArray diagrams = diagramData["result"]["data"] as JArray;
                return diagrams.Select(diagram => int.Parse((string)diagram["id"])).ToArray();
            }

            return new int[0];
        }

        /// <summary>
        /// Downloads a diagram from Phabricator.
        /// </summary>
        /// <param name="conduit"></param>
        /// <param name="diagramToken"></param>
        /// <returns></returns>
        public Base64EIDOStream DownloadData(Conduit conduit, string diagramToken)
        {
            if (conduit.APIExists("diagram.search") == false)
            {
                // Diagram extension is not installed on Phorge instance
                return null;
            }

            Base64EIDOStream base64EIDOStream = new Base64EIDOStream();
            string json = conduit.Query("diagram.search",
                                        new Constraint[] {
                                            new Constraint("phids", new string[] { diagramToken })
                                        });
            JObject diagramData = JsonConvert.DeserializeObject(json) as JObject;
            if (diagramData != null)
            {
                System.Exception exception = null;
                HttpWebResponse response = null;
                string downloadURL = diagramData["result"]["data"][0]["fields"]["dataURI"].ToString();
                for (int retry = 0; retry < 10; retry++)
                {
                    try
                    {
                        HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(downloadURL);
                        webRequest.Method = "GET";
                        response = (HttpWebResponse)webRequest.GetResponse();
                        break;
                    }
                    catch (System.Exception e)
                    {
                        response = null;
                        exception = e;
                        Thread.Sleep(1000);
                    }
                }

                if (response == null && exception != null)
                {
                    throw exception;
                }

                using (BinaryReader binaryReader = new BinaryReader(response.GetResponseStream()))
                {
                    byte[] buffer = binaryReader.ReadBytes(0x400000);
                    while (buffer.Length > 0)
                    {
                        base64EIDOStream.WriteDecodedData(buffer);
                        buffer = binaryReader.ReadBytes(buffer.Length);
                    }
                }
            }

            return base64EIDOStream;
        }

        /// <summary>
        /// Uploads a diagram to Phabricator
        /// </summary>
        /// <param name="conduit">The Phabricator Conduit API link</param>
        /// <param name="diagram">Diagram to be uploaded</param>
        /// <returns>The newly created Phabricator diagram reference id number if success; null if failed</returns>
        public int? Edit(Conduit conduit, Data.Diagram diagram)
        {
            if (conduit.APIExists("diagram.upload") == false)
            {
                // Diagram extension is not installed on Phorge instance
                return null;
            }

            // upload diagram in one go instead of uploading it in chunks
            string diagramData = diagram.DataStream.ReadEncodedBlock(0, (int)diagram.DataStream.LengthEncodedData);
            JObject conduitResult = conduit.Query("diagram.upload", new JObject
                    {
                        { "name", diagram.FileName },
                        { "data_base64", diagramData }
                    });

            string diagramPHID = (string)conduitResult["result"];


            // determine new phabricator diagram reference id and return it
            string json = conduit.Query("diagram.search",
                                        new Constraint[] {
                                                new Constraint("phids", new string[] { diagramPHID })
                                        });
            JObject diagramSearchResultData = JsonConvert.DeserializeObject(json) as JObject;
            if (diagramSearchResultData != null)
            {
                return Int32.Parse((string)diagramSearchResultData["result"]["data"][0]["id"]);
            }

            // no new phabricator diagram reference created
            return null;
        }
    }
}