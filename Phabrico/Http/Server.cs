using Newtonsoft.Json;
using Phabrico.Controllers;
using Phabrico.Exception;
using Phabrico.Miscellaneous;
using Phabrico.Phabricator.API;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Phabrico.Http
{
    /// <summary>
    /// Represents the Phabrico Web server
    /// </summary>
    public class Server
    {
        /// <summary>
        /// Represents the state of a cache
        /// </summary>
        private enum CacheState
        {
            /// <summary>
            /// The content of the cache is being renewed
            /// </summary>
            Busy,

            /// <summary>
            /// The content of the cache is too old and should be renewed
            /// </summary>
            Invalid,

            /// <summary>
            /// The content of the cache is OK
            /// </summary>
            Valid
        }

        /// <summary>
        /// The HTTP listener
        /// </summary>
        private HttpListener httpListener;

        /// <summary>
        /// Controllers cached per url
        /// </summary>
        private Dictionary<string, CachedControllerInfo> cachedControllerInfo = new Dictionary<string, CachedControllerInfo>();

        /// <summary>
        /// Dictionary containing static Http.Response.HttpMessage (e.g. css, javascript, fonts, ...).
        /// This is used for fast loading this static content instead of "slow" loading the embedded resourced
        /// </summary>
        private Dictionary<string, Http.Response.HttpMessage> cachedFixedHttpMessages = new Dictionary<string, Response.HttpMessage>();

        /// <summary>
        /// Dictionary containing the HTML of non-static Http.Response.HttpMessage (e.g. wiki, tasks).
        /// This dictionary is cleaned up periodically
        /// </summary>
        private static Dictionary<string, CachedHttpMessage> cachedHttpMessages = new Dictionary<string, CachedHttpMessage>();

        /// <summary>
        /// If set to Invalid, the cache will be filled up by the PreloadContent method after the next GET request.
        /// The PreloadContent method will set this variable to Busy during the preloading and to Valid when the preloading is finished
        /// </summary>
        private static CacheState cacheStatusHttpMessages = CacheState.Invalid;

        /// <summary>
        /// Timestamp when the next cache invalidation should happen
        /// (This will always be triggered by a HTTP GET call)
        /// </summary>
        private DateTime cacheNextInvalidationTimestamp = DateTime.UtcNow;

        /// <summary>
        /// Dictionary containing the latest notification messages sent to the browsers
        /// Key=WebSocket message identifier, Value=message content
        /// </summary>
        private static Dictionary<string, string> currentNotifications = new Dictionary<string, string>();

        /// <summary>
        /// Dictionary containing all file objects based on their macro names
        /// </summary>
        public static Dictionary<string, Phabricator.Data.File> FilesPerMacroName { get; } = new Dictionary<string, Phabricator.Data.File>() { { "", null } };
        private static object filesPerMacroNameLock = new object();

        /// <summary>
        /// True if Phabrico is executed as IIS Http module
        /// False is Phabrico is executed as a Windows service
        /// </summary>
        public bool IsHttpModule { get; private set; }

        /// <summary>
        /// Only 1 instance of the Http.Server can be created.
        /// Each time the constructor is executed, this variable is increased.
        /// Each time Stop() is executed, this variable is decreased.
        /// If set to 1 and the constructor is executed again, an exception will be thrown
        /// </summary>
        private static int numberOfInstancesCreated = 0;

        /// <summary>
        /// Contains all the url paths which point to an unsecured controller method
        /// </summary>
        private List<string> unsecuredUrlPaths = new List<string>();

        /// <summary>
        /// The root address to where the webserver is listening to.
        /// E.g. http://localhost:13467/
        /// </summary>
        public string Address { get; private set; }

        /// <summary>
        /// Configuration for customizing Phabrico
        /// </summary>
        public ApplicationCustomization Customization { get; private set; } = new ApplicationCustomization();

        /// <summary>
        /// List of available and loaded Phabrico plugins
        /// </summary>
        public static List<Plugin.PluginBase> Plugins { get; } = new List<Plugin.PluginBase>();

        /// <summary>
        /// If false, only connections from 127.x.x.x (localhost) will be allowed
        /// </summary>
        public bool RemoteAccessEnabled { get; private set; }

        /// <summary>
        /// URL of the Phabrico application
        /// Start with a '/' and ends with a '/'
        /// E.g. "/phabrico/"
        /// </summary>
        public static string RootPath { get; private set; }

        /// <summary>
        /// Session manager object which manages all the browser session tokens.
        /// Contains 
        /// </summary>
        public SessionManager Session { get; } = new SessionManager();

        /// <summary>
        /// TCP portnr for webserver listener
        /// </summary>
        public int TcpPortNr { get; private set; }

        /// <summary>
        /// If set to true, some extra unit testing code is executed.
        /// E.g. a dummy translator engine is used instead of an online translator engine
        /// </summary>
        public static bool UnitTesting = false;

        /// <summary>
        /// User roles per username
        /// </summary>
        public Dictionary<string, string[]> UserRoles { get; internal set; } = null;

        /// <summary>
        /// List of active WebSockets
        /// </summary>
        public static List<WebSocketContext> WebSockets = new List<WebSocketContext>();

        /// <summary>
        /// Constructor
        /// </summary>
        public Server(bool remoteAccessEnabled, int listenTcpPortNr, string rootPath, bool isHttpModule = false)
        {
            if (numberOfInstancesCreated > 0)
            {
                throw new System.Exception("Only one instance of Http.Server is allowed");
            }

            numberOfInstancesCreated++;

            this.TcpPortNr = listenTcpPortNr;
            this.RemoteAccessEnabled = remoteAccessEnabled;
            this.IsHttpModule = isHttpModule;

            RootPath = "/" + rootPath.Trim('/') + "/";
            RootPath = RegexSafe.Replace(RootPath, "//+", "/");

            cacheStatusHttpMessages = CacheState.Invalid;

            // search for unsecured controller methods in Phabrico
            unsecuredUrlPaths.AddRange(Assembly.GetExecutingAssembly()
                                                .GetExportedTypes()
                                                .Where(controllerClass => controllerClass.IsSubclassOf(typeof(Phabrico.Controllers.Controller)))
                                                .Select(controller => controller.GetMethods()
                                                                                .Select(method =>
                                                                                {
                                                                                    UrlControllerAttribute urlControllerAttribute = method.GetCustomAttribute<UrlControllerAttribute>();
                                                                                    return new
                                                                                    {
                                                                                        Method = method,
                                                                                        UrlControllerAttribute = urlControllerAttribute,
                                                                                        Unsecure = urlControllerAttribute != null && urlControllerAttribute.Unsecure
                                                                                    };
                                                                                })
                                                                                .Where(method => method.Unsecure)
                                                                                .Select(method => method.UrlControllerAttribute.URL)
                                                        )
                                                .SelectMany(url => url)
                                        );

            // load plugin DLLs
            string rootDirectory = System.IO.Path.GetDirectoryName(AppConfigLoader.ConfigFileName);
            foreach (string pluginFileName in System.IO.Directory.EnumerateFiles(rootDirectory, "Phabrico.Plugin.*.dll"))
            {
                try
                {
                    string absolutePathPluginDLL = Path.GetFullPath(pluginFileName);
                    var pluginDLL = Assembly.LoadFile(absolutePathPluginDLL);

                    foreach (Type pluginType in pluginDLL.GetExportedTypes().Where(t => t.BaseType == typeof(Plugin.PluginBase)))
                    {
                        Plugin.PluginBase plugin = pluginType.GetConstructor(Type.EmptyTypes).Invoke(null) as Plugin.PluginBase;
                        plugin.Assembly = pluginDLL;
                        plugin.Database = new Storage.Database(null);
                        Plugins.Add(plugin);

                        try
                        {
                            plugin.Load();
                            plugin.State = Plugin.PluginBase.PluginState.Loaded;

                            // search for unsecured controller methods
                            unsecuredUrlPaths.AddRange(pluginDLL.GetExportedTypes()
                                                                 .Where(controllerClass => controllerClass.IsSubclassOf(typeof(Phabrico.Controllers.Controller)))
                                                                 .Select(controller => controller.GetMethods()
                                                                                                 .Select(method =>
                                                                                                 {
                                                                                                     UrlControllerAttribute urlControllerAttribute = method.GetCustomAttribute<UrlControllerAttribute>();
                                                                                                     return new
                                                                                                     {
                                                                                                         Method = method,
                                                                                                         UrlControllerAttribute = urlControllerAttribute,
                                                                                                         Unsecure = urlControllerAttribute != null && urlControllerAttribute.Unsecure
                                                                                                     };
                                                                                                 })
                                                                                                 .Where(method => method.Unsecure)
                                                                                                 .Select(method => method.UrlControllerAttribute.URL)
                                                                         )
                                                                 .SelectMany(url => url)
                                                      );

                        }
                        catch (System.Exception pluginException)
                        {
                            Logging.WriteError(plugin.GetType().Name, "Error during loading: {0}", pluginException.Message);
                        }
                    }
                }
                catch (System.Exception pluginException)
                {
                    Logging.WriteException(pluginFileName.Trim('.', '/', '\\'), pluginException);
                }
            }

            Address = string.Format("http://{0}:{1}{2}", Environment.MachineName, listenTcpPortNr, RootPath);

            if (isHttpModule)
            {
                // event for stopping Phabrico when IIS is about to stop
                AppDomain.CurrentDomain.DomainUnload += new EventHandler(delegate (object sender, EventArgs args)
                {
                    Stop();
                });
            }
            else
            {
                // start listener
                httpListener = new HttpListener();
                httpListener.Prefixes.Add(string.Format("http://+:{0}{1}", listenTcpPortNr, RootPath));
                httpListener.AuthenticationSchemeSelectorDelegate = new AuthenticationSchemeSelector(AuthenticationSchemeForBrowser);
                httpListener.Start();

                httpListener.BeginGetContext(ProcessHttpRequest, httpListener);
            }
        }

        /// <summary>
        /// Determines what kind of authentication should be used for the given HTTP request.
        /// This can be Anonymous or IntegratedWindowsAuthentication
        /// </summary>
        /// <param name="httpRequest"></param>
        /// <returns></returns>
        private AuthenticationSchemes AuthenticationSchemeForBrowser(System.Net.HttpListenerRequest httpRequest)
        {
            string url = httpRequest.RawUrl;
            string controllerUrl;
            string controllerUrlAlias;
            Dictionary<MethodInfo, Http.Response.HtmlViewPage.ContentOptions> controllerMethods;
            string[] controllerParameters;
            if (IsStaticURL(httpRequest.Url.LocalPath) == false)
            {
                if (GetControllerInfo(null, ref url, out controllerUrl, out controllerUrlAlias, out controllerMethods, out controllerParameters) != null)
                {
                    MethodInfo controllerMethod = controllerMethods.FirstOrDefault().Key;
                    UrlControllerAttribute urlControllerAttribute = controllerMethod.GetCustomAttributes(typeof(UrlControllerAttribute)).FirstOrDefault() as UrlControllerAttribute;
                    if (urlControllerAttribute.IntegratedWindowsSecurity)
                    {
                        return AuthenticationSchemes.IntegratedWindowsAuthentication;
                    }
                }
            }

            return AuthenticationSchemes.Anonymous;
        }

        /// <summary>
        /// Cleans up old browser sessions
        /// </summary>
        public void CleanUpSessions()
        {
            foreach (SessionManager.Token activeToken in SessionManager.ActiveTokens
                                                                       .ToArray()
                                                                       .Where(token => token.Key != "temp"))
            {
                if (activeToken.Invalid)
                {
                    Session.CancelToken(activeToken.ID);
                }
            }
        }

        /// <summary>
        /// Validates if a given URL is actually a shortened URL.
        /// If so, the given URL will be replaced by the 'extended' URL
        /// </summary>
        /// <param name="cmdGetUrl">URL to be checked (and to be overwritten)</param>
        /// <returns>True if URL was a shortened URL</returns>
        private bool GetAlternativeRoute(ref string cmdGetUrl)
        {
            try
            {
                Match shortenedManiphestTaskUrl = RegexSafe.Match(cmdGetUrl, "^/T[0-9]+/?(\\?.*)?", RegexOptions.None);
                if (shortenedManiphestTaskUrl.Success)
                {
                    cmdGetUrl = "maniphest/" + shortenedManiphestTaskUrl.Value;
                    return true;
                }
            }
            finally
            {
                cmdGetUrl = cmdGetUrl.Replace("//", "/");
            }

            return false;
        }

        /// <summary>
        /// Parses the requested url and searches for the corresponding Controller class to process this url further
        /// </summary>
        /// <param name="browser">Link to the browser</param>
        /// <param name="url">The requested url (including parameters)</param>
        /// <param name="controllerUrl">The url itself (can be used to determine the corresponding View)</param>
        /// <param name="controllerUrlAlias">An alias/shortened url (can be used to determine the corresponding View)</param>
        /// <param name="controllerMethods">Corresponding controller methods (can contain GET and/or POST)</param>
        /// <param name="controllerParameters">Parameters to be processed by controller method; these are the sub-paths of the requested url</param>
        /// <returns>The controller class which represents the requested url</returns>
        private object GetControllerInfo(Browser browser, ref string url, out string controllerUrl, out string controllerUrlAlias, out Dictionary<MethodInfo, Http.Response.HtmlViewPage.ContentOptions> controllerMethods, out string[] controllerParameters)
        {
            object controller = null;
            Dictionary<MethodInfo, string> urlPerControllerMethod = new Dictionary<MethodInfo, string>();
            Dictionary<MethodInfo, string> aliasPerControllerMethod = new Dictionary<MethodInfo, string>();

            // take the alternative route for the current url, if it exists
            GetAlternativeRoute(ref url);

            // remove Phabrico version parameter from URL if existant
            int phabricoVersionPosition = url.IndexOf("?version=" + VersionInfo.Version);
            if (phabricoVersionPosition >= 0)
            {
                int suffixPosition = phabricoVersionPosition + ("?version=" + VersionInfo.Version).Length;
                if (url.Length > suffixPosition && url[suffixPosition] == '&')
                {
                    url = url.Substring(0, suffixPosition)
                        + "?"
                        + url.Substring(suffixPosition + 1);
                }

                url = url.Substring(0, phabricoVersionPosition)
                    + url.Substring(suffixPosition);
            }

            // in case url has parameters, skip parameters
            string originalUrl = url.TrimEnd('/');
            url = url.Split('?').FirstOrDefault();
            string arguments = string.Join("?", originalUrl.Split('?').Skip(1));

            string baseUrl = url.TrimEnd('/') + "/";

            CachedControllerInfo cachedController;
            if (cachedControllerInfo.TryGetValue(baseUrl, out cachedController))
            {
                controllerUrl = cachedController.ControllerUrl;
                controllerUrlAlias = cachedController.ControllerUrlAlias;
                controllerMethods = cachedController.ControllerMethods;

                if (cachedController.ControllerParameters == null)
                {
                    controllerParameters = null;
                }
                else
                {
                    List<string> parameters = new List<string>(cachedController.ControllerParameters);
                    parameters.RemoveAll(p => p.StartsWith("?"));
                    if (string.IsNullOrEmpty(arguments) == false)
                    {
                        parameters.AddRange(("?" + arguments).Split('&'));
                    }

                    controllerParameters = parameters.ToArray();
                }

                if (cachedController.ControllerConstructor == null)
                {
                    return null;
                }
                else
                {
                    Controller newController = cachedController.ControllerConstructor.Invoke(null) as Controller;
                    if (browser != null)
                    {
                        newController.GetType().GetProperty("browser").SetValue(newController, browser);

                        string token = browser.Request.Cookies["token"]?.Value;
                        if (token != null)
                        {
                            newController.GetType().GetProperty("TokenId").SetValue(newController, token);
                            newController.GetType().GetProperty("EncryptionKey").SetValue(newController, Session.GetToken(token)?.EncryptionKey);
                        }
                    }

                    return newController;
                }
            }

            controllerMethods = new Dictionary<MethodInfo, Http.Response.HtmlViewPage.ContentOptions>();
            controllerParameters = null;

            // collect all assemblies (Phabrico + plugin dll's)
            List<Assembly> assemblies = new List<Assembly>();
            assemblies.Add(Assembly.GetCallingAssembly());  // phabrico
            assemblies.AddRange(Plugins.Select(plugin => plugin.Assembly));  // plugins

            // loop through all assemblies
            foreach (Assembly assembly in assemblies)
            {
                bool controllerFound = false;

                foreach (Type controllerType in assembly.GetExportedTypes().Where(controllerClass => controllerClass.IsSubclassOf(typeof(Phabrico.Controllers.Controller))))
                {
                    foreach (var someControllerMethodData in controllerType.GetMethods()
                                                                           .Where(method => method.CustomAttributes
                                                                                                  .Any(attr => attr.AttributeType == typeof(UrlControllerAttribute))
                                                                                 )
                                                                           .Select(method => new
                                                                           {
                                                                               Method = method,
                                                                               URLLength = method.CustomAttributes
                                                                                                 .FirstOrDefault(attr => attr.AttributeType == typeof(UrlControllerAttribute))
                                                                                                 .NamedArguments
                                                                                                 .FirstOrDefault(arg => arg.MemberName.Equals("URL"))
                                                                                                 .TypedValue
                                                                                                 .Value
                                                                                                 .ToString()
                                                                                                 .Length
                                                                           })
                                                                           .OrderByDescending(method => method.URLLength))
                    {
                        var urlControllerAttributeType = someControllerMethodData.Method
                                                                                 .CustomAttributes
                                                                                 .FirstOrDefault(attr => attr.AttributeType == typeof(UrlControllerAttribute));
                        if (urlControllerAttributeType != null)
                        {
                            string currentControllerUrl = (string)urlControllerAttributeType.NamedArguments.FirstOrDefault(arg => arg.MemberName.Equals("URL")).TypedValue.Value;
                            string currentControllerUrlAlias = (string)urlControllerAttributeType.NamedArguments.FirstOrDefault(arg => arg.MemberName.Equals("Alias")).TypedValue.Value;

                            if (urlPerControllerMethod.Values.Any(processedControllerUrl => processedControllerUrl.StartsWith(currentControllerUrl, StringComparison.OrdinalIgnoreCase)
                                                                                         && processedControllerUrl.TrimEnd('/').Length > currentControllerUrl.TrimEnd('/').Length
                                                                 ))
                            {
                                continue;
                            }

                            if (urlPerControllerMethod.Values.Any(processedControllerUrlAlias => currentControllerUrlAlias != null
                                                                                              && processedControllerUrlAlias.StartsWith(currentControllerUrlAlias, StringComparison.OrdinalIgnoreCase)
                                                                                              && processedControllerUrlAlias.TrimEnd('/').Length > currentControllerUrlAlias.TrimEnd('/').Length
                                                                ))
                            {
                                continue;
                            }

                            Http.Response.HtmlViewPage.ContentOptions? controllerOptions = (Http.Response.HtmlViewPage.ContentOptions?)(int?)urlControllerAttributeType.NamedArguments.FirstOrDefault(arg => arg.MemberName.Equals("HtmlViewPageOptions")).TypedValue.Value;
                            if (controllerOptions.HasValue == false)
                            {
                                controllerOptions = Http.Response.HtmlViewPage.ContentOptions.Default;
                            }

                            if (currentControllerUrl != null && baseUrl.StartsWith(currentControllerUrl.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))
                            {
                                if (originalUrl.Equals(currentControllerUrl, StringComparison.OrdinalIgnoreCase))
                                {
                                    controllerParameters = new string[0];
                                }
                                else
                                {
                                    string nextPath = originalUrl.Substring(currentControllerUrl.TrimEnd('/').Length);
                                    if (nextPath.StartsWith("/"))
                                    {
                                        controllerParameters = nextPath.Substring(1)
                                                                       .Split('?')
                                                                       .First()
                                                                       .Split('/');
                                    }
                                    else
                                    {
                                        controllerParameters = new string[0];
                                    }
                                }
                            }
                            else
                            if (currentControllerUrlAlias != null && baseUrl.StartsWith(currentControllerUrlAlias.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))
                            {
                                if (originalUrl.Equals(currentControllerUrlAlias, StringComparison.OrdinalIgnoreCase))
                                {
                                    controllerParameters = new string[0];
                                }
                                else
                                {
                                    string nextPath = originalUrl.Substring(currentControllerUrlAlias.TrimEnd('/').Length);
                                    if (nextPath.StartsWith("/"))
                                    {
                                        controllerParameters = nextPath.Substring(1)
                                                                       .Split('?')
                                                                       .First()
                                                                       .Split('/');
                                    }
                                    else
                                    {
                                        controllerParameters = new string[0];
                                    }
                                }
                            }
                            else
                            {
                                continue;
                            }

                            // if first controller parameter is empty, remove it
                            if (controllerParameters.Any() && string.IsNullOrEmpty(controllerParameters[0]))
                            {
                                controllerParameters = controllerParameters.Skip(1).ToArray();
                            }

                            // remember url and alias
                            urlPerControllerMethod[someControllerMethodData.Method] = currentControllerUrl;
                            aliasPerControllerMethod[someControllerMethodData.Method] = currentControllerUrlAlias;

                            controller = controllerType.GetConstructor(Type.EmptyTypes).Invoke(null);
                            controllerMethods[someControllerMethodData.Method] = controllerOptions.Value;

                            if (browser != null)
                            {
                                controllerType.GetProperty("browser").SetValue(controller, browser);

                                string token = browser.Request.Cookies["token"]?.Value;
                                if (token != null)
                                {
                                    controllerType.GetProperty("TokenId").SetValue(controller, token);
                                    controllerType.GetProperty("EncryptionKey").SetValue(controller, Session.GetToken(token)?.EncryptionKey);
                                }
                            }
                        }
                    }

                    if (controller != null)
                    {
                        controllerFound = true;
                        break;
                    }
                }

                if (controllerFound)
                {
                    break;
                }
            }

            // in case a controller method is found assigned to the given url, remove all controller methods from resultset which are assigned to a partially url (+parameter)
            if (urlPerControllerMethod.ContainsValue(url))
            {
                string urlControllerMethod = url;
                foreach (MethodInfo method in urlPerControllerMethod.Where(kvp => kvp.Value.TrimEnd('/').Equals(urlControllerMethod.TrimEnd('/')) == false).Select(kvp => kvp.Key).ToList())
                {
                    controllerMethods.Remove(method);
                    urlPerControllerMethod.Remove(method);
                    aliasPerControllerMethod.Remove(method);
                }
            }

            if (urlPerControllerMethod.Count > 1)
            {
                string longestUrl = urlPerControllerMethod.Values.OrderByDescending(controllerMethodUrl => controllerMethodUrl.Length).FirstOrDefault().TrimEnd('/');
                foreach (var kvp in urlPerControllerMethod.ToList())
                {
                    if (kvp.Value.TrimEnd('/').Equals(longestUrl) == false)
                    {
                        controllerMethods.Remove(kvp.Key);
                        urlPerControllerMethod.Remove(kvp.Key);
                        aliasPerControllerMethod.Remove(kvp.Key);
                    }
                }
            }

            controllerUrl = urlPerControllerMethod.Values.FirstOrDefault();
            controllerUrlAlias = aliasPerControllerMethod.Values.FirstOrDefault();

            if (controller != null && string.IsNullOrEmpty(arguments) == false)
            {
                // make sure we have our parameters again in our url
                url = url + "?" + arguments;
            }

            // cache result
            cachedController = new CachedControllerInfo();
            cachedController.ControllerUrl = controllerUrl;
            cachedController.ControllerUrlAlias = controllerUrlAlias;
            cachedController.ControllerMethods = controllerMethods;
            cachedController.ControllerParameters = controllerParameters;
            cachedController.ControllerConstructor = controller?.GetType()?.GetConstructor(Type.EmptyTypes);
            cachedControllerInfo[baseUrl] = cachedController;

            return controller;
        }

        /// <summary>
        /// Returns the timestamp when the latest synchronization between Phabrico and Phabricator was executed
        /// </summary>
        /// <param name="token"></param>
        /// <param name="locale"></param>
        /// <returns></returns>
        public string GetLatestSynchronizationTime(SessionManager.Token token, Language locale)
        {
            string encryptionKey = token?.EncryptionKey;
            if (string.IsNullOrEmpty(encryptionKey) == false)
            {
                using (Storage.Database database = new Storage.Database(encryptionKey))
                {
                    Storage.Account accountStorage = new Storage.Account();

                    Phabricator.Data.Account accountData = accountStorage.Get(database, Language.NotApplicable).FirstOrDefault();
                    if (accountData != null)
                    {
                        if (accountData.Parameters.LastSynchronizationTimestamp == DateTimeOffset.MinValue)
                        {
                            return Locale.TranslateText("Synchronization.Status.NotYetSynchronized", locale);
                        }
                        else
                        {
                            return Controller.FormatDateTimeOffset(accountData.Parameters.LastSynchronizationTimestamp, locale);
                        }
                    }
                }
            }

            return "Unknown";
        }

        /// <summary>
        /// Processes a Phabrico request from IIS
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IISApplication_AuthenticateRequest(object sender, EventArgs e)
        {
            HttpApplication application = (HttpApplication)sender;
            HttpContext context = application.Context;

            if (context.Request.RawUrl.StartsWith(RootPath, StringComparison.OrdinalIgnoreCase) || context.Request.RawUrl.Equals(RootPath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            {
                Phabrico.Miscellaneous.HttpListenerContext httpListenerContext = context;
                ProcessHttpRequest(httpListenerContext);
                application.CompleteRequest();
            }
        }

        /// <summary>
        /// Prepares the request from a IIS module
        /// </summary>
        /// <param name="application"></param>
        public void Init(HttpApplication application)
        {
            application.AuthenticateRequest += IISApplication_AuthenticateRequest;
        }

        /// <summary>
        /// Remove all cached data which is older than a given timestamp
        /// This method is executed after some GET HTTP calls
        /// </summary>
        /// <param name="database"></param>
        public static void InvalidateNonStaticCache(Database database, DateTime since)
        {
            lock (cachedHttpMessages)
            {
                if (since == DateTime.MaxValue)
                {
                    // recalculate median and maximum file sizes in homepage
                    Storage.File.RecalculateMedianAndMaximumFileSizesInDatabase(database);

                    // clear cached messages
                    cachedHttpMessages.Clear();

                    cacheStatusHttpMessages = CacheState.Invalid;

                    return;
                }

                // delete old cached messages
                int cacheSize = cachedHttpMessages.Sum(cachedHttpMessage => cachedHttpMessage.Value.Size);
                foreach (var cachedHttpMessage in cachedHttpMessages.ToArray())
                {
                    if (cachedHttpMessage.Value.Timestamp < since)
                    {
                        cachedHttpMessages.Remove(cachedHttpMessage.Key);
                    }
                }

                cacheStatusHttpMessages = CacheState.Invalid;
            }
        }

        /// <summary>
        /// Removes all cached data which contains a given url
        /// This method is executed when some data is saved to the database
        /// </summary>
        /// <param name="url"></param>
        public void InvalidateNonStaticCache(string url)
        {
            lock (cachedHttpMessages)
            {
                // delete old cached messages
                foreach (var cachedHttpMessage in cachedHttpMessages.ToArray())
                {
                    if (cachedHttpMessage.Key.Contains(url))
                    {
                        cachedHttpMessages.Remove(cachedHttpMessage.Key);
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if a given URL is a static URL and should not be processed by a controller
        /// </summary>
        /// <param name="url">URL to be validated</param>
        /// <returns>True if URL is a static URL</returns>
        private bool IsStaticURL(string url)
        {
            return url.StartsWith("/css/", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("/favicon.ico", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("/fonts/", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("/js/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Loads some data from the database and stores it in the non-static cache
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="encryptionKey"></param>
        /// <param name="username"></param>
        public void PreloadContent(Browser browser, string encryptionKey, string username)
        {
            string token = browser.GetCookie("token");
            Miscellaneous.HttpListenerContext fakeHttpListenerContext = new Miscellaneous.HttpListenerContext(browser.Request);
            Browser clonedBrowser = new Browser(this, fakeHttpListenerContext);
            Phabricator.Data.Account whoAmI;

            cacheStatusHttpMessages = CacheState.Busy;

            using (Storage.Database database = new Database(encryptionKey))
            {
                Storage.Account accountStorage = new Storage.Account();
                whoAmI = accountStorage.WhoAmI(database, browser);
                if (whoAmI == null)
                {
                    // this can happen during unit tests which are running too fast -> this is not critical, so skip it
                    return;
                }

                SessionManager.Token sessionToken = Session.CreateToken(whoAmI.Token, browser);
                clonedBrowser.SetCookie("token", sessionToken.ID, true);

                whoAmI.PublicXorCipher = accountStorage.GetPublicXorCipher(database, sessionToken);

                // reinitialize session variables
                sessionToken.EncryptionKey = Encryption.XorString(encryptionKey, whoAmI.PublicXorCipher);
                sessionToken.AuthenticationFactor = AuthenticationFactor.Public;
                Session.ClientSessions[sessionToken.ID] = new SessionManager.ClientSession();

                // set language
                clonedBrowser.Session.Locale = browser.Session.Locale;
            }

            Task.Delay(500)
                .ContinueWith((task) =>
            {
                Logging.WriteInfo("(internal)", "PreloadContent");

                using (Storage.Database database = new Database(encryptionKey))
                {
                    string htmlContent;
                    string theme = database.ApplicationTheme;
                    Response.HtmlViewPage.ContentOptions htmlViewPageOptions;

                    // preload favorite phriction documents
                    Storage.Phriction phrictionStorage = new Storage.Phriction();
                    List<Phabricator.Data.Phriction> favoritePhrictionDocuments = phrictionStorage.GetFavorites(database, browser, username).ToList();
                    favoritePhrictionDocuments.Add(phrictionStorage.Get(database, "/", browser.Session.Locale));
                    htmlViewPageOptions = Response.HtmlViewPage.ContentOptions.HideGlobalTreeView;
                    Controllers.Phriction phrictionController = new Controllers.Phriction();
                    phrictionController.browser = clonedBrowser;
                    foreach (Phabricator.Data.Phriction phrictionDocument in favoritePhrictionDocuments.Where(document => document != null))
                    {
                        lock (cachedHttpMessages)
                        {
                            string cacheKey = token + theme + clonedBrowser.Language + "/w/" + phrictionDocument.Path;
                            if (cachedHttpMessages.ContainsKey(cacheKey)) continue;

                            string[] parameters = phrictionDocument.Path.Trim('/').Split('/');
                            Response.HtmlViewPage viewPage = new Response.HtmlViewPage(this, clonedBrowser, true, "Phriction", parameters);

                            phrictionController.EncryptionKey = encryptionKey;

                            phrictionController.HttpGetLoadParameters(this, clonedBrowser, ref viewPage, parameters, "");

                            viewPage.Theme = theme;
                            htmlContent = viewPage.GetFullContent(clonedBrowser, htmlViewPageOptions);

                            cachedHttpMessages[cacheKey] = new CachedHttpMessage(encryptionKey, UTF8Encoding.UTF8.GetBytes(htmlContent), "text/html");
                        }

                        Thread.Sleep(100);
                    }

                    // preload assigned maniphest tasks
                    Storage.Maniphest maniphestStorage = new Storage.Maniphest();
                    Storage.Stage stageStorage = new Storage.Stage();
                    Phabricator.Data.Maniphest[] stagedManiphestTasks = stageStorage.Get<Phabricator.Data.Maniphest>(database, browser.Session.Locale).ToArray();
                    foreach (Phabricator.Data.Maniphest stagedManiphestTask in stagedManiphestTasks)
                    {
                        maniphestStorage.LoadStagedTransactionsIntoManiphestTask(database, stagedManiphestTask, browser.Session.Locale);
                    }

                    Phabricator.Data.Maniphest[] assignedManiphestTasks = maniphestStorage.Get(database, browser.Session.Locale)
                                                                                          .Where(maniphestTask => stagedManiphestTasks.All(stagedTask => stagedTask.Token.Equals(maniphestTask.Token) == false)
                                                                                                               && maniphestTask.Owner.Equals(whoAmI.Parameters.UserToken)
                                                                                                               && maniphestTask.IsOpen.Equals("true")
                                                                                                )
                                                                                          .ToArray();
                    htmlViewPageOptions = Response.HtmlViewPage.ContentOptions.HideGlobalTreeView;
                    Controllers.Maniphest maniphestController = new Controllers.Maniphest();
                    maniphestController.browser = clonedBrowser;
                    foreach (Phabricator.Data.Maniphest assignedManiphestTask in assignedManiphestTasks)
                    {
                        lock (cachedHttpMessages)
                        {
                            string cacheKey = token + theme + clonedBrowser.Language + "/maniphest/T" + assignedManiphestTask.ID + "/";
                            if (cachedHttpMessages.ContainsKey(cacheKey)) continue;

                            string[] parameters = new string[] { "T" + assignedManiphestTask.ID.ToString() };
                            Response.HtmlViewPage viewPage = new Response.HtmlViewPage(this, clonedBrowser, true, "ManiphestTask", parameters);

                            maniphestController.EncryptionKey = encryptionKey;

                            maniphestController.HttpGetLoadParameters(this, clonedBrowser, ref viewPage, parameters, "");

                            viewPage.Theme = theme;
                            htmlContent = viewPage.GetFullContent(clonedBrowser, htmlViewPageOptions);

                            cachedHttpMessages[cacheKey] = new CachedHttpMessage(encryptionKey, UTF8Encoding.UTF8.GetBytes(htmlContent), "text/html");
                        }

                        Thread.Sleep(100);
                    }
                }

                // cache was filled up again
                cacheStatusHttpMessages = CacheState.Valid;
            });
        }

        /// <summary>
        /// Loads all file objects which have a macro-name into the static FilesPerMacroName dictionary.
        /// This FilesPerMacroName dictionary is used by the remarkup parser for decoding macro-names
        /// </summary>
        /// <param name="encryptionKey"></param>
        public void PreloadFileMacros(string encryptionKey)
        {
            lock (filesPerMacroNameLock)
            {
                if (Http.Server.FilesPerMacroName.ContainsKey(""))
                {
                    try
                    {
                        Dictionary<string, Phabricator.Data.File> filesPerMacroName = new Dictionary<string, Phabricator.Data.File>();

                        using (Storage.Database database = new Database(encryptionKey))
                        {
                            Storage.File fileStorage = new Storage.File();
                            foreach (Phabricator.Data.File macroFile in fileStorage.GetMacroFiles(database, Language.NotApplicable)
                                                                                   .Where(record => string.IsNullOrWhiteSpace(record.MacroName) == false)
                                    )
                            {
                                filesPerMacroName[macroFile.MacroName] = macroFile;
                            }
                        }

                        // copy local filesPerMacroName to static FilesPerMacroName
                        FilesPerMacroName.Clear();
                        foreach (string macroName in filesPerMacroName.Keys)
                        {
                            FilesPerMacroName[macroName] = filesPerMacroName[macroName];
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
        }

        /// <summary>
        /// This method is executed when an AccessDeniedException is thrown.
        /// This will trigger an Access Denied error screen
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="accessDeniedException"></param>
        private void ProcessAccessDeniedException(Browser browser, AccessDeniedException accessDeniedException)
        {
            using (Storage.Database database = new Storage.Database(null))
            {
                Response.HtmlViewPage htmlViewPage = new Response.HtmlViewPage(browser);
                htmlViewPage.SetContent(browser, htmlViewPage.GetViewData("AccessDenied"));
                htmlViewPage.SetText("INVALID-LOCAL-URL", accessDeniedException.URL);
                htmlViewPage.SetText("PHABRICATOR-URL", accessDeniedException.URL);
                htmlViewPage.SetText("THEME", database.ApplicationTheme);
                htmlViewPage.SetText("THEME-STYLE", "", Response.HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                htmlViewPage.SetText("LOCALE", browser.Session.Locale, Response.HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                htmlViewPage.SetText("COMMENT", Locale.TranslateText(accessDeniedException.Comment, browser.Session.Locale));
                htmlViewPage.SetText("PHABRICO-ROOTPATH", Http.Server.RootPath, Response.HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                htmlViewPage.Merge();
                htmlViewPage.HttpStatusCode = 403;
                htmlViewPage.HttpStatusMessage = "Forbidden";
                htmlViewPage.Send(browser);
            }
        }

        /// <summary>
        /// This method is executed when an AuthorizationException is thrown.
        /// This exception is usually thrown when the user's token has been invalidated (e.g. after a timeout)
        /// and a new HTTP request is executed.
        /// This will trigger a redirect to the authentication dialog
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="authorizationException"></param>
        private void ProcessAuthorizationException(Browser browser, AuthorizationException authorizationException)
        {
            Logging.WriteError(browser.Token?.ID, "AuthorizationException thrown:\r\n{0}", authorizationException.StackTrace);

            if (browser.Token != null && browser.Token.AuthenticationFactor != AuthenticationFactor.Ownership)
            {
                // current session token was invalidated somehow -> redirect to the homepage
                string token = browser.Request.Cookies["token"]?.Value;
                if (token != null)
                {
                    Session.CancelToken(token);
                }
            }

            Http.Response.HttpRedirect httpResponse = null;
            httpResponse = new Http.Response.HttpRedirect(this, browser, RootPath + "?ReturnURL=" + browser.Request.RawUrl.Split('?')[0]);
            httpResponse.Send(browser);
        }

        /// <summary>
        /// This method will process incoming HTTP GET requests from the web browser
        /// </summary>
        /// <param name="browser"></param>
        private void ProcessHttpGetRequest(Browser browser)
        {
            SessionManager.Token token = null;
            string encryptionKey = null;
            bool needAuthorization = false;

            // decode requested url from browser
            string cmdGetUrl = browser.Request.RawUrl;
            Logging.WriteInfo(browser?.Token?.ID, "GET {0}", cmdGetUrl);

            // in case the url is a global alias url, convert it to an internal url
            cmdGetUrl = RouteManager.GetInternalURL(cmdGetUrl);

            string tokenId = browser.Request.Cookies["token"]?.Value;
            if (tokenId == null && IsStaticURL(cmdGetUrl) == false)
            {
                // no token found -> redirect to homepage
                needAuthorization = true;
            }
            else
            {
                token = Session.GetToken(tokenId);
                encryptionKey = token?.EncryptionKey;
            }

            using (Storage.Database database = new Storage.Database(encryptionKey))
            {
                Storage.Account accountStorage = new Storage.Account();

                database.PrivateEncryptionKey = token?.PrivateEncryptionKey;

                try
                {
                    if (unsecuredUrlPaths.All(path => cmdGetUrl.StartsWith(path) == false) &&
                        (needAuthorization || cmdGetUrl.Split('?')[0].Equals("/") || cmdGetUrl.Split('?')[0].Equals(""))
                       )
                    {
                        if (UserAgentIsSupported(browser.Request.UserAgent) == false)
                        {
                            Http.Response.HtmlViewPage browserNotSupportedPage = new Http.Response.HtmlViewPage(this, browser, true, "BrowserNotSupported", null);
                            browserNotSupportedPage.SetText("LOCALE", browser.Session.Locale, Http.Response.HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                            browserNotSupportedPage.SetText("PHABRICO-ROOTPATH", Http.Server.RootPath, Http.Response.HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                            browserNotSupportedPage.Merge();
                            browserNotSupportedPage.Send(browser);
                            return;
                        }
                        else
                        {
                            ProcessHttpGetInitialPageRequest(browser, database, token, encryptionKey, cmdGetUrl);
                            return;
                        }
                    }
                    else
                    {
                        Http.Response.HttpMessage httpResponse = null;

                        if (httpResponse == null)
                        {
                            // check if url is correctly formatted (URL should end with a /)
                            if (cmdGetUrl.Split('?').FirstOrDefault().EndsWith("/") == false)
                            {
                                string[] incorrectUrlParts = cmdGetUrl.Split('?');
                                string cmdGetUrlParameters = string.Join("?", incorrectUrlParts.Skip(1));

                                cmdGetUrl = incorrectUrlParts[0] + "/";
                                if (string.IsNullOrEmpty(cmdGetUrlParameters) == false)
                                {
                                    cmdGetUrl += "?" + cmdGetUrlParameters;
                                }
                            }

                            // check if we have a cached static url
                            if (cachedFixedHttpMessages.ContainsKey(cmdGetUrl))
                            {
                                httpResponse = cachedFixedHttpMessages[cmdGetUrl];
                                httpResponse.Send(browser);
                                return;
                            }

                            lock (cachedHttpMessages)
                            {
                                // check if we have a cached non-static url
                                string theme = database.ApplicationTheme;
                                if (cachedHttpMessages.ContainsKey(browser.Token?.ID + theme + browser.Language + cmdGetUrl))
                                {
                                    CachedHttpMessage cachedHttpMessage = cachedHttpMessages[browser.Token?.ID + theme + browser.Language + cmdGetUrl];
                                    cachedHttpMessage.Timestamp = DateTime.UtcNow;
                                    browser.Response.ContentType = cachedHttpMessage.ContentType;
                                    byte[] decryptedCachedData = UTF8Encoding.UTF8.GetBytes(Encryption.Decrypt(encryptionKey, cachedHttpMessage.EncryptedData));
                                    browser.Send(decryptedCachedData, cachedHttpMessage.EncryptedData.Length);
                                    return;
                                }

                                // check if we have a non-cached static url
                                switch (cmdGetUrl.TrimEnd('/'))
                                {
                                    case string css when css.StartsWith("/css/", StringComparison.OrdinalIgnoreCase):
                                        httpResponse = new Http.Response.StyleSheet(this, browser, cmdGetUrl.Substring("/css/".Length));
                                        if (httpResponse.Content != null)
                                        {
                                            cachedFixedHttpMessages[cmdGetUrl] = httpResponse;
                                            httpResponse.Send(browser);
                                            return;
                                        }
                                        break;

                                    case string favicon when favicon.Split('?')
                                                                    .FirstOrDefault()
                                                                    .TrimEnd('/')
                                                                    .Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase):
                                        httpResponse = new Http.Response.FavIcon(this, browser, cmdGetUrl);
                                        cachedFixedHttpMessages[cmdGetUrl] = httpResponse;
                                        httpResponse.Send(browser);
                                        return;

                                    case string fonts when fonts.StartsWith("/fonts/", StringComparison.OrdinalIgnoreCase):
                                        httpResponse = new Http.Response.Font(this, browser, cmdGetUrl.Substring("/fonts/".Length));
                                        cachedFixedHttpMessages[cmdGetUrl] = httpResponse;
                                        httpResponse.Send(browser);
                                        return;

                                    case string images when images.StartsWith("/images/", StringComparison.OrdinalIgnoreCase):
                                        httpResponse = new Http.Response.File(this, browser, cmdGetUrl);
                                        cachedFixedHttpMessages[cmdGetUrl] = httpResponse;
                                        httpResponse.Send(browser);
                                        return;

                                    case string scripts when scripts.StartsWith("/js/", StringComparison.OrdinalIgnoreCase):
                                        httpResponse = new Http.Response.Script(this, browser, cmdGetUrl.Substring("/js/".Length));
                                        if (httpResponse.Content != null)
                                        {
                                            cachedFixedHttpMessages[cmdGetUrl] = httpResponse;
                                            httpResponse.Send(browser);
                                            return;
                                        }
                                        break;

                                    case "/logout":
                                        if (tokenId != null)
                                        {
                                            // set default language to current user's language (so the Login-dialog will appear in the last user's language)
                                            Session.ClientSessions[SessionManager.TemporaryToken.ID].Locale = browser.Session.Locale;

                                            // cancel session
                                            Session.CancelToken(tokenId);

                                            // restore language
                                            browser.Session.Locale = Session.ClientSessions[SessionManager.TemporaryToken.ID].Locale;
                                        }

                                        Http.Response.HomePage homepageResponse = new Http.Response.HomePage(this, browser, "/");
                                        homepageResponse.Theme = database.ApplicationTheme;
                                        homepageResponse.Send(browser, "");
                                        return;

                                    case "/poke":
                                        bool tokenIsValid = false;
                                        if (tokenId != null)
                                        {
                                            tokenIsValid = Session.TokenValid(tokenId);
                                            if (tokenIsValid)
                                            {
                                                SessionManager.Token sessionToken = SessionManager.GetToken(browser);
                                                sessionToken.ServerValidationCheckEnabled = true;
                                            }
                                        }

                                        // get AutoLogOutAfterMinutesOfInactivity parameter
                                        string json;
                                        Phabricator.Data.Account accountData = accountStorage.WhoAmI(database, browser);
                                        if (accountData == null)
                                        {
                                            json = "{\"AutoLogOutAfterMinutesOfInactivity\":1}";
                                        }
                                        else
                                        {
                                            json = JsonConvert.SerializeObject(new { accountData.Parameters.AutoLogOutAfterMinutesOfInactivity });
                                        }

                                        // send to browser
                                        httpResponse = new Http.Response.JsonMessage(json);
                                        httpResponse.Send(browser);
                                        return;

                                    default:
                                        break;
                                }
                            }
                        }
                        else
                        {
                            // return cached HTTP response
                            httpResponse.Send(browser);
                        }
                    }


                    Http.Response.JsonMessage jsonMessage = null;
                    Http.Response.File fileObject = null;
                    Http.Response.PlainTextMessage plainTextMessage = null;
                    Http.Response.Script script = null;
                    Http.Response.StyleSheet styleSheet = null;

                    // search for controller method
                    string controllerUrl;
                    string controllerUrlAlias;
                    Dictionary<MethodInfo, Http.Response.HtmlViewPage.ContentOptions> controllerMethods;
                    string[] controllerParameters;
                    object controller = GetControllerInfo(browser, ref cmdGetUrl, out controllerUrl, out controllerUrlAlias, out controllerMethods, out controllerParameters);

                    // search for view
                    string[] urlParts = cmdGetUrl.Split('/');
                    string urlPath = urlParts[1];
                    if (Http.Response.HttpMessage.ViewExists(this, urlPath) == false &&
                        controllerUrlAlias != null &&
                        controllerUrlAlias.Trim('/').Equals(urlPath.Split('?', '&').FirstOrDefault()))
                    {
                        // URL is an alias -> convert URL to real URL
                        urlPath = controllerUrl.Trim('/');
                    }

                    Http.Response.HtmlViewPage viewPage = null;
                    if (Http.Response.HttpMessage.ViewExists(this, urlPath))
                    {
                        try
                        {
                            // load view
                            viewPage = new Http.Response.HtmlViewPage(this, browser, true, urlPath, urlParts.Skip(2).ToArray());
                        }
                        catch
                        {
                            return;
                        }
                    }
                    else
                    {
                        foreach (Plugin.PluginBase plugin in Http.Server.Plugins)
                        {
                            if (plugin.State != Plugin.PluginBase.PluginState.Initialized)
                            {
                                plugin.Database = new Storage.Database(database.EncryptionKey);
                                plugin.Initialize();
                                plugin.State = Plugin.PluginBase.PluginState.Initialized;
                            }

                            if (plugin.IsVisibleInNavigator(browser))
                            {
                                viewPage = plugin.GetViewPage(browser, urlPath);
                                if (viewPage != null)
                                {
                                    break;
                                }
                            }
                        }
                    }

                    // separate action parameters from controller parameters
                    string parameterActions = string.Join("&", browser.Request.RawUrl.Split('?', '&').Skip(1));
                    if (string.IsNullOrEmpty(parameterActions) == false &&
                        controllerParameters != null &&
                        controllerParameters.Any() &&
                        controllerParameters.LastOrDefault().Equals(parameterActions)
                       )
                    {
                        controllerParameters = controllerParameters.Take(controllerParameters.Length - 1).ToArray();
                    }

                    // if controller found, process view
                    UrlControllerAttribute urlControllerAttribute = null;
                    Http.Response.HtmlViewPage.ContentOptions controllerOptions = Http.Response.HtmlViewPage.ContentOptions.Default;
                    if (controller != null)
                    {
                        // get correct GET controller method
                        KeyValuePair<MethodInfo, Http.Response.HtmlViewPage.ContentOptions> controllerMethodInfo = controllerMethods.FirstOrDefault(m => m.Key.GetParameters().Length == 5);
                        if (controllerMethodInfo.Key != null)
                        {
                            MethodInfo controllerMethod = controllerMethodInfo.Key;
                            urlControllerAttribute = controllerMethod.GetCustomAttributes(typeof(UrlControllerAttribute)).FirstOrDefault() as UrlControllerAttribute;
                            controllerOptions = controllerMethodInfo.Value;

                            // check if controller method should be impersonated executed
                            AuthenticationSchemes authenticationScheme = AuthenticationSchemes.Anonymous;
                            if (browser.WindowsIdentity != null)
                            {
                                if (urlControllerAttribute.IntegratedWindowsSecurity)
                                {
                                    authenticationScheme = AuthenticationSchemes.IntegratedWindowsAuthentication;
                                }
                            }

                            // check in case we have found a viewpage, that the controller method also supports HTML for the 3rd parameter
                            if (viewPage != null)
                            {
                                var outputParameter = controllerMethod.GetParameters().ElementAt(2).ParameterType;
                                if (outputParameter.FullName.StartsWith(typeof(Http.Response.HtmlViewPage).FullName) == false &&
                                    outputParameter.FullName.StartsWith(typeof(Http.Response.HtmlPage).FullName) == false &&
                                    outputParameter.FullName.StartsWith(typeof(Http.Response.HttpFound).FullName) == false &&
                                    outputParameter.FullName.StartsWith(typeof(Http.Response.HttpMessage).FullName) == false
                                   )
                                {
                                    viewPage = null;
                                }
                            }

                            // impersonate if needed
                            System.Security.Principal.WindowsImpersonationContext windowsImpersonationContext = null;
                            if (authenticationScheme == AuthenticationSchemes.IntegratedWindowsAuthentication)
                            {
                                IntPtr currentUserToken = ImpersonationHelper.GetCurrentUserToken();
                                WindowsIdentity currentUser = new WindowsIdentity(currentUserToken);
                                windowsImpersonationContext = currentUser.Impersonate();
                            }

                            string tokenToLog = "(unsecure)";
                            if (browser.Token != null)
                            {
                                tokenToLog = browser.Token.ID;
                            }

                            // check if session if still OK
                            if (encryptionKey == null && unsecuredUrlPaths.All(path => cmdGetUrl.StartsWith(path) == false))
                            {
                                throw new Exception.AuthorizationException();
                            }

                            // invoke method
                            Logging.WriteInfo(tokenToLog, "Invoking {0}.{1}", controller.GetType().Name, controllerMethod.Name);
                            object[] methodArguments = new object[] { this, browser, viewPage, controllerParameters, parameterActions };
                            Http.Response.HttpMessage httpResponse;
                            try
                            {
                                httpResponse = controllerMethod.Invoke(controller, methodArguments) as Http.Response.HttpMessage;
                            }
                            catch (TargetInvocationException targetInvocationException)
                            {
                                if (targetInvocationException.InnerException != null)
                                {
                                    if (targetInvocationException.InnerException is Exception.HttpNotFound)
                                    {
                                        httpResponse = null;
                                        methodArguments = new object[5];
                                    }
                                    else
                                    {
                                        ExceptionDispatchInfo.Capture(targetInvocationException.InnerException).Throw();
                                        return;
                                    }
                                }
                                else
                                {
                                    throw targetInvocationException;
                                }
                            }
                            finally
                            {
                                // un-impersonate if needed
                                if (windowsImpersonationContext != null)
                                {
                                    // Undo impersonation
                                    windowsImpersonationContext.Undo();

                                    windowsImpersonationContext.Dispose();
                                }
                            }

                            Logging.WriteInfo(tokenToLog, "Finished {0}.{1}", controller.GetType().Name, controllerMethod.Name);
                            if (httpResponse is Http.Response.HttpRedirect)
                            {
                                Http.Response.HttpRedirect redirect = httpResponse as Http.Response.HttpRedirect;
                                redirect.Send(browser);
                                return;
                            }

                            // get ref-values back
                            viewPage = methodArguments[2] as Http.Response.HtmlViewPage;
                            jsonMessage = methodArguments[2] as Http.Response.JsonMessage;
                            fileObject = methodArguments[2] as Http.Response.File;
                            plainTextMessage = methodArguments[2] as Http.Response.PlainTextMessage;
                            script = methodArguments[2] as Http.Response.Script;
                            styleSheet = methodArguments[2] as Http.Response.StyleSheet;
                        }
                    }

                    if (viewPage != null)
                    {
                        // send HTML view result to browser
                        viewPage.Theme = database.ApplicationTheme;
                        viewPage.Merge();
                        string dataSent = viewPage.Send(browser, controllerOptions);

                        if (viewPage.HttpStatusCode == 200 && parameterActions != null && parameterActions.Any() == false)
                        {
                            if (urlControllerAttribute != null && urlControllerAttribute.ServerCache)
                            {
                                lock (cachedHttpMessages)
                                {
                                    string theme = database.ApplicationTheme;
                                    cachedHttpMessages[browser.Token?.ID + theme + browser.Language + cmdGetUrl] = new CachedHttpMessage(encryptionKey, UTF8Encoding.UTF8.GetBytes(dataSent), "text/html");
                                }
                            }
                        }
                        return;
                    }

                    if (jsonMessage != null)
                    {
                        // send JSON result to browser
                        jsonMessage.Send(browser);
                        return;
                    }

                    if (fileObject != null)
                    {
                        // send file data to browser
                        fileObject.Send(browser);
                        return;
                    }

                    if (script != null)
                    {
                        // send file data to browser
                        script.Send(browser);
                        return;
                    }

                    if (styleSheet != null)
                    {
                        // send file data to browser
                        styleSheet.Send(browser);
                        return;
                    }

                    if (plainTextMessage != null)
                    {
                        // send file data to browser
                        plainTextMessage.Send(browser);
                        return;
                    }

                    // invalid url
                    Http.Response.HttpNotFound notFound = new Http.Response.HttpNotFound(this, browser, cmdGetUrl);
                    notFound.Send(browser);
                }
                finally
                {
                    if (Synchronization.InProgress == false)
                    {
                        if (cacheNextInvalidationTimestamp < DateTime.UtcNow)
                        {
                            int nbrMinutesToKeepCache = 15;               // check cache status each 15 minutes
                            int maxCacheSizeInBytes = 20 * 1024 * 1024;   // set max cache size to 20MB

                            int cacheSize;
                            lock (cachedHttpMessages)
                            {
                                // detrmine current cache size
                                cacheSize = cachedHttpMessages.Sum(cachedHttpMessage => cachedHttpMessage.Value.Size);
                            }

                            // check if cache is too big
                            if (cacheSize > maxCacheSizeInBytes)
                            {
                                // clean up non-static cache
                                InvalidateNonStaticCache(database, DateTime.UtcNow.AddMinutes(-nbrMinutesToKeepCache));
                            }

                            cacheNextInvalidationTimestamp = DateTime.UtcNow.AddMinutes(nbrMinutesToKeepCache);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// This method will determine what initial page should be shown.
        /// This can be the authentication dialog (in case of not being authenticated), 
        /// the create-user dialog  (in case of no sqlite database available)
        /// or the homepage (in case of being authenticated)
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="database"></param>
        /// <param name="token"></param>
        /// <param name="encryptionKey"></param>
        /// <param name="cmdGetUrl"></param>
        private void ProcessHttpGetInitialPageRequest(Browser browser, Storage.Database database, SessionManager.Token token, string encryptionKey, string cmdGetUrl)
        {
            bool noUserConfigured = false;
            string tokenId = browser.Request.Cookies["token"]?.Value;

            database.ValidateLogIn(tokenId, out noUserConfigured);

            Http.Response.HomePage httpResponse = new Http.Response.HomePage(this, browser, cmdGetUrl);
            if (noUserConfigured)
            {
                httpResponse.Status = Http.Response.HomePage.HomePageStatus.Initialized;
            }
            else
            {
                Storage.Account accountStorage = new Storage.Account();
                string authenticationFactor;
                if (browser.HttpServer.IsHttpModule)
                {
                    // take authentication factor over from Customization property
                    switch (Customization.AuthenticationFactor)
                    {
                        case ApplicationCustomization.ApplicationAuthenticationFactor.Public:
                            authenticationFactor = AuthenticationFactor.Public;
                            break;

                        default:
                            authenticationFactor = AuthenticationFactor.Knowledge;
                            break;
                    }
                }
                else
                {
                    // take authentication factor from Configuration screen
                    authenticationFactor = database.GetAuthenticationFactor(browser);
                }

                switch (authenticationFactor)
                {
                    case AuthenticationFactor.Public:
                        // auto-logon is configured
                        string publicEncryptionKey = database.GetConfigurationParameter("EncryptionKey");

                        // find out if public token already exists
                        bool createPublicToken = false;
                        tokenId = browser.GetCookie("token");
                        if (string.IsNullOrEmpty(tokenId))
                        {
                            createPublicToken = true;
                        }
                        else
                        {
                            token = Session.GetToken(tokenId);
                            if (token == null)
                            {
                                createPublicToken = true;
                            }
                        }

                        if (createPublicToken)
                        {
                            // logging in without username and password (auto logon)

                            // get tokenId from accountinfo
                            database.EncryptionKey = publicEncryptionKey;
                            Phabricator.Data.Account primaryUserAccount = accountStorage.Get(database, Language.NotApplicable)
                                                                                        .FirstOrDefault(account => account.Parameters.AccountType == Phabricator.Data.Account.AccountTypes.PrimaryUser);
                            tokenId = primaryUserAccount.Token;

                            // create new session token (or reuse the one with the same tokenId)
                            token = Session.CreateToken(tokenId, browser);
                            browser.SetCookie("token", token.ID, true);

                            // reinitialize session variables
                            publicEncryptionKey = Encryption.XorString(publicEncryptionKey, primaryUserAccount.PublicXorCipher);
                            token.EncryptionKey = publicEncryptionKey;
                            token.AuthenticationFactor = AuthenticationFactor.Public;
                            Session.ClientSessions[token.ID] = new SessionManager.ClientSession();
                        }
                        break;

                    case AuthenticationFactor.Knowledge:
                    default:
                        ProcessKnowledgeAuthentication(database, browser, httpResponse, authenticationFactor, tokenId, encryptionKey);
                        break;

                    case AuthenticationFactor.Ownership:
                        // get DPAPI key
                        string dpapiKey = Encryption.GetDPAPIKey();

                        // calculate database keys
                        UInt64[] dpapiXorCipherPublic, dpapiXorCipherPrivate;
                        accountStorage.GetDpapiXorCiphers(database, out dpapiXorCipherPublic, out dpapiXorCipherPrivate);
                        database.EncryptionKey = Encryption.XorString(dpapiKey, dpapiXorCipherPublic);
                        database.PrivateEncryptionKey = Encryption.XorString(dpapiKey, dpapiXorCipherPrivate);

                        // create new session token (or reuse the one with the same tokenId)
                        Phabricator.Data.Account existingAccount = null;
                        bool ownershipFailed = false;
                        try
                        {
                            existingAccount = accountStorage.Get(database, Language.NotApplicable).FirstOrDefault();
                        }
                        catch
                        {
                            ownershipFailed = true;
                        }

                        if (ownershipFailed)
                        {
                            // there was an issue with decrypting the database with the DPAPI keys -> recover to Knowledge authentication
                            database.SetConfigurationParameter("AuthenticationFactor", AuthenticationFactor.Knowledge);
                            ProcessKnowledgeAuthentication(database, browser, httpResponse, authenticationFactor, tokenId, encryptionKey);
                        }
                        else
                        {
                            tokenId = existingAccount.Token;
                            token = Session.CreateToken(tokenId, browser);
                            browser.SetCookie("token", token.ID, true);
                            token.EncryptionKey = database.EncryptionKey;
                            token.PrivateEncryptionKey = database.PrivateEncryptionKey;
                            token.AuthenticationFactor = AuthenticationFactor.Ownership;
                            Session.ClientSessions[token.ID] = new SessionManager.ClientSession();
                            Session.ClientSessions[token.ID].Locale = browser.Language;

                            // store AuthenticationFactor in database
                            database.SetConfigurationParameter("AuthenticationFactor", AuthenticationFactor.Ownership);

                            database.UpgradeIfNeeded();

                            if (browser.Request.RawUrl.Contains("?ReturnURL="))
                            {
                                string redirectURL = RootPath + browser.Request.RawUrl.Substring((RootPath + "?ReturnURL=").Length);
                                redirectURL = redirectURL.Replace("//", "/");
                                Http.Response.HttpRedirect httpRedirect = new Http.Response.HttpRedirect(browser.HttpServer, browser, redirectURL);
                                httpRedirect.Send(browser);
                                return;
                            }
                        }
                        break;
                }
            }

            httpResponse.Theme = database.ApplicationTheme;

            httpResponse.Send(browser, "");

            if (httpResponse.Status == Response.HomePage.HomePageStatus.Authenticated)
            {
                UpdateUserRoleConfiguration(database);
            }
        }

        private void ProcessKnowledgeAuthentication(Database database, Browser browser, Response.HomePage httpResponse, string authenticationFactor, string tokenId, string encryptionKey)
        {
            if (string.IsNullOrEmpty(authenticationFactor))
            {
                database.SetConfigurationParameter("AuthenticationFactor", AuthenticationFactor.Knowledge);
            }

            if (string.IsNullOrEmpty(tokenId) == false && string.IsNullOrEmpty(encryptionKey) == false)
            {
                Storage.User userStorage = new Storage.User();
                if (userStorage.Get(database, Language.NotApplicable).Any() == false &&
                    browser.Session.FormVariables["/auth/login"]?.ContainsKey("username") == true &&
                    browser.Session.FormVariables["/auth/login"]?.ContainsKey("password") == true)
                {
                    // we have a local SQLite database, but there is no data in it => try to synchronize with Phabricator server
                    httpResponse.Status = Http.Response.HomePage.HomePageStatus.EmptyDatabase;
                }
            }

            database.UpgradeIfNeeded();
        }

        /// <summary>
        /// This method will process incoming HTTP POST requests from the web browser
        /// </summary>
        /// <param name="browser"></param>
        private void ProcessHttpPostRequest(Browser browser)
        {
            string encryptionKey = null;

            // decode requested url from browser
            string cmdPostUrl = browser.Request.RawUrl;
            string contentType = browser.Request.ContentType;
            if (string.IsNullOrEmpty(contentType))
            {
                // invalid content
                return;
            }

            // read POST parameters
            if (browser.Request.ContentLength64 > 0)
            {
                byte[] rcvBuffer = new byte[0x400000];
                int bytesRead = browser.Receive(ref rcvBuffer, (int)browser.Request.ContentLength64);

                if (contentType.Equals("application/x-www-form-urlencoded"))
                {
                    browser.Session.FormVariables[browser.Request.RawUrl] = UTF8Encoding.UTF8.GetString(rcvBuffer.Take(bytesRead).ToArray())
                                                                   .Split('&')
                                                                   .ToDictionary(key => key.Split('=')[0],
                                                                                 value => HttpUtility.UrlDecode(value.Substring(value.IndexOf('=') + 1)));
                }

                if (contentType.StartsWith("multipart/form-data"))
                {
                    string postData = UTF8Encoding.UTF8.GetString(rcvBuffer, 0, (int)browser.Request.ContentLength64);
                    string mimeSeparator = postData.Split(new string[] { "\r\n" }, StringSplitOptions.None).FirstOrDefault(line => line.StartsWith("---"));
                    string[] mimeParts = postData.Split(new string[] { mimeSeparator }, StringSplitOptions.None).Skip(1).ToArray();
                    browser.Session.FormVariables[browser.Request.RawUrl] = mimeParts.Select(part => part.Trim('\r', '\n'))
                                                               .Where(part => RegexSafe.IsMatch(part.Split('\r', '\n').FirstOrDefault(), "(^|; )name=\"[^\"]*\"", System.Text.RegularExpressions.RegexOptions.None))
                                                               .ToDictionary(key => RegexSafe.Match(key, "name=\"([^\"]*)\"", System.Text.RegularExpressions.RegexOptions.None).Groups[1].Value,
                                                                             value => value.IndexOf("\r\n\r\n") == -1
                                                                                        ? ""
                                                                                        : value.Substring(value.IndexOf("\r\n\r\n") + "\r\n\r\n".Length)
                                                                            );
                }

                if (contentType.Equals("application/octet-stream"))
                {
                    browser.Session.OctetStreamData = rcvBuffer.Take(bytesRead).ToArray();
                }
            }

            // process POST request
            Logging.WriteInfo(browser.Token?.ID, "POST {0}", cmdPostUrl);

            // search for controller method
            string controllerUrl;
            string controllerUrlAlias;
            Dictionary<MethodInfo, Http.Response.HtmlViewPage.ContentOptions> controllerMethods;
            string[] controllerParameters;
            bool requestsProcessed = false;
            cmdPostUrl = RouteManager.GetInternalURL(cmdPostUrl);
            object controller = GetControllerInfo(browser, ref cmdPostUrl, out controllerUrl, out controllerUrlAlias, out controllerMethods, out controllerParameters);
            if (controller != null)
            {
                // get correct controller method
                MethodInfo controllerMethod = controllerMethods.FirstOrDefault(m => m.Key.GetParameters().Length == 3).Key;
                if (controllerMethod == null)
                {
                    Logging.WriteError(browser.Token.ID, "Controller method not found for {0}", controller.GetType().Name);
                }
                else
                {
                    requestsProcessed = true;
                    UrlControllerAttribute urlControllerAttribute = controllerMethod.GetCustomAttributes(typeof(UrlControllerAttribute)).FirstOrDefault() as UrlControllerAttribute;

                    // check if controller method should be impersonated executed
                    AuthenticationSchemes authenticationScheme = AuthenticationSchemes.Anonymous;
                    if (browser.WindowsIdentity != null)
                    {
                        if (urlControllerAttribute.IntegratedWindowsSecurity)
                        {
                            authenticationScheme = AuthenticationSchemes.IntegratedWindowsAuthentication;
                        }
                    }

                    // impersonate if needed
                    System.Security.Principal.WindowsImpersonationContext windowsImpersonationContext = null;
                    if (authenticationScheme == AuthenticationSchemes.IntegratedWindowsAuthentication)
                    {
                        IntPtr currentUserToken = ImpersonationHelper.GetCurrentUserToken();
                        if (currentUserToken != IntPtr.Zero)
                        {
                            WindowsIdentity currentUser = new WindowsIdentity(currentUserToken);
                            windowsImpersonationContext = currentUser.Impersonate();
                        }
                    }

                    if (cmdPostUrl.StartsWith("/auth/") == false)
                    {
                        // verify if session is still OK
                        string tokenId = browser.Request.Cookies["token"]?.Value;
                        if (tokenId != null)
                        {
                            encryptionKey = Session.GetToken(tokenId)?.EncryptionKey;
                            if (encryptionKey == null)
                            {
                                throw new Exception.AuthorizationException();
                            }
                        }
                    }

                    // invoke method
                    Http.Response.HttpMessage httpResponse = null;
                    Logging.WriteInfo(browser.Token?.ID, "Invoking {0}.{1}", controller.GetType().Name, controllerMethod.Name);
                    try
                    {
                        Plugin.PluginController pluginController = controller as Plugin.PluginController;
                        if (pluginController != null)
                        {
                            if (browser.Session.FormVariables[browser.Request.RawUrl]?.ContainsKey("confirm") == true)
                            {
                                // controller method belongs of plugin -> load app-specific formdata data from browser
                                Plugin.PluginBase pluginClass = Plugins.FirstOrDefault(plugin => plugin.Assembly.Equals(controllerMethod.Module.Assembly));

                                Plugin.PluginTypeAttribute.UsageType[] pluginUsages = pluginClass.GetType()
                                                                                                 .GetCustomAttributes(typeof(Plugin.PluginTypeAttribute))
                                                                                                 .OfType<Plugin.PluginTypeAttribute>()
                                                                                                 .Select(pluginTypeAttribute => pluginTypeAttribute.Usage)
                                                                                                 .ToArray();
                                if (pluginUsages.Contains(Plugin.PluginTypeAttribute.UsageType.ManiphestTask))
                                {
                                    pluginController.ManiphestTaskData = new Plugin.PluginController.ManiphestTaskDataType();
                                    pluginController.ManiphestTaskData.ConfirmState = (Plugin.PluginController.ConfirmResponse)Enum.Parse(typeof(Plugin.PluginController.ConfirmResponse), browser.Session.FormVariables[browser.Request.RawUrl]["confirm"]);
                                    pluginController.ManiphestTaskData.TaskID = browser.Session.FormVariables[browser.Request.RawUrl]["taskID"];
                                }

                                if (pluginUsages.Contains(Plugin.PluginTypeAttribute.UsageType.PhrictionDocument))
                                {
                                    pluginController.PhrictionData = new Plugin.PluginController.PhrictionDataType();
                                    pluginController.PhrictionData.ConfirmState = (Plugin.PluginController.ConfirmResponse)Enum.Parse(typeof(Plugin.PluginController.ConfirmResponse), browser.Session.FormVariables[browser.Request.RawUrl]["confirm"]);
                                    pluginController.PhrictionData.Content = browser.Session.FormVariables[browser.Request.RawUrl]["content"];
                                    pluginController.PhrictionData.Crumbs = browser.Session.FormVariables[browser.Request.RawUrl]["crumbs"];
                                    pluginController.PhrictionData.IsPrepared = bool.Parse(browser.Session.FormVariables[browser.Request.RawUrl]["isPrepared"]);
                                    pluginController.PhrictionData.Path = browser.Session.FormVariables[browser.Request.RawUrl]["path"];
                                    pluginController.PhrictionData.TOC = browser.Session.FormVariables[browser.Request.RawUrl]["toc"];
                                }
                            }
                        }

                        httpResponse = controllerMethod.Invoke(controller, new object[] { this, browser, controllerParameters }) as Http.Response.HttpMessage;
                    }
                    catch (System.Exception httpResponseException)
                    {
                        if (httpResponseException is InvalidCSRFException || httpResponseException.InnerException is InvalidCSRFException)
                        {
                            httpResponse = new Http.Response.InvalidCSRF(this, browser, cmdPostUrl);
                        }
                    }
                    finally
                    {
                        // un-impersonate if needed
                        if (windowsImpersonationContext != null)
                        {
                            // Undo impersonation
                            windowsImpersonationContext.Undo();

                            windowsImpersonationContext.Dispose();
                        }
                    }
                    Logging.WriteInfo(browser.Token?.ID, "Finished {0}.{1}", controller.GetType().Name, controllerMethod.Name);

                    // reply to browser that POST-call is finished
                    if (httpResponse == null)
                    {
                        httpResponse = new Http.Response.HttpFound(this, browser, cmdPostUrl);
                    }
                    httpResponse.Send(browser);
                }
            }


            if (requestsProcessed == false)
            {
                // reply to browser that POST-call is finished
                Http.Response.HttpNotFound httpResponse = new Http.Response.HttpNotFound(this, browser, cmdPostUrl);
                httpResponse.Send(browser);
            }
        }

        /// <summary>
        /// This method will process any incoming request (i.e. POST/GET, HTTP/Websocket)
        /// </summary>
        /// <param name="httpListenerContext"></param>
        public void ProcessHttpRequest(Miscellaneous.HttpListenerContext httpListenerContext)
        {
            try
            {
                if (httpListenerContext.IsWebSocketRequest)
                {
                    httpListenerContext.AcceptWebSocket();
                }
                else
                {
                    using (Browser browser = new Browser(this, httpListenerContext))
                    {
                        if (RemoteAccessEnabled == false)
                        {
                            if (httpListenerContext.Request.IsLocal == false)
                            {
                                bool isStaticData = httpListenerContext.Request.RawUrl.StartsWith("/favicon.ico", StringComparison.OrdinalIgnoreCase)
                                                 || httpListenerContext.Request.RawUrl.StartsWith("/fonts/", StringComparison.OrdinalIgnoreCase)
                                                 || httpListenerContext.Request.RawUrl.StartsWith("/css/", StringComparison.OrdinalIgnoreCase)
                                                 || httpListenerContext.Request.RawUrl.StartsWith("/js/", StringComparison.OrdinalIgnoreCase);
                                if (isStaticData == false)
                                {
                                    AccessDeniedException accessDeniedException = new AccessDeniedException("/", "Remote access is disabled");
                                    ProcessAccessDeniedException(browser, accessDeniedException);
                                    return;
                                }
                            }
                        }

                        // copy WindowsIdentity from context to browser object
                        browser.WindowsIdentity = httpListenerContext.WindowsIdentity;

                        try
                        {
                            browser.Conduit = new Conduit(this, browser);

                            // === Process GET commando ==================================================================================================================================
                            if (browser.Request.HttpMethod.Equals("GET"))
                            {
                                ProcessHttpGetRequest(browser);
                            }
                            else
                            // === Process POST commando =================================================================================================================================
                            if (browser.Request.HttpMethod.Equals("POST"))
                            {
                                ProcessHttpPostRequest(browser);

                                if (browser.Request.RawUrl.Equals("/auth/login") == false)  // we can't delete /auth/login immeditely because of the two-step authentication
                                {
                                    browser.Session.FormVariables.Remove(browser.Request.RawUrl);
                                }
                            }
                        }
                        catch (AuthorizationException authorizationException)
                        {
                            ProcessAuthorizationException(browser, authorizationException);
                        }
                        catch (AccessDeniedException accessDeniedException)
                        {
                            ProcessAccessDeniedException(browser, accessDeniedException);
                        }
                        catch (CryptographicException)
                        {
                            // unable to decode database -> access denied
                            AccessDeniedException accessDeniedException = new AccessDeniedException("/", "Invalid credentials");
                            ProcessAccessDeniedException(browser, accessDeniedException);
                        }
                        catch (HttpListenerException)
                        {
                            throw;
                        }
                        catch (HttpNotFound notFoundException)
                        {
                            Http.Response.HttpNotFound notFound = new Http.Response.HttpNotFound(this, browser, notFoundException.Url);
                            notFound.Send(browser);
                        }
                        catch (InvalidConfigurationException invalidConfigurationException)
                        {
                            ProcessInvalidConfigurationException(browser, invalidConfigurationException);
                        }
                        catch (System.Exception exception)
                        {
                            Logging.WriteException(browser.Token?.ID, exception);
                            SendExceptionToBrowser(browser, exception);
                        }
                    }
                }
            }
            catch (System.Exception exception)
            {
                HttpListenerException httpListenerException = exception as HttpListenerException;
                if (httpListenerException != null)
                {
                    if (httpListenerException.ErrorCode == 995)
                    {
                        // The I/O operation has been aborted because of either a thread exit or an application request
                        return;
                    }
                }

                AuthorizationException authorizationException = exception as AuthorizationException;
                if (authorizationException != null)
                {
                    using (Browser browser = new Browser(this, httpListenerContext))
                    {
                        ProcessAuthorizationException(browser, authorizationException);
                        return;
                    }
                }

                Logging.WriteException(null, exception);
            }
        }

        /// <summary>
        /// This method is executed for each HTTP request received from the web browser
        /// </summary>
        /// <param name="ar">asyncResult</param>
        private void ProcessHttpRequest(IAsyncResult asyncResult)
        {
            Miscellaneous.HttpListenerContext context = null;

            try
            {
                context = httpListener.EndGetContext(asyncResult);
            }
            catch (System.Exception exception)
            {
                HttpListenerException httpListenerException = exception as HttpListenerException;
                if (httpListenerException != null)
                {
                    if (httpListenerException.ErrorCode == 995)
                    {
                        // The I/O operation has been aborted because of either a thread exit or an application request
                        return;
                    }
                }

                AuthorizationException authorizationException = exception as AuthorizationException;
                if (authorizationException != null)
                {
                    using (Browser browser = new Browser(this, context))
                    {
                        ProcessAuthorizationException(browser, authorizationException);
                        return;
                    }
                }

                Logging.WriteException(null, exception);
            }

            Task.Factory.StartNew((Object obj) =>
            {
                var data = (dynamic)obj;
                Miscellaneous.HttpListenerContext httpListenerContext = data.context;

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
        /// This method is executed when an InvalidConfigurationException is thrown.
        /// This will trigger an InvalidConfiguration error screen
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="accessDeniedException"></param>
        private void ProcessInvalidConfigurationException(Browser browser, InvalidConfigurationException invalidConfigurationException)
        {
            Response.HtmlViewPage htmlViewPage = new Response.HtmlViewPage(browser);
            htmlViewPage.SetContent(browser, htmlViewPage.GetViewData("InvalidConfiguration"));
            htmlViewPage.SetText("ERROR-MESSAGE", invalidConfigurationException.ErrorMessage);
            htmlViewPage.SetText("APP-CONFIG-FILENAME", AppConfigLoader.ConfigFileName);
            htmlViewPage.SetText("PHABRICO-ROOTPATH", Http.Server.RootPath, Response.HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
            htmlViewPage.Merge();
            htmlViewPage.Send(browser);
        }

        /// <summary>
        /// Sends a stacktrace of an exception to the web browser
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="exception"></param>
        private void SendExceptionToBrowser(Browser browser, System.Exception exception)
        {
            string redirectReasonField1 = exception.GetType().FullName;
            string redirectReasonField2 = exception.Message;
            string redirectReasonField3 = exception.StackTrace;

            if (redirectReasonField1.Length + redirectReasonField2.Length + redirectReasonField3.Length > 1300)
            {
                int positionLastNewLine = redirectReasonField3.LastIndexOf('\n');
                redirectReasonField3 = redirectReasonField3.Substring(0, positionLastNewLine).Trim('\r', '\n');
            }

            string redirectReason = "?data="
                                  + Convert.ToBase64String(UTF8Encoding.UTF8.GetBytes(redirectReasonField1)) + "/"
                                  + Convert.ToBase64String(UTF8Encoding.UTF8.GetBytes(redirectReasonField2)) + "/"
                                  + Convert.ToBase64String(UTF8Encoding.UTF8.GetBytes(redirectReasonField3));

            string url = RootPath + "exception/" + redirectReason;
            Http.Response.HttpRedirect httpResponse = null;
            httpResponse = new Http.Response.HttpRedirect(this, browser, url);
            httpResponse.Send(browser);
        }

        /// <summary>
        /// Pushes the latest notification messages to all the browsers
        /// This is usefull when a browser connects and receives immediately the latests states
        /// </summary>
        public static void SendLatestNotifications()
        {
            foreach (string webSocketMessageIdentifier in currentNotifications.Keys.ToArray())
            {
                foreach (WebSocketContext webSocketContext in WebSockets.ToArray().Where(websocket => websocket != null).ToArray())
                {
                    try
                    {
                        if (webSocketContext.RequestUri.LocalPath.TrimEnd('/')
                                            .Equals(webSocketMessageIdentifier.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                        {
                            Logging.WriteInfo("Notify", webSocketContext.RequestUri.LocalPath);
                            byte[] data = UTF8Encoding.UTF8.GetBytes(currentNotifications[webSocketMessageIdentifier]);
                            webSocketContext.WebSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                    }
                    catch
                    {
                        WebSockets.Remove(webSocketContext);
                    }
                }
            }
        }

        /// <summary>
        /// Pushes an informational message to one or more browser sessions via WebSockets.
        /// </summary>
        /// <param name="webSocketMessageIdentifier">
        /// Local path of websocket
        /// For example: if websocket connects to http://localhost:13467/abc/def the webSocketMessageIdentifier should be /abc/def
        /// If there are multiple browser sessions connected to this identifier, they will all receive a message
        /// </param>
        /// <param name="message">data to be sent to browser</param>
        public static void SendNotificationInformation(string webSocketMessageIdentifier, string message)
        {
            string jsonData = JsonConvert.SerializeObject(new
            {
                Message = message,
                Type = "info"
            });

            // prepend root-path
            webSocketMessageIdentifier = RootPath.TrimEnd('/') + webSocketMessageIdentifier;

            currentNotifications[webSocketMessageIdentifier] = jsonData;

            foreach (WebSocketContext webSocketContext in WebSockets.Where(websocket => websocket != null
                                                                                     && websocket.RequestUri
                                                                                                 .LocalPath
                                                                                                 .TrimEnd('/')
                                                                                                 .Equals(webSocketMessageIdentifier.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                                                                          )
                                                                    .ToArray())
            {
                Logging.WriteInfo("Notify-Info", webSocketContext.RequestUri.LocalPath);
                byte[] data = UTF8Encoding.UTF8.GetBytes(currentNotifications[webSocketMessageIdentifier]);
                webSocketContext.WebSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        /// <summary>
        /// Pushes an error message to one or more browser sessions via WebSockets
        /// </summary>
        /// <param name="webSocketMessageIdentifier">
        /// Local path of websocket
        /// For example: if websocket connects to http://localhost:13467/abc/def the webSocketMessageIdentifier should be /abc/def
        /// If there are multiple browser sessions connected to this identifier, they will all receive a message
        /// </param>
        /// <param name="message">data to be sent to browser</param>
        public static void SendNotificationError(string webSocketMessageIdentifier, string message)
        {
            string jsonData = JsonConvert.SerializeObject(new
            {
                Message = message,
                Type = "error"
            });

            // prepend root-path
            webSocketMessageIdentifier = RootPath.TrimEnd('/') + webSocketMessageIdentifier;

            currentNotifications[webSocketMessageIdentifier] = jsonData;

            foreach (HttpListenerWebSocketContext webSocketContext in WebSockets.Where(websocket => websocket != null
                                                                                                 && websocket.RequestUri
                                                                                                             .LocalPath
                                                                                                             .TrimEnd('/')
                                                                                                             .Equals(webSocketMessageIdentifier.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                                                                                      )
                                                                                .ToArray())
            {
                Logging.WriteInfo("Notify-Error", webSocketContext.RequestUri.LocalPath);
                byte[] data = UTF8Encoding.UTF8.GetBytes(currentNotifications[webSocketMessageIdentifier]);
                webSocketContext.WebSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        /// <summary>
        /// Pushes an error message to one or more browser sessions via WebSockets
        /// </summary>
        /// <param name="webSocketMessageIdentifier">
        /// Local path of websocket
        /// For example: if websocket connects to http://localhost:13467/abc/def the webSocketMessageIdentifier should be /abc/def
        /// If there are multiple browser sessions connected to this identifier, they will all receive a message
        /// </param>
        /// <param name="message">data to be sent to browser</param>
        public static void SendNotificationWarning(string webSocketMessageIdentifier, string message)
        {
            string jsonData = JsonConvert.SerializeObject(new
            {
                Message = message,
                Type = "warning"
            });

            // prepend root-path
            webSocketMessageIdentifier = RootPath.TrimEnd('/') + webSocketMessageIdentifier;

            currentNotifications[webSocketMessageIdentifier] = jsonData;

            foreach (HttpListenerWebSocketContext webSocketContext in WebSockets.Where(websocket => websocket != null
                                                                                                 && websocket.RequestUri
                                                                                                             .LocalPath
                                                                                                             .TrimEnd('/')
                                                                                                             .Equals(webSocketMessageIdentifier.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                                                                                      )
                                                                                .ToArray())
            {
                Logging.WriteInfo("Notify-Warning", webSocketContext.RequestUri.LocalPath);
                byte[] data = UTF8Encoding.UTF8.GetBytes(currentNotifications[webSocketMessageIdentifier]);
                webSocketContext.WebSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        /// <summary>
        /// Finalizes an instance of the Http.Server class.
        /// </summary>
        public void Stop()
        {
            // terminates all the plugins
            foreach (Plugin.PluginBase plugin in Plugins)
            {
                plugin.UnlLoad();
            }

            if (IsHttpModule == false)
            {
                // terminates the TCP socket listener
                this.httpListener.Stop();
            }

            // wait until we're not caching any more
            for (int timeout=100; timeout > 0 && cacheStatusHttpMessages == CacheState.Busy; timeout--)
            {
                Thread.Sleep(100);
            }

            // dispose all the plugins
            foreach (Plugin.PluginBase plugin in Plugins)
            {
                plugin.Dispose();
            }
            Plugins.Clear();

            numberOfInstancesCreated--;
        }

        /// <summary>
        /// Updates the user role configuration in memory
        /// </summary>
        /// <param name="database"></param>
        internal void UpdateUserRoleConfiguration(Storage.Database database)
        {
            if (UserRoles == null)
            {
                UserRoles = new Dictionary<string, string[]>();
            }

            lock (UserRoles)
            {
                Storage.Account accountStorage = new Storage.Account();

                UserRoles = accountStorage.Get(database, Language.NotApplicable)
                                          .ToDictionary(user => user.UserName,
                                                        user => (user.Parameters.DefaultUserRoleTag ?? "")
                                                                               .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)

                                                       );
            }
        }

        /// <summary>
        /// Validates if the user is using a supported web browser
        /// </summary>
        /// <param name="userAgent"></param>
        /// <returns></returns>
        private bool UserAgentIsSupported(string userAgent)
        {
            bool isMicrosoftInternetExplorer = RegexSafe.IsMatch(userAgent, "([,;(] ?MSIE |Trident)", System.Text.RegularExpressions.RegexOptions.None);

            return isMicrosoftInternetExplorer == false;
        }

        /// <summary>
        /// Returns true if all given userRoleTags belong to the current user
        /// </summary>
        /// <param name="database">Phabrico database</param>
        /// <param name="browser">Reference to browser</param>
        /// <param name="phabricatorObject">PhabricatorObject to verify</param>
        /// <returns></returns>
        internal bool ValidUserRoles(Storage.Database database, Browser browser, Phabricator.Data.PhabricatorObject phabricatorObject)
        {
            Storage.Account accountStorage = new Storage.Account();
            Phabricator.Data.Account whoAmI = accountStorage.WhoAmI(database, browser);
            if (whoAmI == null)
            {
                // this can happen during unit tests which are running too fast -> this is not critical, so skip it
                return false;
            }

            if (whoAmI.Parameters.AccountType == Phabricator.Data.Account.AccountTypes.PrimaryUser)
            {
                if (browser.Token.PrivateEncryptionKey != null)
                {
                    // primary user has all access
                    return true;
                }
            }

            if (UserRoles == null || UserRoles.ContainsKey(whoAmI.UserName) == false)
            {
                UpdateUserRoleConfiguration(database);
            }

            Dictionary<string, string[]> userRoles;
            lock (UserRoles)
            {
                userRoles = new Dictionary<string, string[]>(UserRoles.Where(kvp => kvp.Value.Any())
                                                                      .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                                                            );
            }

            if (userRoles.Any())
            {
                string[] availableUserRoles = userRoles.SelectMany(kvp => kvp.Value).ToArray();

                string[] myUserRoles;
                if (userRoles.TryGetValue(whoAmI.UserName, out myUserRoles) == false)
                {
                    // can happen when logging in with public account (which is linked to the primary user account)
                    myUserRoles = new string[0];  // assign no user roles
                }

                Phabricator.Data.Phriction phrictionDocument = phabricatorObject as Phabricator.Data.Phriction;
                if (phrictionDocument != null)
                {
                    Storage.Phriction phrictionStorage = new Storage.Phriction();

                    string[] pathElements = phrictionDocument.Path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries).ToArray();
                    while (true)
                    {
                        string[] phrictionDocumentUserRoleTags = phrictionDocument.Projects
                                                                                  .Split(',')
                                                                                  .Where(tag => string.IsNullOrWhiteSpace(tag) == false)
                                                                                  .ToArray();

                        foreach (string phrictionDocumentUserRoleTag in phrictionDocumentUserRoleTags)
                        {
                            if (availableUserRoles.Contains(phrictionDocumentUserRoleTag) &&
                                myUserRoles.Contains(phrictionDocumentUserRoleTag) == false
                               )
                            {
                                // access denied
                                return false;
                            }
                        }

                        // go to parent document
                        pathElements = pathElements.Take(pathElements.Length - 1).ToArray();
                        if (pathElements.Any() == false) break;  // we're at the root -> stop

                        string parentPath = string.Join("/", pathElements) + "/";
                        phrictionDocument = phrictionStorage.Get(database, parentPath, browser.Session.Locale, false);
                        if (phrictionDocument == null) break;  // parent not document found -> stop
                    }
                }

                Phabricator.Data.Maniphest maniphestTask = phabricatorObject as Phabricator.Data.Maniphest;
                if (maniphestTask != null)
                {
                    string[] maniphestTaskUserRoleTags = maniphestTask.Projects
                                                                      .Split(',')
                                                                      .Where(tag => string.IsNullOrWhiteSpace(tag) == false)
                                                                      .ToArray();

                    foreach (string maniphestTaskUserRoleTag in maniphestTaskUserRoleTags)
                    {
                        if (availableUserRoles.Contains(maniphestTaskUserRoleTag) &&
                            myUserRoles.Contains(maniphestTaskUserRoleTag) == false
                           )
                        {
                            // access denied
                            return false;
                        }
                    }
                }

                Phabricator.Data.Project project = phabricatorObject as Phabricator.Data.Project;
                if (project != null)
                {
                    if (availableUserRoles.Contains(project.Token) &&
                        myUserRoles.Contains(project.Token) == false
                       )
                    {
                        // access denied
                        return false;
                    }
                }
            }

            // default: everyone has access
            return true;
        }
    }
}