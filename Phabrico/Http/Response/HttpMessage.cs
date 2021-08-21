using Phabrico.Parsers.Base64;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Phabrico.Http.Response
{
    /// <summary>
    /// Represents a basic HTTP response
    /// </summary>
    abstract public class HttpMessage
    {
        private static Dictionary<string,string> cachedViewData = new Dictionary<string, string>();

        /// <summary>
        /// List of view names which can not be accessed directly
        /// </summary>
        private static string[] internalViewNames = new string[] {
            "AccessDenied",
            "BrowserNotSupported",
            "HomePage.Authenticated.HeaderActions",
            "HomePage.Authenticated",
            "HomePage.AuthenticationDialog",
            "HomePage.AuthenticationDialogCreateUser",
            "HomePage.IFrameContent",
            "HomePage.NoHeaderLocalTreeView.Template",
            "HomePage.NoHeaderTreeView.Template",
            "HomePage.NoTreeView.Template",
            "HomePage.Template",
            "HomePage.TreeView.Template",
            "HttpNotFound",
            "ManiphestTask",
            "ManiphestTaskEdit",
            "PhrictionEdit",
            "PhrictionHierarchy",
            "PhrictionNoDocumentFound",
            "Staging",
            "StagingDiff",
            "SynchronizationDiff",
        };

        /// <summary>
        /// HTTP marker used by the server to advertise its support of partial requests
        /// </summary>
        public string AcceptRanges { get; set; }

        /// <summary>
        /// Reference to browser object (i.e. session info and stuff)
        /// </summary>
        public Browser Browser { get; set; }

        /// <summary>
        /// Returns the CharSet of the message content
        /// </summary>
        public string CharSet { get; set; }

        /// <summary>
        /// Content of the message
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Length of the message content
        /// </summary>
        public long ContentLength { get; set; }

        /// <summary>
        /// Returns the MIME content type
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// If true, the HTTP Message can be cached by the browser and a second call to the same object may
        /// not require any action from Phabrico
        /// </summary>
        public bool EnableBrowserCache { get; set; } = true;

        /// <summary>
        /// if true, the content will be translated to the language of the user
        /// </summary>
        protected bool DoTranslateContent { get; set; } = true;

        /// <summary>
        /// Returns the name of the file in case the message represent a file
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// References the Phabrico web server
        /// </summary>
        public Http.Server HttpServer { get; set; }

        /// <summary>
        /// Returns the HTTP status code
        /// </summary>
        public int HttpStatusCode { get; set; }

        /// <summary>
        /// Returns the HTTP status description
        /// </summary>
        public string HttpStatusMessage { get; set; }

        /// <summary>
        /// True if the message represents a file
        /// </summary>
        public bool IsAttachment { get; set; } = false;

        /// <summary>
        /// UTC Timestamp when the message was last sent to a browser.
        /// This is mainly used in the caching functionality
        /// </summary>
        public DateTime TimestampSent { get; set; }

        /// <summary>
        /// Returns the HTTP URL
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Initializes a new HttpMessage object
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="url"></param>
        internal HttpMessage(Http.Server httpServer, Browser browser, string url)
        {
            HttpServer = httpServer;
            Browser = browser;
            HttpStatusCode = 200;
            HttpStatusMessage = "OK";
            ContentType = "text/html";
            CharSet = "UTF-8";
            ContentLength = 0;
            AcceptRanges = "bytes";
            Url = url;
            TimestampSent = DateTime.UtcNow;
        }

        /// <summary>
        /// Returns the HTML of a given view
        /// </summary>
        /// <param name="viewName"></param>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public string GetViewData(string viewName, Assembly assembly = null)
        {
            // in case url has parameters, skip parameters
            viewName = viewName.Split('?').FirstOrDefault();

            // check if we can get a cached version of the view
            string cacheKey = Browser.Session.Locale + assembly?.FullName + viewName;
            string cachedView;
            if (cachedViewData.TryGetValue(cacheKey, out cachedView))
            {
                return cachedView;
            }

            // load view
            if (assembly == null)
            {
                assembly = Assembly.GetExecutingAssembly();
            }

            string resourceName = string.Format("Phabrico.View.{0}.html", viewName);
            resourceName = assembly.GetManifestResourceNames().FirstOrDefault(name => name.Equals(resourceName, System.StringComparison.OrdinalIgnoreCase));
            if (resourceName == null)
            {
                resourceName = string.Format("Phabrico.View.{0}", viewName);
                resourceName = assembly.GetManifestResourceNames().FirstOrDefault(name => name.Equals(resourceName, System.StringComparison.OrdinalIgnoreCase));
            }

            // standard view not found -> check for localized views
            if (resourceName == null)
            {
                resourceName = string.Format("Phabrico.Locale.{0}", viewName);
                resourceName = assembly.GetManifestResourceNames().FirstOrDefault(name => name.Equals(resourceName, System.StringComparison.OrdinalIgnoreCase));
            }

            // check if view exists in plugin
            if (resourceName == null)
            {
                resourceName = viewName;
                resourceName = assembly.GetManifestResourceNames().FirstOrDefault(name => name.Equals(resourceName, System.StringComparison.OrdinalIgnoreCase));
            }

            if (resourceName == null)
            {
                foreach (Plugin.PluginBase plugin in Http.Server.Plugins)
                {
                    resourceName = plugin.Assembly.GetManifestResourceNames().FirstOrDefault(name => name.Equals("Phabrico.Plugin.View." + viewName + ".html", System.StringComparison.OrdinalIgnoreCase));
                    if (resourceName != null)
                    {
                        assembly = plugin.Assembly;
                        break;
                    }
                }
            }

            if (resourceName == null)
            {
                // should not happen
                cachedViewData[cacheKey] = "ERROR: " + viewName + " was not found";
            }
            else
            {
                // translate HTML content
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string content = reader.ReadToEnd();
                        if (DoTranslateContent == false)
                        {
                            cachedViewData[cacheKey] = content;
                        }

                        string locale = Browser.Session.Locale;
                        if (locale == null)
                        {
                            locale = Browser.HttpServer.Session.ClientSessions[SessionManager.TemporaryToken.ID].Locale;
                        }

                        string translatedHtmlContent = Miscellaneous.Locale.TranslateHTML(content, locale);
                        if (translatedHtmlContent.StartsWith("<!DOCTYPE html"))
                        {
                            // html viewpage
                            string localizedHtmlContent = Miscellaneous.Locale.MergeLocaleCss(translatedHtmlContent, locale);
                            cachedViewData[cacheKey] = localizedHtmlContent;
                        }
                        else
                        {
                            // partial view
                            cachedViewData[cacheKey] = translatedHtmlContent;
                        }
                    }
                }
            }

            return cachedViewData[cacheKey];
        }

        /// <summary>
        /// True if a given view exists
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="viewName"></param>
        /// <returns></returns>
        public static bool ViewExists(Http.Server httpServer, string viewName)
        {
            if (internalViewNames.Any(internalViewName => internalViewName.Equals(viewName, StringComparison.OrdinalIgnoreCase))) return false;

            if (httpServer.Customization.HideConfig && viewName.Equals("configure", StringComparison.OrdinalIgnoreCase)) return false;
            if (httpServer.Customization.HideFiles && viewName.Equals("file", StringComparison.OrdinalIgnoreCase)) return false;
            if (httpServer.Customization.HideManiphest && viewName.Equals("maniphest", StringComparison.OrdinalIgnoreCase)) return false;
            if (httpServer.Customization.HideOfflineChanges && viewName.Equals("staging", StringComparison.OrdinalIgnoreCase)) return false;
            if (httpServer.Customization.HidePhriction && viewName.Equals("phriction", StringComparison.OrdinalIgnoreCase)) return false;
            if (httpServer.Customization.HideProjects && viewName.Equals("projects", StringComparison.OrdinalIgnoreCase)) return false;
            if (httpServer.Customization.HideUsers && viewName.Equals("user", StringComparison.OrdinalIgnoreCase)) return false;

            // in case url has parameters, skip parameters
            viewName = viewName.Split('?').FirstOrDefault();

            // check if view exists
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = string.Format("Phabrico.View.{0}.html", viewName);
            return assembly.GetManifestResourceNames().Any(name => name.Equals(resourceName, System.StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Sends filedata to the web browser
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="base64EIDOStream"></param>
        virtual public void Send(Browser browser, Base64EIDOStream base64EIDOStream)
        {
            ContentLength = base64EIDOStream.Length;

            if (ContentLength > 0)
            {
                if (CharSet != null)
                {
                    browser.Response.ContentType = ContentType + "; charset=" + CharSet;
                }
                else
                {
                    browser.Response.ContentType = ContentType;
                }
            
                if (IsAttachment && FileName != null)
                {
                    string rfc5987EncodedFileName = string.Join("", System.Text.Encoding.UTF8.GetBytes(FileName).Select(c => string.Format("%{0:X2}", c)));
                    browser.Response.Headers.Add("Content-Disposition", string.Format("attachment; filename*=UTF-8''{0}", rfc5987EncodedFileName));
                }

                if (EnableBrowserCache)
                {
                    browser.Response.Headers.Add("Cache-Control", "public, max-age=31536000, immutable");
                }
                else
                {
                    browser.Response.Headers["Cache-Control"] = "private, no-cache, must-revalidate";
                    browser.Response.Headers["Pragma"] = "no-cache";
                    browser.Response.Headers["Expires"] = "-1";
                }

                // set some HTTP header tag for security
                browser.Response.Headers.Add("Server", ""); 
                browser.Response.Headers.Add("X-Content-Type-Options", "nosniff");

                try
                {
                    // send data
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        byte[] decodedData = new byte[0x400000];

                        base64EIDOStream.Seek(0, SeekOrigin.Begin);

                        browser.SetContentLength(base64EIDOStream.Length);

                        while (true)
                        {
                            int nbrBytesRead = base64EIDOStream.Read(decodedData, 0, decodedData.Length);
                            int nbrBytesSent = browser.SendBlock(decodedData, nbrBytesRead);

                            if (nbrBytesSent != decodedData.Length) break;
                        }
                    }
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Sends a sequence of bytes to the web browser
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="data"></param>
        virtual public void Send(Browser browser, byte[] data = null)
        {
            if (data == null)
            {
                ContentLength = 0;
                data = new byte[0];
            }
            else
            {
                ContentLength = data.Length;
            }

            if (CharSet != null)
            {
                browser.Response.ContentType = ContentType + "; charset=" + CharSet;
            }
            else
            {
                browser.Response.ContentType = ContentType;
            }
            
            if (IsAttachment)
            {
                browser.Response.Headers.Add("Content-Disposition", string.Format("attachment; filename=\"{0}\"", FileName));
            }

            // remove Server header tag
            browser.Response.Headers.Add("Server", ""); 

            browser.Response.ContentType = ContentType;
            browser.Response.StatusCode = HttpStatusCode;
            browser.Response.StatusDescription = HttpStatusMessage;

            if (data != null && data.Length > 0)
            {
                try
                {
                    // send data
                    browser.Send(data);
                    TimestampSent = DateTime.UtcNow;
                }
                catch
                {
                }
            }
            else
            if (HttpStatusCode < 200 || HttpStatusCode >= 300)
            {
                // send data
                browser.Send(data);
            }
        }
    }
}
