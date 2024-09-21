using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Http;
using Phabrico.Miscellaneous;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;

namespace Phabrico.Phabricator.API
{
    /// <summary>
    /// Represents the Conduit communinication class for communicating with Phabricator
    /// </summary>
    public class Conduit
    {
        /// <summary>
        /// Exception class for handling communication issues with Phabricator
        /// </summary>
        public class PhabricatorException : System.Exception
        {
            /// <summary>
            /// Error code from Phabricator
            /// </summary>
            public string ErrorCode { get; set; }

            /// <summary>
            /// Detailed error information
            /// </summary>
            public string ErrorInfo { get; set; }

            /// <summary>
            /// The request data that caused the exception
            /// </summary>
            public string Request { get; set; }

            /// <summary>
            /// Initializes an Exception object
            /// </summary>
            /// <param name="errorCode"></param>
            /// <param name="errorInfo"></param>
            /// <param name="request"></param>
            public PhabricatorException(string errorCode, string errorInfo, string request)
            {
                ErrorCode = errorCode;
                ErrorInfo = errorInfo;
                Request = request;
            }

            /// <summary>
            /// Returns a readable description
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return string.Format("{0}: {1}", ErrorCode, ErrorInfo);
            }

            /// <summary>
            /// Returns a readable description
            /// </summary>
            public override string Message
            {
                get
                {
                    return this.ToString();
                }
            }
        }

        /// <summary>
        /// Array of Conduit API names which can be executed
        /// </summary>
        private static string[] availableConduitAPIs { get; set; } = null;

        /// <summary>
        /// The URL where the Phabricator server is located
        /// </summary>
        public string PhabricatorUrl { get; set; }

        /// <summary>
        /// Conduit API token for communicating with Phabricator
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Initializes a Conduit object
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        public Conduit(Http.Server httpServer, Http.Browser browser)
        {
            SessionManager.Token token = browser.Token;
            if (token != null)
            {
                string encryptionKey = token.EncryptionKey;
                if (encryptionKey != null)
                {
                    using (Storage.Database database = new Storage.Database(encryptionKey))
                    {
                        database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                        Storage.Account accountStorage = new Storage.Account();

                        Data.Account currentAccount = accountStorage.Get(database, token);
                        if (currentAccount != null)
                        {
                            PhabricatorUrl = currentAccount.PhabricatorUrl;
                            Token = currentAccount.ConduitAPIToken;
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Returns the current timestamp of the Phabricator server
        /// </summary>
        /// <returns></returns>
        public DateTime GetTimestampPhabricatorServer()
        {
            DateTime result;

            // The DateTime.TryParseExact() .NET API can not work with TimeZone names.
            // It can only work with numeric TimeZones. This array translates a TimeZone name into a numeric TimeZone.
            string[][] TimeZones = new string[][] {
                new string[] {"ACDT", "+1030", "Australian Central Daylight"},
                new string[] {"ACST", "+0930", "Australian Central Standard"},
                new string[] {"ADT", "-0300", "(US) Atlantic Daylight"},
                new string[] {"AEDT", "+1100", "Australian East Daylight"},
                new string[] {"AEST", "+1000", "Australian East Standard"},
                new string[] {"AHDT", "-0900", ""},
                new string[] {"AHST", "-1000", ""},
                new string[] {"AST", "-0400", "(US) Atlantic Standard"},
                new string[] {"AT", "-0200", "Azores"},
                new string[] {"AWDT", "+0900", "Australian West Daylight"},
                new string[] {"AWST", "+0800", "Australian West Standard"},
                new string[] {"BAT", "+0300", "Bhagdad"},
                new string[] {"BDST", "+0200", "British Double Summer"},
                new string[] {"BET", "-1100", "Bering Standard"},
                new string[] {"BST", "-0300", "Brazil Standard"},
                new string[] {"BT", "+0300", "Baghdad"},
                new string[] {"BZT2", "-0300", "Brazil Zone 2"},
                new string[] {"CADT", "+1030", "Central Australian Daylight"},
                new string[] {"CAST", "+0930", "Central Australian Standard"},
                new string[] {"CAT", "-1000", "Central Alaska"},
                new string[] {"CCT", "+0800", "China Coast"},
                new string[] {"CDT", "-0500", "(US) Central Daylight"},
                new string[] {"CED", "+0200", "Central European Daylight"},
                new string[] {"CET", "+0100", "Central European"},
                new string[] {"CST", "-0600", "(US) Central Standard"},
                new string[] {"EAST", "+1000", "Eastern Australian Standard"},
                new string[] {"EDT", "-0400", "(US) Eastern Daylight"},
                new string[] {"EED", "+0300", "Eastern European Daylight"},
                new string[] {"EET", "+0200", "Eastern Europe"},
                new string[] {"EEST", "+0300", "Eastern Europe Summer"},
                new string[] {"EST", "-0500", "(US) Eastern Standard"},
                new string[] {"FST", "+0200", "French Summer"},
                new string[] {"FWT", "+0100", "French Winter"},
                new string[] {"GMT", "-0000", "Greenwich Mean"},
                new string[] {"GST", "+1000", "Guam Standard"},
                new string[] {"HDT", "-0900", "Hawaii Daylight"},
                new string[] {"HST", "-1000", "Hawaii Standard"},
                new string[] {"IDLE", "+1200", "Internation Date Line East"},
                new string[] {"IDLW", "-1200", "Internation Date Line West"},
                new string[] {"IST", "+0530", "Indian Standard"},
                new string[] {"IT", "+0330", "Iran"},
                new string[] {"JST", "+0900", "Japan Standard"},
                new string[] {"JT", "+0700", "Java"},
                new string[] {"MDT", "-0600", "(US) Mountain Daylight"},
                new string[] {"MED", "+0200", "Middle European Daylight"},
                new string[] {"MET", "+0100", "Middle European"},
                new string[] {"MEST", "+0200", "Middle European Summer"},
                new string[] {"MEWT", "+0100", "Middle European Winter"},
                new string[] {"MST", "-0700", "(US) Mountain Standard"},
                new string[] {"MT", "+0800", "Moluccas"},
                new string[] {"NDT", "-0230", "Newfoundland Daylight"},
                new string[] {"NFT", "-0330", "Newfoundland"},
                new string[] {"NT", "-1100", "Nome"},
                new string[] {"NST", "+0630", "North Sumatra"},
                new string[] {"NZ", "+1100", "New Zealand "},
                new string[] {"NZST", "+1200", "New Zealand Standard"},
                new string[] {"NZDT", "+1300", "New Zealand Daylight"},
                new string[] {"NZT", "+1200", "New Zealand"},
                new string[] {"PDT", "-0700", "(US) Pacific Daylight"},
                new string[] {"PST", "-0800", "(US) Pacific Standard"},
                new string[] {"ROK", "+0900", "Republic of Korea"},
                new string[] {"SAD", "+1000", "South Australia Daylight"},
                new string[] {"SAST", "+0900", "South Australia Standard"},
                new string[] {"SAT", "+0900", "South Australia Standard"},
                new string[] {"SDT", "+1000", "South Australia Daylight"},
                new string[] {"SST", "+0200", "Swedish Summer"},
                new string[] {"SWT", "+0100", "Swedish Winter"},
                new string[] {"USZ3", "+0400", "USSR Zone 3"},
                new string[] {"USZ4", "+0500", "USSR Zone 4"},
                new string[] {"USZ5", "+0600", "USSR Zone 5"},
                new string[] {"USZ6", "+0700", "USSR Zone 6"},
                new string[] {"UT", "-0000", "Universal Coordinated"},
                new string[] {"UTC", "-0000", "Universal Coordinated"},
                new string[] {"UZ10", "+1100", "USSR Zone 10"},
                new string[] {"WAT", "-0100", "West Africa"},
                new string[] {"WET", "-0000", "West European"},
                new string[] {"WST", "+0800", "West Australian Standard"},
                new string[] {"YDT", "-0800", "Yukon Daylight"},
                new string[] {"YST", "-0900", "Yukon Standard"},
                new string[] {"ZP4", "+0400", "USSR Zone 3"},
                new string[] {"ZP5", "+0500", "USSR Zone 4"},
                new string[] {"ZP6", "+0600", "USSR Zone 5"}
            };

            // browse to the Phabricator home page
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(PhabricatorUrl);
            webRequest.Method = "GET";
            webRequest.Timeout = 5000;
            HttpWebResponse httpWebResponse = webRequest.GetResponse() as HttpWebResponse;
            if (httpWebResponse != null)
            {
                // get timestamp from HTTP header
                string rfc2616DateTime = httpWebResponse.Headers[HttpResponseHeader.Date];

                // convert HTTP date string into a DateTime
                string regWkDay = "(Mon|Tue|Wed|Thu|Fri|Sat|Sun)";
                string regWeekDay = "(Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)";
                string regMonth = "(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)";

                // try allowed HTTP datetime format 1  (e.g.   Sun, 06 Nov 1994 08:49:37 GMT  ; RFC 822, updated by RFC 1123)
                System.Text.RegularExpressions.Match match = RegexSafe.Match(rfc2616DateTime, 
                                                                             string.Format("{0}, *([0-9]?[0-9]) +{1} +([0-9][0-9][0-9][0-9]) +([0-9][0-9]):([0-9][0-9]):([0-9][0-9]) +([^; \t\r\n]*)",
                                                                                           regWkDay, regMonth
                                                                                          ),
                                                                             System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string formattedDateTimeString = string.Format("{0}-{1}-{2} {3}:{4}:{5} {6}",
                                                            match.Groups[4],
                                                            match.Groups[3],
                                                            match.Groups[2],
                                                            match.Groups[5],
                                                            match.Groups[6],
                                                            match.Groups[7],
                                                            (TimeZones.FirstOrDefault(tz => tz[0].Equals(match.Groups[8].Value)) ?? new string[] { "", match.Groups[8].Value })[1]
                                                        );
                    if (DateTime.TryParseExact(formattedDateTimeString,
                                               "yyyy-MMM-dd HH:mm:ss K",
                                               System.Globalization.CultureInfo.InvariantCulture,
                                               System.Globalization.DateTimeStyles.AssumeLocal,
                                               out result))
                    {
                        return result;
                    }
                }


                // try allowed HTTP datetime format 2  (e.g.   Sunday, 06-Nov-94 08:49:37 GMT ; RFC 850, obsoleted by RFC 1036)
                match = RegexSafe.Match(rfc2616DateTime, 
                                        string.Format("{0}, *([0-9]?[0-9])-{1}-([0-9][0-9]) +([0-9][0-9]):([0-9][0-9]):([0-9][0-9]) +([^; \t\r\n]*)",
                                                      regWeekDay, regMonth
                                                     ),
                                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string formattedDateTimeString = string.Format("{0}-{1}-{2} {3}:{4}:{5} {6}",
                                                            match.Groups[4],
                                                            match.Groups[3],
                                                            match.Groups[2],
                                                            match.Groups[5],
                                                            match.Groups[6],
                                                            match.Groups[7],
                                                            (TimeZones.FirstOrDefault(tz => tz[0].Equals(match.Groups[8].Value)) ?? new string[] { "", match.Groups[8].Value })[1]
                                                        );
                    if (DateTime.TryParseExact(formattedDateTimeString,
                                               "yy-MMM-dd HH:mm:ss K",
                                               System.Globalization.CultureInfo.InvariantCulture,
                                               System.Globalization.DateTimeStyles.AssumeLocal,
                                               out result))
                    {
                        return result;
                    }
                }


                // try allowed HTTP datetime format 3  (e.g.   Sun Nov  6 08:49:37 1994       ; ANSI C's asctime() format)
                match = RegexSafe.Match(rfc2616DateTime, 
                                        string.Format("{0} *{1} +([0-9]?[0-9]) +([0-9][0-9]):([0-9][0-9]):([0-9][0-9]) +([0-9][0-9][0-9][0-9])",
                                                      regWkDay, regMonth
                                                     ),
                                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string formattedDateTimeString = string.Format("{0}-{1}-{2} {3}:{4}:{5} {6}",
                                                            match.Groups[7],
                                                            match.Groups[2],
                                                            match.Groups[3],
                                                            match.Groups[4],
                                                            match.Groups[5],
                                                            match.Groups[6],
                                                            (TimeZones.FirstOrDefault(tz => tz[0].Equals(match.Groups[8].Value)) ?? new string[] { "", match.Groups[8].Value })[1]
                                                        );
                    if (DateTime.TryParseExact(formattedDateTimeString,
                                               "yyyy-MMM-d HH:mm:ss K",
                                               System.Globalization.CultureInfo.InvariantCulture,
                                               System.Globalization.DateTimeStyles.AssumeLocal,
                                               out result))
                    {
                        return result;
                    }
                }
            }

            // unable to retrieve datetime from HTTP headers: return current datetime
            return DateTime.Now;
        }

        /// <summary>
        /// Verifies if some give Conduit API method exists on the Phabricator/Phorge instance
        /// </summary>
        /// <param name="conduitAPIName"></param>
        /// <returns></returns>
        public bool APIExists(string conduitAPIName)
        {
            if (availableConduitAPIs == null)
            {
                try
                {
                    string json = Query("conduit.query");
                    JObject accessibleAPIMethods = JsonConvert.DeserializeObject(json) as JObject;
                    availableConduitAPIs = accessibleAPIMethods["result"]
                        .OfType<JProperty>()
                        .Select(token => token.Name)
                        .ToArray();
                }
                catch
                {
                }
            }

            if (availableConduitAPIs == null)
            {
                return false;
            }

            return availableConduitAPIs.Contains(conduitAPIName);
        }

        /// <summary>
        /// Handles the response of a Conduit API request.
        /// </summary>
        /// <param name="jsonResponse"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        private string ProcessConduitResponse(string jsonResponse, string request)
        {
            JObject conduitResult = JsonConvert.DeserializeObject(jsonResponse) as JObject;
            if (conduitResult == null)
            {
                throw new Conduit.PhabricatorException("Invalid response from Phabricator", "Invalid response from Phabricator", request);
            }

            if (conduitResult["error_code"].Type != JTokenType.Null)
            {
                string errorCode = (string)conduitResult["error_code"];
                string errorDescription = (string)conduitResult["error_info"];

                throw new Conduit.PhabricatorException(errorCode, errorDescription, request);
            }

            return jsonResponse;
        }

        /// <summary>
        /// Downloads object information from the Phabricator server
        /// </summary>
        /// <param name="url">Conduit URL to be used</param>
        /// <param name="constraints">Constraints to be used in the Conduit API call</param>
        /// <param name="attachments">Attachments to be used in the Conduit API call</param>
        /// <param name="order">Order of result items from the Conduit API call</param>
        /// <param name="firstItemId">First item to show in result items from the Conduit API call</param>
        /// <returns>JSON array of JSON objects</returns>
        public string Query(string url, Constraint[] constraints = null, Attachment[] attachments = null, string order = null, string firstItemId = null)
        {
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(string.Format("{0}/api/{1}", PhabricatorUrl, url));
            webRequest.Method = "POST";
            webRequest.Timeout = 60000;
            webRequest.ContentType = "application/x-www-form-urlencoded";

            Logging.WriteInfo(null, "<Conduit> {0}", url);

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters["__conduit__"] = new { token = Token };

            if (constraints != null)
            {
                parameters["constraints"] = Constraint.ToObject(constraints);
            }

            if (attachments != null)
            {
                parameters["attachments"] = Attachment.ToObject(attachments);
            }

            if (order != null)
            {
                parameters["order"] = order;
            }

            if (firstItemId != null)
            {
                parameters["after"] = firstItemId;
            }
            
            string jsonData = JsonConvert.SerializeObject(parameters);
            string indentedJsonData = JsonConvert.SerializeObject(parameters, Formatting.Indented);
            string postData = string.Format("params={0}&format=json&__conduit__=1", Uri.EscapeDataString(jsonData));

            using (var stream = webRequest.GetRequestStream())
            {
                using (StreamWriter streamWriter = new StreamWriter(stream))
                {
                    streamWriter.Write(postData);
                }
            }

            HttpWebResponse response = (HttpWebResponse)webRequest.GetResponse();
            using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
            {
                string request = url + ":\r\n" + indentedJsonData;
                return ProcessConduitResponse(streamReader.ReadToEnd(), request);
            }
        }

        /// <summary>
        /// Downloads information of a specifc object from the Phabricator server
        /// </summary>
        /// <param name="url">Conduit API method URL to be used</param>
        /// <param name="jsonObject">Parameters to be executed with the Conduit API method</param>
        /// <returns>JSON array of JSON objects</returns>
        public JObject Query(string url, JObject jsonObject)
        {
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(string.Format("{0}/api/{1}", PhabricatorUrl, url));
            webRequest.Method = "POST";
            webRequest.Timeout = 60000;
            webRequest.ContentType = "application/x-www-form-urlencoded";

            Logging.WriteInfo(null, "<Conduit> {0}", url);

            string originalJsonData = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);

            // overwrite/add token to jsonObject
            jsonObject.Remove("__conduit__");
            jsonObject.Add(new JProperty("__conduit__", new JObject(new JProperty("token", Token))));

            string jsonData = JsonConvert.SerializeObject(jsonObject);
            string postData = string.Format("params={0}&format=json&__conduit__=1", HttpUtility.UrlEncode(jsonData));

            using (var stream = webRequest.GetRequestStream())
            {
                using (StreamWriter streamWriter = new StreamWriter(stream))
                {
                    streamWriter.Write(postData);
                }
            }

            HttpWebResponse response = (HttpWebResponse)webRequest.GetResponse();
            using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
            {
                string request = url + ":\r\n" + originalJsonData;
                string json = ProcessConduitResponse(streamReader.ReadToEnd(), request);
                return JsonConvert.DeserializeObject(json) as JObject;
            }
        }
    }
}
