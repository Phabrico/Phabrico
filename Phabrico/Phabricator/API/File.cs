using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Parsers.Base64;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace Phabrico.Phabricator.API
{
    /// <summary>
    /// Represent some file based Phabricator Conduit API wrappers
    /// </summary>
    class File
    {
        /// <summary>
        /// Downloads all new macro references from Phabricator since a given timestamp
        /// </summary>
        /// <param name="conduit"></param>
        /// <param name="modifiedSince"></param>
        /// <returns></returns>
        public IEnumerable<KeyValuePair<string, string>> GetMacroReferences(Conduit conduit, DateTimeOffset modifiedSince)
        {
            double minimumDateTime = modifiedSince.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;

            string json = conduit.Query("macro.query");
            JObject macroListData = JsonConvert.DeserializeObject(json) as JObject;

            foreach (JProperty macro in macroListData["result"].Children())
            {
                if ((UInt64)macro.Value["dateCreated"] < minimumDateTime) break;

                yield return new KeyValuePair<string, string>(macro.Name, (string)macro.Value["filePHID"]);
            }

            yield break;
        }

        /// <summary>
        /// Downloads all requested fileobjects from Phabricator
        /// </summary>
        /// <param name="database">SQLite database</param>
        /// <param name="conduit">Reference to conduit</param>
        /// <param name="fileObjectIds">IDs of files to download from Phabricator</param>
        /// <returns></returns>
        public IEnumerable<Data.File> GetReferences(Database database, Conduit conduit, IEnumerable<int> fileObjectIds)
        {
            List<Data.File> result = new List<Data.File>();
            if (fileObjectIds.Any() == false)
            {
                // return empty list if no fileObjectIds were given
                return result;
            }

            string firstItemId = "";
            while (true)
            {
                string json = conduit.Query("file.search", 
                                            null,
                                            null,
                                            "newest",
                                            firstItemId);
                JObject fileData = JsonConvert.DeserializeObject(json) as JObject;

                IEnumerable<JObject> files = fileData["result"]["data"].OfType<JObject>();

                if (files.Any() == false) break;

                foreach (JObject file in files)
                {
                    int fileObjectId = Int32.Parse(file["id"].ToString());
                    if (fileObjectIds.Contains(fileObjectId) == false) continue;

                    Data.File newFile = new Data.File();
                    newFile.Token = file["phid"].ToString();
                    newFile.FileName = file["fields"]["name"].ToString();
                    newFile.ID = fileObjectId;
                    newFile.DataStream = null;
                    newFile.Size = Int32.Parse(file["fields"]["size"].ToString());
                    newFile.DateModified = DateTimeOffset.FromUnixTimeSeconds( long.Parse(file["fields"]["dateModified"].ToString()));
                    result.Add(newFile);
                }

                if (fileObjectIds.Count() == result.Count()) break;

                string lastPageId = files.Select(c => c.SelectToken("id")).LastOrDefault().Value<string>();

                firstItemId = lastPageId;
            }

            return result;
        }

        /// <summary>
        /// Downloads a file from Phabricator.
        /// The Phabricator 'file.download' API doesn't work correctly for very big files (causes some out-of-memory exceptions).
        /// A combination of the Phabricator 'file.search' and a 'wget-alike' download are used instead.
        /// The result of the 'file.search' may be raw data but could also be a JSON which contains the dataURI or the direct 
        /// download-link to the file. The content of this dataURI is a JSON file from which the 'result' property contains the 
        /// base64 encoded file content.
        /// </summary>
        /// <param name="conduit"></param>
        /// <param name="fileToken"></param>
        /// <returns></returns>
        public Base64EIDOStream DownloadData(Conduit conduit, string fileToken)
        {
            Base64EIDOStream base64EIDOStream = new Base64EIDOStream();
            string json = conduit.Query("file.search", 
                                        new Constraint[] {
                                            new Constraint("phids", new string[] { fileToken })
                                        });
            JObject fileData = JsonConvert.DeserializeObject(json) as JObject;

            string downloadURL = fileData["result"]["data"][0]["fields"]["dataURI"].ToString();
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(downloadURL);
            webRequest.Method = "GET";
            HttpWebResponse response = (HttpWebResponse)webRequest.GetResponse();
            using (BinaryReader binaryReader = new BinaryReader(response.GetResponseStream()))
            {
                byte[] buffer = binaryReader.ReadBytes(0x400000);
                while (buffer.Length > 0)
                {
                    base64EIDOStream.WriteDecodedData(buffer);
                    buffer = binaryReader.ReadBytes(buffer.Length);
                }
            }

            return base64EIDOStream;
        }

        /// <summary>
        /// Uploads a file to Phabricator
        /// </summary>
        /// <param name="conduit">The Phabricator Conduit API link</param>
        /// <param name="file">File to be uploaded</param>
        /// <returns>The newly created Phabricator file reference id number if success; null if failed</returns>
        public int? Edit(Conduit conduit, Data.File file)
        {
            // allocate file
            JObject conduitResult = conduit.Query("file.allocate", new JObject
                {
                    { "name", file.FileName },
                    { "contentLength", file.DataStream.Length },
                    { "contentHash", file.Hash}
                });

            if ((bool)conduitResult["result"]["upload"])
            {
                string filePHID = (string)conduitResult["result"]["filePHID"];

                // check if file should be upload in chunks
                if (filePHID != null)
                {
                    // upload file in chunks: determine chunks to be uploaded
                    conduitResult = conduit.Query("file.querychunks", new JObject
                    {
                        {"filePHID", filePHID }
                    });

                    JArray chunks = conduitResult["result"] as JArray;
                    if (chunks != null)
                    {
                        // upload each chunk separately
                        IEnumerable<JToken> unpublishedChunks = chunks.Where(c => c["complete"].ToString().Equals("True", StringComparison.OrdinalIgnoreCase) == false);
                        foreach (JToken chunk in unpublishedChunks)
                        {
                            int byteStart = Int32.Parse(chunk["byteStart"].ToString());
                            int byteEnd = Int32.Parse(chunk["byteEnd"].ToString());
                            string base64Data = file.DataStream.ReadEncodedBlock(byteStart, byteEnd - byteStart);
                            conduitResult = conduit.Query("file.uploadchunk", new JObject
                            {
                                { "filePHID", filePHID },
                                { "byteStart", byteStart },
                                { "data", base64Data },
                                { "dataEncoding", "base64" }
                            });
                        }
                    }
                }
                else
                {
                    // upload file in one go instead of uploading it in chunks
                    string fileData = file.DataStream.ReadEncodedBlock(0, (int)file.DataStream.LengthEncodedData);
                    conduitResult = conduit.Query("file.upload", new JObject
                    {
                        { "name", file.FileName },
                        { "data_base64", fileData }
                    });

                    filePHID = (string)conduitResult["result"];
                }

                // determine new phabricator file reference id and return it
                string json = conduit.Query("file.search", 
                                            new Constraint[] {
                                                new Constraint("phids", new string[] { filePHID })
                                            });
                JObject fileSearchResultData = JsonConvert.DeserializeObject(json) as JObject;
                if (fileSearchResultData != null)
                {
                    return Int32.Parse((string)fileSearchResultData["result"]["data"][0]["id"]);
                }
            }

            // no new phabricator file reference created
            return null;
        }
    }
}
