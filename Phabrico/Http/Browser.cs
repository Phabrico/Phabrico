using Phabrico.Miscellaneous;
using Phabrico.Phabricator.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Phabrico.Http
{
    /// <summary>
    /// Class which represents a single HTTP connection
    /// </summary>
    public class Browser : IDisposable
    {
        public class PublishedProperties
        {
            private Browser _owner;

            /// <summary>
            /// IP Address where the browser is running on
            /// </summary>
            public string IPAddress
            {
                get
                {
                    string ipv4Address = _owner.Request.IPv4Address;
                    if (string.IsNullOrEmpty(ipv4Address))
                    {
                        return _owner.Request.RemoteEndPoint.Address.MapToIPv4().ToString();
                    }
                    else
                    {
                        return ipv4Address;
                    }
                }
            }

            /// <summary>
            /// Language of the web browser
            /// </summary>
            public Language Language
            {
                get;
                set;
            }

            /// <summary>
            /// URL address
            /// </summary>
            public string URL
            {
                get
                {
                    return _owner.Request.RawUrl;
                }
            }

            /// <summary>
            /// Browser's useragent string
            /// </summary>
            public string UserAgent
            {
                get
                {
                    return _owner.Request.UserAgent;
                }
            }

            internal PublishedProperties(Browser browser)
            {
                _owner = browser;
            }
        }

        private SessionManager.Token _token = null;

        private Miscellaneous.HttpListenerContext httpListenerContext { get; set; }

        private static readonly List<string> preloaded = new List<string>();

        public static Browser Dummy { get; private set; } = null;

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
        /// Properties of the browser session which can be used in plugins
        /// </summary>
        public PublishedProperties Properties
        {
            get;
            internal set;
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

            if (Dummy == null)
            {
                Dummy = this;
            }

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
                            + httpListenerContext.Request.RemoteEndPoint.Address;
            }

            Properties = new PublishedProperties(this);

            if (httpServer.Customization.AvailableLanguages == null || httpServer.Customization.AvailableLanguages.Count() != 1)
            {
                // set language based on language-cookie
                if (httpListenerContext.Request.Cookies["language"] != null)
                {
                    Properties.Language = httpListenerContext.Request.Cookies["language"].Value;
                    Session.Locale = Properties.Language;
                }
                else
                {
                    // no cookie-found: first time or in incognito-mode -> set language based on server-session-variable
                    using (Storage.Database database = new Storage.Database(null))
                    {
                        Properties.Language = database.GetSessionVariable(this, "language");

                        if (string.IsNullOrWhiteSpace(Properties.Language))
                        {
                            // no session-variable found: set default language to the language of the browser
                            Properties.Language = httpListenerContext.Request
                                                                     .UserLanguages
                                                                     .FirstOrDefault()
                                                                     .Split('-')
                                                                     .FirstOrDefault();

                            database.SetSessionVariable(this, "language", Properties.Language);
                        }
                    }

                    // set language cookie
                    Session.Locale = Properties.Language;
                    SetCookie("language", Properties.Language, false);
                }
            }
            else
            {
                // set language cookie
                Properties.Language = httpServer.Customization.AvailableLanguages.FirstOrDefault();
                Session.Locale = Properties.Language;
                SetCookie("language", Properties.Language, false);
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
                                    || httpListenerContext.Request.RawUrl.StartsWith("/js/", StringComparison.OrdinalIgnoreCase);

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
            Dictionary<string, Cookie> cookies = httpListenerContext.Request
                                                                   .Cookies
                                                                   .OfType<Cookie>()
                                                                   .ToDictionary(c => c.Name,
                                                                                 c => c
                                                                                );

            if (cookies.Any())
            {
                string cookieDate = DateTime.UtcNow.AddDays(1).ToString("ddd, dd-MMM-yyyy H:mm:ss");
                string cookieSettings = ";SameSite=Strict;Path=" + Http.Server.RootPath +";Expires=" + cookieDate + " GMT";

                string cookieValue = cookies.Values.FirstOrDefault().Value + cookieSettings;
                if (cookies.FirstOrDefault().Value.HttpOnly) cookieValue += "; HttpOnly";

                httpListenerContext.Response.AddHeader("Set-Cookie", cookies.Keys.FirstOrDefault() + "=" + cookieValue);
                foreach (string cookieName in cookies.Keys.Skip(1))
                {
                    cookieValue = cookies[cookieName].Value + cookieSettings;
                    if (cookies[cookieName].HttpOnly) cookieValue += "; HttpOnly";
                    httpListenerContext.Response.AppendHeader("Set-Cookie", cookieName + "=" + cookieValue);
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
        /// Creates a new CSRF (= a random string of 64 characters) for a given session
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        private string GenerateCSRF(SessionManager.ClientSession session)
        {
            byte[] randomBytes = new byte[32];

            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(randomBytes);
            }

            string result = "";
            foreach (byte randomByte in randomBytes)
            {
                result += string.Format("{0:x2}", randomByte);
            }

            // store new CSRFs
            session.ActiveCSRF[result] = DateTime.UtcNow;

            return result;
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
        /// Returns true if a CSRF required action, does not contain a valid CSRF form field
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public bool InvalidCSRF(string url)
        {
            return Session.ActiveCSRF.ContainsKey( Session.FormVariables[url]["csrf_token"] ?? "" ) == false;
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

                    // if content contains CSRF fields -> replace them with generated CSRFs
                    if (content.Contains("@@CSRF@@"))
                    {
                        content = content.Replace("@@CSRF@@", GenerateCSRF(Session));
                    }

                    bytes = UTF8Encoding.UTF8.GetBytes(content);
                    nbrBytesWrite = bytes.Length;
                }

                httpListenerContext.Response.ContentLength64 = nbrBytesWrite;

                // check if data can be cached by browser
                bool allowBrowserCaching = httpListenerContext.Request.RawUrl.StartsWith("/favicon.ico", StringComparison.OrdinalIgnoreCase)
                                        || httpListenerContext.Request.RawUrl.StartsWith("/images/", StringComparison.OrdinalIgnoreCase)
                                        || httpListenerContext.Request.RawUrl.StartsWith("/fonts/", StringComparison.OrdinalIgnoreCase)
                                        || httpListenerContext.Request.RawUrl.StartsWith("/css/", StringComparison.OrdinalIgnoreCase)
                                        || httpListenerContext.Request.RawUrl.StartsWith("/js/", StringComparison.OrdinalIgnoreCase);

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

                httpListenerContext.Response.Headers["Server"] = "";
                httpListenerContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
                httpListenerContext.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
                httpListenerContext.Response.Headers["Content-Security-Policy"] = string.Join(";", (new string[] {
                    "default-src 'self' blob: https://api.github.com/repos/phabrico/phabrico/releases/latest https://github.com/jgraph/drawio https://github.com/1j01/jspaint",
                    "img-src 'self' blob: data:",
                    "style-src 'self' 'unsafe-inline'",
                    "script-src 'self' 'unsafe-inline'",
                    "frame-ancestors 'self'",
                    "form-action 'self';"
                }).Where(r => r != null)
                  .ToArray()
                );

                // send data
                httpListenerContext.Response.ContentLength64 = compressedData.Length;
                httpListenerContext.Response.OutputStream.Write(compressedData, 0, compressedData.Length);
                httpListenerContext.Response.OutputStream.Flush();
                httpListenerContext.Response.OutputStream.Close();

                if (Token?.EncryptionKey != null)
                {
                    Phabricator.Data.Account whoAmI = null;
                    bool systemDataIsPreloaded, userDataIsPreloaded = false;

                    lock (Server.synchronizationProcessHttpRequest)
                    {
                        systemDataIsPreloaded = preloaded.Any();
                    }

                    if (systemDataIsPreloaded == false)
                    {
                        Task.Delay(1000)
                            .ContinueWith((t) =>
                        {
                            // initialize some stuff after the first time we authenticate
                            lock (Server.synchronizationProcessHttpRequest)
                            {
                                using (Storage.Database database = new Storage.Database(Token?.EncryptionKey))
                                {
                                    Storage.Stage.DeleteUnreferencedFiles(database, this);   // clean up unreferenced staged files
                                }
                            }

                            Thread.Sleep(100);

                            lock (Server.synchronizationProcessHttpRequest)
                            {
                                HttpServer.PreloadFileMacros(Token.EncryptionKey);  // preload file macro's
                            }
                        });
                    }

                    using (Storage.Database database = new Storage.Database(Token?.EncryptionKey))
                    {
                        Storage.Account accountStorage = new Storage.Account();
                        whoAmI = accountStorage.WhoAmI(database, this);
                        if (whoAmI != null)
                        {
                            lock (Server.synchronizationProcessHttpRequest)
                            {
                                userDataIsPreloaded = preloaded.Contains(whoAmI.UserName);
                                if (userDataIsPreloaded == false)
                                {
                                    // initialize some user-specific stuff
                                    preloaded.Add(whoAmI.UserName);
                                }
                            }
                        }
                    }

                    if (whoAmI != null && userDataIsPreloaded == false)
                    {
                        Task.Delay(1250)
                            .ContinueWith((t) =>
                        {
                            try
                            {
                                HttpServer.PreloadContent(this, Token.EncryptionKey, whoAmI.UserName);  // preload content like favorites
                            }
                            catch (Phabrico.Exception.InvalidWhoAmIException)
                            {
                                lock (Server.synchronizationProcessHttpRequest)
                                {
                                    // run preload again after user has been logged on again
                                    preloaded.Clear();
                                }
                            }
                        });
                    }
                }

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
        public void SetCookie(string name, string value, bool httpOnly)
        {
            DictionarySafe<string,string> originalFormVariables = null;

            if (Token != null)
            {
                originalFormVariables = Session.FormVariables[Request.RawUrl];
            }

            Dictionary<string,Cookie> cookies = httpListenerContext.Request
                                                                   .Cookies
                                                                   .OfType<Cookie>()
                                                                   .Where(c => c.Name.Equals(name) == false)
                                                                   .ToDictionary(c => c.Name, 
                                                                                 c => c
                                                                                );
            
            string cookieValue;
            string cookieDate = DateTime.UtcNow.AddDays(1).ToString("ddd, dd-MMM-yyyy H:mm:ss");
            string cookieSettings = ";SameSite=Strict;Path=" + Http.Server.RootPath + ";Expires=" + cookieDate + " GMT";

            if (cookies.Any())
            {
                cookieValue = cookies.Values.FirstOrDefault().Value + cookieSettings;
                if (cookies.FirstOrDefault().Value.HttpOnly) cookieValue += "; HttpOnly";
                httpListenerContext.Response.AddHeader("Set-Cookie", cookies.Keys.FirstOrDefault() + "=" + cookieValue);

                foreach (string cookieName in cookies.Keys.Skip(1))
                {
                    cookieValue = cookies[cookieName].Value + cookieSettings;
                    if (cookies[cookieName].HttpOnly) cookieValue += "; HttpOnly";
                    httpListenerContext.Response.AppendHeader("Set-Cookie", cookieName + "=" + cookieValue);
                }
            }

            // add new cookie
            cookieValue = value + cookieSettings;
            if (httpOnly) cookieValue += "; HttpOnly";
            httpListenerContext.Response.AppendHeader("Set-Cookie", name + "=" + cookieValue);

            Session.FormVariables[Request.RawUrl] = originalFormVariables;
        }
    }
}
