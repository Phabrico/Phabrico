using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;

using Phabrico.Miscellaneous;
using Phabrico.Phabricator.API;

namespace Phabrico.Http
{
    /// <summary>
    /// Class which represents a single HTTP connection
    /// </summary>
    public class Browser : IDisposable
    {
        private SessionManager.Token _token = null;

        private Miscellaneous.HttpListenerContext httpListenerContext { get; set; }

        /// <summary>
        /// Represents the fingerprint of the browser
        /// </summary>
        public string Fingerprint
        {
            get;
            set;
        }

        /// <summary>
        /// Represents the Phabrico web server
        /// </summary>
        public Http.Server HttpServer
        {
            get;
            set;
        }

        /// <summary>
        /// Language of the web browser
        /// </summary>
        public string Language
        {
            get;
            set;
        }

        /// <summary>
        /// Represents the HTTP request communication channel from the web browser
        /// </summary>
        public Miscellaneous.HttpListenerRequest Request
        {
            get
            {
                return httpListenerContext.Request;
            }
        }

        /// <summary>
        /// Represents the HTTP response communication channel from the web browser
        /// </summary>
        public Miscellaneous.HttpListenerResponse Response
        {
            get
            {
                return httpListenerContext.Response;
            }
        }

        /// <summary>
        /// Represents the web browser's session
        /// </summary>
        public SessionManager.ClientSession Session
        {
            get
            {
                if (Token == null)
                {
                    _token = SessionManager.TemporaryToken;
                }

                if (HttpServer.Session.ClientSessions.ContainsKey(Token.ID) == false)
                {
                    HttpServer.Session.ClientSessions[Token.ID] = new SessionManager.ClientSession();
                }

                return HttpServer.Session.ClientSessions[Token.ID];
            }
        }

        /// <summary>
        /// Represents the web browser's authentication token
        /// </summary>
        public SessionManager.Token Token
        {
            get
            {
                if (_token == null)
                {
                    _token = SessionManager.GetToken(this);
                }

                return _token;
            }
        }

        /// <summary>
        /// Phabricator server connection settings, used for synchronizing
        /// </summary>
        public Conduit Conduit
        {
            get;
            set;
        }

        /// <summary>
        /// If logged on with a Windows account, this property will return the Windows identity
        /// </summary>
        public WindowsIdentity WindowsIdentity
        {
            get;
            internal set;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="httpListenerContext"></param>
        public Browser(Http.Server httpServer, Miscellaneous.HttpListenerContext httpListenerContext)
        {   
            this.HttpServer = httpServer;
            this.httpListenerContext = httpListenerContext;

            CopyCookiesFromRequestToResponse();

            // set fingerprint
            if (httpListenerContext.Request.IsLocal && httpServer.RemoteAccessEnabled == false)
            {
                Fingerprint = "IsLocal";
            }
            else
            {
                Fingerprint = httpListenerContext.Request.UserAgent
                            + string.Join("", httpListenerContext.Request.UserLanguages)
                            + httpListenerContext.Request.RemoteEndPoint.Address.ToString();
            }

            // set language based on language-cookie
            if (httpListenerContext.Request.Cookies["language"] != null)
            {
                Language = httpListenerContext.Request.Cookies["language"].Value;
            }
            else
            {
                // no cookie-found: first time or in incognito-mode -> set language based on server-session-variable
                using (Storage.Database database = new Storage.Database(null))
                {
                    Language = database.GetSessionVariable(this, "language");

                    if (string.IsNullOrEmpty(Language))
                    {
                        // no session-variable found: set default language to the language of the browser
                        Language = httpListenerContext.Request
                                                      .UserLanguages
                                                      .FirstOrDefault()
                                                      .Split('-')
                                                      .FirstOrDefault();

                        database.SetSessionVariable(this, "language", Language);
                    }
                }

                // set language cookie
                Session.Locale = Language;
                SetCookie("language", Language);
            }
        }

        /// <summary>
        /// Closes the TCP/IP connection
        /// </summary>
        public void Close()
        {
            bool allowBrowserCaching = httpListenerContext.Request.RawUrl.StartsWith("/favicon.ico", StringComparison.OrdinalIgnoreCase)
                                    || httpListenerContext.Request.RawUrl.StartsWith("/images/", StringComparison.OrdinalIgnoreCase)
                                    || httpListenerContext.Request.RawUrl.StartsWith("/fonts/", StringComparison.OrdinalIgnoreCase)
                                    || httpListenerContext.Request.RawUrl.StartsWith("/css/", StringComparison.OrdinalIgnoreCase)
                                    || httpListenerContext.Request.RawUrl.StartsWith("/scripts/", StringComparison.OrdinalIgnoreCase);

            if (allowBrowserCaching)
            {
                httpListenerContext.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
            }
            else
            {
                httpListenerContext.Response.Headers["Cache-Control"] = "private, no-cache, must-revalidate";
                httpListenerContext.Response.Headers["Pragma"] = "no-cache";
                httpListenerContext.Response.Headers["Expires"] = "-1";
            }

            httpListenerContext.Response.OutputStream.Flush();
            httpListenerContext.Response.OutputStream.Close();
        }

        /// <summary>
        /// Copies the cookies from the HTTP request to the HTTP response
        /// </summary>
        private void CopyCookiesFromRequestToResponse()
        {
            Dictionary<string, string> cookies = httpListenerContext.Request
                                                                   .Cookies
                                                                   .OfType<Cookie>()
                                                                   .ToDictionary(c => c.Name,
                                                                                 c => c.Value
                                                                                );

            if (cookies.Any())
            {
                string cookieDate = DateTime.UtcNow.AddDays(1).ToString("ddd, dd-MMM-yyyy H:mm:ss");
                string cookieSettings = ";SameSite=Strict;Path=/;Expires=" + cookieDate + " GMT";

                httpListenerContext.Response.AddHeader("Set-Cookie", cookies.Keys.FirstOrDefault() + "=" + cookies.Values.FirstOrDefault() + cookieSettings);
                foreach (string cookieName in cookies.Keys.Skip(1))
                {
                    httpListenerContext.Response.AppendHeader("Set-Cookie", cookieName + "=" + cookies[cookieName] + cookieSettings);
                }
            }
        }

        /// <summary>
        /// Finalizes an instance of the TcpConnection class.
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Returns a browser cookie based on the HTTP header 'Set-Cookie'
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public string GetCookie(string name)
        {
            string cookieHeader = httpListenerContext.Response.Headers["Set-Cookie"];
            if (cookieHeader != null)
            {
                Match cookieMatch = RegexSafe.Match(cookieHeader, "[,;]? *" + name + "=([^,;]*)", RegexOptions.None);
                if (cookieMatch == null)
                {
                    return null;
                }

                return cookieMatch.Groups[1].Value;
            }

            Cookie requestCookie = httpListenerContext.Request.Cookies[name];
            if (requestCookie != null)
            {
                return requestCookie.Value;
            }

            if (httpListenerContext.IsFake && name.Equals("token"))
            {
                return HttpServer.Session.ClientSessions.LastOrDefault().Key;
            }

            return null;
        }

        /// <summary>
        /// Reads a sequence of bytes from the web browser
        /// </summary>
        /// <param name="rcvBuffer"></param>
        /// <param name="contentLength"></param>
        /// <returns></returns>
        public int Receive(ref byte[] rcvBuffer, int contentLength)
        {
            using (var memstream = new MemoryStream())
            {
                int bytesRead;
                while ((bytesRead = httpListenerContext.Request.InputStream.Read(rcvBuffer, 0, contentLength)) > 0)
                {
                    memstream.Write(rcvBuffer, 0, bytesRead);
                }

                rcvBuffer = memstream.ToArray();
            }

            return rcvBuffer.Length;
        }

        /// <summary>
        /// Overwrites the current token
        /// </summary>
        /// <param name="newToken"></param>
        public void ResetToken(SessionManager.Token newToken)
        {
            _token = newToken;
        }

        /// <summary>
        /// Sends a sequence of bytes to the web browser
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="nbrBytesWrite"></param>
        /// <returns></returns>
        public int Send(byte[] bytes, int nbrBytesWrite)
        {
            try
            {
                Logging.WriteInfo(Token?.ID, "{0}: {1}", httpListenerContext.Request.RawUrl, httpListenerContext.Response.StatusCode.ToString());

                if (Response.ContentType == null || Response.ContentType.Equals("text/html") || Response.ContentType.Equals("text/css"))
                {
                    // update version in references
                    string content = UTF8Encoding.UTF8.GetString(bytes);
                    content = content.Replace("?version=@@PHABRICO-VERSION@@", "?version=" + VersionInfo.Version);
                    bytes = UTF8Encoding.UTF8.GetBytes(content);
                    nbrBytesWrite = bytes.Length;
                }

                httpListenerContext.Response.ContentLength64 = nbrBytesWrite;

                // check if data can be cached by browser
                bool allowBrowserCaching = httpListenerContext.Request.RawUrl.StartsWith("/favicon.ico", StringComparison.OrdinalIgnoreCase)
                                        || httpListenerContext.Request.RawUrl.StartsWith("/images/", StringComparison.OrdinalIgnoreCase)
                                        || httpListenerContext.Request.RawUrl.StartsWith("/fonts/", StringComparison.OrdinalIgnoreCase)
                                        || httpListenerContext.Request.RawUrl.StartsWith("/css/", StringComparison.OrdinalIgnoreCase)
                                        || httpListenerContext.Request.RawUrl.StartsWith("/scripts/", StringComparison.OrdinalIgnoreCase);

                if (allowBrowserCaching)
                {
                    httpListenerContext.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
                }
                else
                {
                    httpListenerContext.Response.Headers["Cache-Control"] = "private, no-cache, must-revalidate";
                    httpListenerContext.Response.Headers["Pragma"] = "no-cache";
                    httpListenerContext.Response.Headers["Expires"] = "-1";
                }

                // compress content with gzip
                byte[] compressedData;
                httpListenerContext.Response.Headers["Content-Encoding"] = "gzip";
                using (var compressedStream = new MemoryStream())
                {
                    using (var gzStream = new BufferedStream(new GZipStream(compressedStream, CompressionMode.Compress), 1000000))
                    {
                        gzStream.Write(bytes, 0, bytes.Length);
                    }
                    compressedData = compressedStream.ToArray();
                }

                // send data
                httpListenerContext.Response.ContentLength64 = compressedData.Length;
                httpListenerContext.Response.OutputStream.Write(compressedData, 0, compressedData.Length);
                httpListenerContext.Response.OutputStream.Flush();
                httpListenerContext.Response.OutputStream.Close();

                return nbrBytesWrite;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Sends a sequence of bytes to the web browser
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public int Send(byte[] bytes)
        {
            return Send(bytes, bytes.Length);
        }

        /// <summary>
        /// Sends a sequence of bytes to the web browser
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="nbrBytesWrite"></param>
        /// <returns></returns>
        public int SendBlock(byte[] bytes, int nbrBytesWrite)
        {
            try
            {
                httpListenerContext.Response.OutputStream.Write(bytes, 0, nbrBytesWrite);
                return nbrBytesWrite;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Sets the HTTP content-length
        /// </summary>
        /// <param name="nbrBytesWrite"></param>
        public void SetContentLength(long nbrBytesWrite)
        {
            httpListenerContext.Response.ContentLength64 = nbrBytesWrite;
        }

        /// <summary>
        /// Sets a browser cookie based on the HTTP header 'Set-Cookie'
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetCookie(string name, string value)
        {
            DictionarySafe<string,string> originalFormVariables = null;

            if (Token != null)
            {
                originalFormVariables = Session.FormVariables;
            }

            Dictionary<string,string> cookies = httpListenerContext.Request
                                                                   .Cookies
                                                                   .OfType<Cookie>()
                                                                   .ToDictionary(c => c.Name, 
                                                                                 c => c.Value
                                                                                );

            cookies[name] = value;
            
            string cookieDate = DateTime.UtcNow.AddDays(1).ToString("ddd, dd-MMM-yyyy H:mm:ss");
            string cookieSettings = ";SameSite=Strict;Path=/;Expires=" + cookieDate + " GMT";

            httpListenerContext.Response.AddHeader("Set-Cookie", cookies.Keys.FirstOrDefault() + "=" + cookies.Values.FirstOrDefault() + cookieSettings);
            foreach (string cookieName in cookies.Keys.Skip(1))
            {
                httpListenerContext.Response.AppendHeader("Set-Cookie", cookieName + "=" + cookies[cookieName] + cookieSettings);
            }

            Session.FormVariables = originalFormVariables;
        }
    }
}
