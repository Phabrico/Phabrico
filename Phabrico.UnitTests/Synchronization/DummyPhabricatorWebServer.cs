using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Phabrico.Miscellaneous;

namespace Phabrico.UnitTests.Synchronization
{
    public class DummyPhabricatorWebServer
    {
        private TcpListener tcpListener;
        private bool stopAllBrowserConnectionThreads;
        
        public DummyPhabricatorWebServer()
        {

            tcpListener = new TcpListener(System.Net.IPAddress.Parse("127.0.0.2"), 46975);
            tcpListener.Start();

            Thread thrListener = new Thread(this.ListenerThread);
            thrListener.Start();
        }

        public void Stop()
        {
            stopAllBrowserConnectionThreads = true;
            tcpListener.Stop();
        }

        private void ListenerThread()
        {
            Socket browser = null;
            byte[] rcvBuffer = new byte[32767];

            // wait for connection
            while (!this.stopAllBrowserConnectionThreads)
            {
                if (tcpListener.Pending())
                {
                    browser = tcpListener.AcceptSocket();
                    browser.ReceiveBufferSize = rcvBuffer.Length;
                    browser.ReceiveTimeout = 60000;

                    Thread thrListener = new Thread(this.ListenerThread);
                    thrListener.Start();
                    break;
                }
            }

            while (stopAllBrowserConnectionThreads == false)
            {
                // has browser sent any data ?
                if (browser.Available > 0)
                {
                    // peek into socketbuffer
                    int bytesRead = browser.Receive(rcvBuffer, 0, browser.Available, SocketFlags.Peek);
                    if (bytesRead > 0)
                    {
                        // is ETX available ?
                        int posETX = bytesRead - 4;
                        for (; posETX >= 0; posETX--)
                        {
                            if (rcvBuffer[posETX + 0] == '\r' &&
                                rcvBuffer[posETX + 1] == '\n' &&
                                rcvBuffer[posETX + 2] == '\r' &&
                                rcvBuffer[posETX + 3] == '\n')
                            {
                                break;
                            }
                        }

                        if (posETX >= 0)
                        {
                            // read data between STX and ETX out of socket buffer (STX and ETX included)
                            bytesRead = browser.Receive(rcvBuffer, 0, 4 + posETX, SocketFlags.None);

                            // convert bytes between STX and ETX to readable string
                            string httpMessage;
                            bool useUTF7 = rcvBuffer.Take(posETX).Any(c => c > 127);
                            if (useUTF7)
                            {
                                httpMessage = Encoding.UTF7.GetString(rcvBuffer, 0, posETX);
                            }
                            else
                            {
                                httpMessage = Encoding.UTF8.GetString(rcvBuffer, 0, posETX);
                            }

                            // parse commando
                            if (httpMessage.StartsWith("GET"))
                            {
                                // decode requested url from browser
                                var httpParameters = httpMessage.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                string url = httpParameters.Where(httpParameter => httpParameter.StartsWith("GET "))
                                                           .Select(httpGetParameter => httpGetParameter.Substring("GET ".Length))
                                                           .Select(httpGetParameter => httpGetParameter.Substring(0, httpGetParameter.LastIndexOf("HTTP") - 1))
                                                           .FirstOrDefault();

                                Match matchFileData = RegexSafe.Match(url, "/file/data/[^/]*/PHID-FILE-[^/]*/(.*)", System.Text.RegularExpressions.RegexOptions.None);
                                if (matchFileData.Success)
                                {
                                    string fileName = matchFileData.Groups[1].Value;
                                    string resultFileName = "Synchronization\\PhabricatorConduitResults\\Get\\" + fileName;
                                    if (System.IO.File.Exists(resultFileName))
                                    {
                                        byte[] fileData = System.IO.File.ReadAllBytes(resultFileName);

                                        int httpStatusCode = 200;
                                        string httpStatusMessage = "OK";
                                        string contentType = "image/png";
                                        int contentLength = fileData.Length;
                                        string acceptRanges = "bytes";

                                        string tcpSocketResponseData = string.Format("HTTP/1.1 {0} {1}\r\n" +
                                                                                  "Content-Type: {2}\r\n" +
                                                                                  "Content-Length: {3}\r\n" +
                                                                                  "Accept-Ranges: {4}\r\n" +
                                                                                  "Cache-Control: no-cache, no-store, must-revalidate\r\n" +
                                                                                  "Pragma: no-cache\r\n" +
                                                                                  "Expires: 0\r\n" +
                                                                                  "\r\n",
                                                                                  httpStatusCode, httpStatusMessage,
                                                                                  contentType,
                                                                                  contentLength,
                                                                                  acceptRanges);

                                        browser.SendBufferSize = tcpSocketResponseData.Length + contentLength;
                                        browser.Send(UTF8Encoding.UTF8.GetBytes(tcpSocketResponseData));
                                        browser.Send(fileData);
                                    }
                                }

                                Match matchHomepage = RegexSafe.Match(url, "/", System.Text.RegularExpressions.RegexOptions.None);
                                if (matchHomepage.Success)
                                {
                                    int httpStatusCode = 200;
                                    string httpStatusMessage = "OK";
                                    string contentType = "text/html";
                                    int contentLength = 0;
                                    string acceptRanges = "bytes";

                                    string tcpSocketResponseData = string.Format("HTTP/1.1 {0} {1}\r\n" +
                                                                              "Content-Type: {2}\r\n" +
                                                                              "Content-Length: {3}\r\n" +
                                                                              "Accept-Ranges: {4}\r\n" +
                                                                              "Date: Sun, 25 Oct 2020 15:49:37 GMT\r\n" +
                                                                              "Cache-Control: no-cache, no-store, must-revalidate\r\n" +
                                                                              "Pragma: no-cache\r\n" +
                                                                              "Expires: 0\r\n" +
                                                                              "\r\n",
                                                                              httpStatusCode, httpStatusMessage,
                                                                              contentType,
                                                                              contentLength,
                                                                              acceptRanges);

                                    browser.SendBufferSize = tcpSocketResponseData.Length + contentLength;
                                    browser.Send(UTF8Encoding.UTF8.GetBytes(tcpSocketResponseData));
                                }

                                continue;
                            }

                            if (httpMessage.StartsWith("POST"))
                            {
                                // decode requested url from browser
                                var httpParameters = httpMessage.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                string url = httpParameters.Where(httpParameter => httpParameter.StartsWith("POST "))
                                                          .Select(httpGetParameter => httpGetParameter.Substring("POST ".Length))
                                                          .Select(httpGetParameter => httpGetParameter.Substring(0, httpGetParameter.LastIndexOf("HTTP") - 1))
                                                          .FirstOrDefault();

                                if (url.StartsWith("/")) url = url.Substring("/".Length);
                                if (url.StartsWith("api/")) url = url.Substring("api/".Length);

                                Dictionary<string,string> formVariables;
                                byte[] binaryData;
                                if (ProcessPostParameters(browser, httpMessage, httpParameters, out formVariables, out binaryData))
                                {
                                    string resultFileName = "Synchronization\\PhabricatorConduitResults\\Post\\" + url + ".json";
                                    if (System.IO.File.Exists(resultFileName))
                                    {
                                        string originalJsonData = System.IO.File.ReadAllText(resultFileName);
                                        string jsonData = ProcessConduitParameters(originalJsonData, formVariables);

                                        int httpStatusCode = 200;
                                        string httpStatusMessage = "OK";
                                        string contentType = "application/json";
                                        int contentLength = jsonData.Length;
                                        string acceptRanges = "bytes";

                                        string tcpSocketResponseData = string.Format("HTTP/1.1 {0} {1}\r\n" +
                                                                                  "Content-Type: {2}\r\n" +
                                                                                  "Content-Length: {3}\r\n" +
                                                                                  "Accept-Ranges: {4}\r\n" +
                                                                                  "Cache-Control: no-cache, no-store, must-revalidate\r\n" +
                                                                                  "Pragma: no-cache\r\n" +
                                                                                  "Expires: 0\r\n" +
                                                                                  "\r\n" +
                                                                                  "{5}",
                                                                                  httpStatusCode, httpStatusMessage,
                                                                                  contentType,
                                                                                  contentLength,
                                                                                  acceptRanges,
                                                                                  jsonData);

                                        browser.SendBufferSize = tcpSocketResponseData.Length;
                                        browser.Send(UTF8Encoding.UTF8.GetBytes(tcpSocketResponseData));

                                        // wait a while before closing the connection so the browser has enough time to read the answer
                                        Thread.Sleep(250);
                                        continue;
                                    }
                                }

                                // invalid POST url received
                                throw new ArgumentException(url);
                            }

                            // invalid HTTP command received
                            throw new InvalidOperationException();
                        }
                    }
                }

                Thread.Sleep(50);
            }
        }

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

        private bool ProcessPostParameters(Socket browser, string httpMessage, string[] httpParameters, out Dictionary<string,string> formVariables, out byte[] binaryData)
        {
            formVariables = new Dictionary<string, string>();
            binaryData = null;

            string contentType = httpParameters.FirstOrDefault(httpParameter => httpParameter.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(contentType))
            {
                // invalid content
                return false;
            }
            else
            {
                contentType = contentType.Substring("Content-Type:".Length).Trim();
            }

            int contentLength=0;
            string contentLengthParameter = httpParameters.FirstOrDefault(httpParameter => httpParameter.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(contentLengthParameter) ||
                Int32.TryParse(contentLengthParameter.Substring("Content-Length:".Length).Trim(), out contentLength) == false ||
                (contentLength > browser.Available && contentType.Equals("application/x-www-form-urlencoded")))
            {
                // check if the remaining data is still being sent
                for (int i = 0; i < 10; i++)
                {
                    if (contentLength <= browser.Available) break;
                    Thread.Sleep(250);
                }

                if (string.IsNullOrEmpty(contentLengthParameter) ||
                    Int32.TryParse(contentLengthParameter.Substring("Content-Length:".Length).Trim(), out contentLength) == false ||
                    (contentLength > browser.Available && contentType.Equals("application/x-www-form-urlencoded")))
                {
                    // invalid content
                    return false;
                }
            }

            // read POST parameters
            if (contentLength > 0)
            {
                byte[] rcvBuffer = new byte[32767];
                int bytesRead = browser.Receive(rcvBuffer, 0, contentLength, SocketFlags.None);

                if (contentType.Equals("application/x-www-form-urlencoded"))
                {
                    formVariables = UTF8Encoding.UTF8.GetString(rcvBuffer.Take(bytesRead).ToArray())
                                                   .Split('&')
                                                   .ToDictionary(key => key.Split('=')[0],
                                                                 value => HttpUtility.UrlDecode(value.Substring(value.IndexOf('=') + 1)));
                }

                if (contentType.StartsWith("multipart/form-data"))
                {
                    httpMessage = httpMessage + "\r\n\r\n" + UTF8Encoding.UTF8.GetString(rcvBuffer.Take(bytesRead).ToArray());

                    string mimeSeparator = httpMessage.Split(new string[] { "\r\n" }, StringSplitOptions.None).FirstOrDefault(line => line.StartsWith("---"));
                    string[] mimeParts = httpMessage.Split(new string[] { mimeSeparator }, StringSplitOptions.None).Skip(1).ToArray();
                    formVariables = mimeParts.Select(part => part.Trim('\r', '\n'))
                                               .Where(part => RegexSafe.IsMatch(part.Split('\r', '\n').FirstOrDefault(), "(^|; )name=\"[^\"]*\"", System.Text.RegularExpressions.RegexOptions.None))
                                               .ToDictionary(key => RegexSafe.Match(key, "name=\"([^\"]*)\"", System.Text.RegularExpressions.RegexOptions.None).Groups[1].Value,
                                                             value => value.IndexOf("\r\n\r\n") == -1
                                                                        ? ""
                                                                        : value.Substring(value.IndexOf("\r\n\r\n") + "\r\n\r\n".Length)
                                                            );
                }

                if (contentType.Equals("application/octet-stream"))
                {
                    binaryData = rcvBuffer.Take(bytesRead).ToArray();
                }
            }

            return true;
        }
    }
}
