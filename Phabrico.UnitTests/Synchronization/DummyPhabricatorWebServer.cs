using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Miscellaneous;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Phabrico.UnitTests.Synchronization
{
    /// <summary>
    /// Simulates a Phabricator webserver.
    /// It will accept Conduit requests and reply to these requests
    /// </summary>
    public class DummyPhabricatorWebServer
    {
        private HttpListener httpListener;

        /// <summary>
        /// Constructor
        /// </summary>
        public DummyPhabricatorWebServer()
        {
            httpListener = new HttpListener();
            httpListener.Prefixes.Add("http://127.0.0.2:46975/");
            httpListener.Start();

            httpListener.BeginGetContext(ProcessHttpRequest, httpListener);
        }

        /// <summary>
        /// Processes the Conduit parameters from the POST data
        /// If the request does not contain an "after" or a "before" parameter, the content of the corresponding JSON test file 
        /// will be sent back as is.
        /// If the request does not contain these parameters, the result/data from the JSON test file will be emptied before it's
        /// sent back
        /// </summary>
        /// <param name="originalJsonData">Original JSON data</param>
        /// <param name="formVariables">Form variables found in POST data</param>
        /// <returns>Modified JSON data</returns>
        private string ProcessConduitParameters(string originalJsonData, Dictionary<string, string> formVariables)
        {
            string conduitParameters;
            if (formVariables == null || formVariables.Any() == false) return originalJsonData;
            if (formVariables.TryGetValue("params", out conduitParameters) == false) return originalJsonData;
            
            // convert HTTP POST parameters to conduit method parameters
            JObject jsonConduitParameters = JsonConvert.DeserializeObject(conduitParameters) as JObject;
            Dictionary<string,string> methodParameters = jsonConduitParameters.Children().Select(property => property as JProperty).ToDictionary(key => key.Name, value => value.Value?.ToString());

            // check if the next page-result is requested
            bool nextPageRequested = false;
            string pageValue;
            if (methodParameters.TryGetValue("after", out pageValue) || methodParameters.TryGetValue("before", out pageValue))
            {
                if (string.IsNullOrWhiteSpace(pageValue) == false)
                {
                    // next page-result is requested
                    nextPageRequested = true;
                }
            }

            JObject jsonContent = JsonConvert.DeserializeObject(originalJsonData) as JObject;
            if (methodParameters.ContainsKey("order"))
            {
                JToken token;
                if (jsonContent.TryGetValue("result", out token) && token is JObject)
                {
                    JObject jsonResult = token as JObject;
                    if (jsonResult.TryGetValue("data", out token) && token is JArray)
                    {
                        JArray jsonData = token as JArray;

                        if (nextPageRequested)
                        {
                            jsonContent["result"]["data"] = new JArray();
                        }
                        else
                        {
                            if (jsonData.Any() && jsonData.FirstOrDefault()["fields"] != null && jsonData.FirstOrDefault()["fields"]["dateModified"] != null)
                            {
                                if (methodParameters["order"].Equals("oldest"))
                                {
                                    jsonContent["result"]["data"] = new JArray(jsonData.OrderBy(record => (ulong)record["fields"]["dateModified"]).ToArray());
                                }
                                else
                                {
                                    jsonContent["result"]["data"] = new JArray(jsonData.OrderByDescending(record => (ulong)record["fields"]["dateModified"]).ToArray());
                                }
                            }
                        }

                        return JsonConvert.SerializeObject(jsonContent);
                    }
                }
            }

            return originalJsonData;
        }

        /// <summary>
        /// Processes incoming HTTP requests for the dummy Phabricator webserver
        /// </summary>
        /// <param name="asyncResult"></param>
        private void ProcessHttpRequest(IAsyncResult asyncResult)
        {
            System.Net.HttpListenerContext context;

            try
            {
                context = httpListener.EndGetContext(asyncResult);
            }
            catch
            {
                return;
            }

            Task.Factory.StartNew((Object obj) =>
            {
                var data = (dynamic)obj;
                System.Net.HttpListenerContext httpListenerContext = data.context;

                try
                {
                    httpListener.BeginGetContext(ProcessHttpRequest, httpListenerContext);
                    ProcessHttpRequest(httpListenerContext);
                }
                catch
                {
                }
            }, new { context });
        }

        /// <summary>
        /// Processes incoming HTTP messages for the dummy Phabricator webserver
        /// </summary>
        /// <param name="context"></param>
        public void ProcessHttpRequest(System.Net.HttpListenerContext context)
        {
            string url = context.Request.RawUrl;

            // parse commando
            if (context.Request.HttpMethod.StartsWith("GET"))
            {
                // decode requested url from browser
                Debug.WriteLine("GET {0}", (object)url);

                // is incoming message a download request of a file 
                Match matchFileData = RegexSafe.Match(url, "/file/data/[^/]*/PHID-FILE-[^/]*/(.*)", System.Text.RegularExpressions.RegexOptions.None);
                if (matchFileData.Success)
                {
                    string fileName = matchFileData.Groups[1].Value;
                    string resultFileName = "Synchronization\\PhabricatorConduitResults\\Get\\" + fileName;
                    if (System.IO.File.Exists(resultFileName))
                    {
                        byte[] fileData = System.IO.File.ReadAllBytes(resultFileName);

                        // send image
                        context.Response.StatusCode = 200;
                        context.Response.StatusDescription = "OK";
                        context.Response.ContentType = "image/png";
                        context.Response.ContentLength64 = fileData.Length;
                        context.Response.AppendHeader("Accept-Ranges", "bytes");
                        context.Response.AppendHeader("Pragma", "no-cache");
                        context.Response.AppendHeader("Expires", "0");
                        context.Response.OutputStream.Write(fileData, 0, fileData.Length);
                        context.Response.OutputStream.Flush();
                        context.Response.OutputStream.Close();
                    }

                    return;
                }

                Match matchHomepage = RegexSafe.Match(url, "/", System.Text.RegularExpressions.RegexOptions.None);
                if (matchHomepage.Success)
                {
                    // send empty page
                    context.Response.StatusCode = 200;
                    context.Response.StatusDescription = "OK";
                    context.Response.ContentType = "text/html";
                    context.Response.ContentLength64 = 0;
                    context.Response.AppendHeader("Accept-Ranges", "bytes");
                    context.Response.AppendHeader("Pragma", "no-cache");
                    context.Response.AppendHeader("Expires", "0");
                    context.Response.OutputStream.Write(new byte[0], 0, 0);
                    context.Response.OutputStream.Flush();
                    context.Response.OutputStream.Close();
                }

                return;
            }

            if (context.Request.HttpMethod.StartsWith("POST"))
            {
                // decode requested url from browser
                Debug.WriteLine("POST {0}", (object)context.Request.RawUrl);

                // parse url
                if (url.StartsWith("/")) url = url.Substring("/".Length);
                if (url.StartsWith("api/")) url = url.Substring("api/".Length);

                // read POST data
                string rcvBuffer;
                using (StreamReader streamReader = new StreamReader(context.Request.InputStream))
                {
                    rcvBuffer = streamReader.ReadToEnd();
                }

                Dictionary<string,string> formVariables = new Dictionary<string, string>();
                if (context.Request.ContentType.Equals("application/x-www-form-urlencoded"))
                {
                    formVariables = rcvBuffer.Split('&')
                                             .ToDictionary(key => key.Split('=')[0],
                                                           value => HttpUtility.UrlDecode(value.Substring(value.IndexOf('=') + 1)));
                }

                if (context.Request.ContentType.StartsWith("multipart/form-data"))
                {
                    throw new NotImplementedException();
                }

                if (context.Request.ContentType.Equals("application/octet-stream"))
                {
                    throw new NotImplementedException();
                }

                // process POST request
                string resultFileName = "Synchronization\\PhabricatorConduitResults\\Post\\" + url + ".json";
                if (System.IO.File.Exists(resultFileName))
                {
                    string jsonData = System.IO.File.ReadAllText(resultFileName);

                    jsonData = ProcessConduitParameters(jsonData, formVariables);

                    context.Response.StatusCode = 200;
                    context.Response.StatusDescription = "OK";
                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength64 = jsonData.Length;
                    context.Response.AppendHeader("Accept-Ranges", "bytes");
                    context.Response.AppendHeader("Pragma", "no-cache, no-store, must-revalidate");
                    context.Response.AppendHeader("Expires", "0");
                    context.Response.OutputStream.Write(UTF8Encoding.UTF8.GetBytes(jsonData), 0, jsonData.Length);
                    context.Response.OutputStream.Flush();
                    context.Response.OutputStream.Close();
                    return;
                }

                // invalid POST url received
                throw new ArgumentException(url);
            }

            // invalid HTTP command received
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Stops the dummy Phabricator webserver
        /// </summary>
        public void Stop()
        {
            httpListener.Stop();
        }
    }
}
