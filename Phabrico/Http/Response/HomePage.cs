using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Miscellaneous;
using Phabrico.Phabricator.Data;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Phabrico.Http.Response.HtmlViewPage;

namespace Phabrico.Http.Response
{
    internal class HomePage : HtmlPage
    {
        public enum HomePageStatus
        {
            /// <summary>
            /// Initial state: not applicable in normal progress
            /// </summary>
            None,

            /// <summary>
            /// There was no local SQLite database found: database will be created and the "HomePage.AuthenticationDialogCreateUser" dialog will be shown
            /// </summary>
            Initialized,

            /// <summary>
            /// Username and password are valid
            /// </summary>
            Authenticated,

            /// <summary>
            /// Invalid username or password were entered in the logon dialog
            /// </summary>
            AuthenticationError,

            /// <summary>
            /// Local SQLite database was created, but contains no data -> synchronize from Phabricator server
            /// </summary>
            EmptyDatabase
        }

        public HomePageStatus Status { get; set; }

        /// <summary>
        /// Initializes a new HTTP object which identifies the initial homepage
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="url"></param>
        internal HomePage(Http.Server httpServer, Browser browser, string url)
            : base(httpServer, browser, url)
        {
            Status = HomePageStatus.None;
        }

        /// <summary>
        /// Converts a numeric filesize value into a human readable filesize
        /// E.g. 2048 becomes 2KB
        /// </summary>
        /// <param name="fileSize"></param>
        /// <returns></returns>
        private string ConvertFileSizeIntoReadableString(long fileSize)
        {
            // Get absolute value
            long absolute_i = (fileSize < 0 ? -fileSize : fileSize);
            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (absolute_i >= 0x1000000000000000) // Exabyte
            {
                suffix = "EB";
                readable = (fileSize >> 50);
            }
            else if (absolute_i >= 0x4000000000000) // Petabyte
            {
                suffix = "PB";
                readable = (fileSize >> 40);
            }
            else if (absolute_i >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = (fileSize >> 30);
            }
            else if (absolute_i >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = (fileSize >> 20);
            }
            else if (absolute_i >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = (fileSize >> 10);
            }
            else if (absolute_i >= 0x400) // Kilobyte
            {
                suffix = "KB";
                readable = fileSize;
            }
            else if (fileSize < 0) // no data
            {
                return "0 B";
            }
            else
            {
                return fileSize.ToString("0 B"); // Byte
            }
            // Divide by 1024 to get fractional value
            readable = (readable / 1024);
            // Return formatted number with suffix
            return readable.ToString("0.# ") + suffix;
        }

        /// <summary>
        /// Returns the crumb path of a given Phriction document
        /// </summary>
        /// <param name="database"></param>
        /// <param name="phrictionDocument"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        private object GetCrumbPath(Storage.Database database, Phabricator.Data.Phriction phrictionDocument, Language language)
        {
            string urlCrumbs = Controllers.Phriction.GenerateCrumbs(database, phrictionDocument, language);
            JArray crumbs = JsonConvert.DeserializeObject(urlCrumbs) as JArray;
            string result = string.Join(" > ", crumbs.Where(t => ((JValue)t["inexistant"]).Value.Equals(false))
                                                     .Select(t => ((JValue)t["name"]).Value).ToArray()
                                       );
            if (string.IsNullOrEmpty(result))
            {
                result = phrictionDocument.Name;
            }

            return result;
        }

        /// <summary>
        /// Retrieves the HTML content of the homepage
        /// This depends on the HomePageStatus
        /// </summary>
        /// <param name="browser"></param>
        /// <returns></returns>
        private string GetPageContent(Browser browser)
        {
            SessionManager.Token token;
            string userName, authenticationFactor;
            Storage.Account accountStorage = new Storage.Account();
            Phabricator.Data.Account accountData;
            HtmlViewPage htmlViewPage = new HtmlViewPage(browser);
            HtmlViewPage htmlPartialViewPage = new HtmlViewPage(browser);
            HtmlViewPage htmlPartialHeaderViewPage = new HtmlViewPage(browser);

            switch (Status)
            {
                case HomePageStatus.Authenticated:
                    token = SessionManager.GetToken(browser);
                    browser.ResetToken(token);
                    browser.Session.Locale = browser.Properties.Language;  // restore locale

                    string encryptionKey = token.EncryptionKey;

                    using (Storage.Database database = new Storage.Database(encryptionKey))
                    {
                        database.PrivateEncryptionKey = token.PrivateEncryptionKey;

                        authenticationFactor = database.GetAuthenticationFactor(browser);

                        database.ClearOldSessionVariables(browser);

                        accountData = accountStorage.WhoAmI(database, browser);

                        // get favorites
                        Storage.Phriction phrictionStorage = new Storage.Phriction();
                        List<Phabricator.Data.Phriction> favoritePhrictionDocuments = new List<Phabricator.Data.Phriction>();
                        if (HttpServer.Customization.HidePhrictionFavorites == false)
                        {
                            favoritePhrictionDocuments = phrictionStorage.GetFavorites(database, browser, accountData.UserName).ToList();
                        }

                        var favorites = favoritePhrictionDocuments.Select(favorite => new
                        {
                            token = favorite.Token,
                            url = favorite.Path == "/"
                                        ? "w/"
                                        : "w/" + favorite.Path,
                            title = GetCrumbPath(database, favorite, browser.Properties.Language),
                            order = favorite.DisplayOrderInFavorites
                        })
                        .ToArray();

                        htmlPartialViewPage.SetContent(browser, GetViewData("HomePage.Authenticated"));
                        htmlPartialViewPage.SetText("PHABRICO-ROOTPATH", Http.Server.RootPath, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("PHABRICO-VERSION", VersionInfo.Version, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("PHABRICO-BUILD-DATE", Controllers.Controller.FormatDateTimeOffset(VersionInfo.BuildDateTimeUtc.ToLocalTime(), browser.Session.Locale), HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlPartialViewPage.SetText("LAST-SYNCHRONIZATION-TIME", HttpServer.GetLatestSynchronizationTime(token, browser.Session.Locale), HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlPartialViewPage.SetText("PHABRICO-DATABASE-LOCATION", database.Connection.FileName, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlPartialViewPage.SetText("PHABRICO-DATABASE-SIZE", ConvertFileSizeIntoReadableString(database.GetFileSize()), HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlPartialViewPage.SetText("PHABRICO-REMOTE-ACCESS", HttpServer.RemoteAccessEnabled ? "RemoteAccess" : "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("PHABRICO-NUMBER-PHRICTION-DOCUMENTS", Storage.Phriction.Count(database).ToString(), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("PHABRICO-NUMBER-MANIPHEST-TASKS", Storage.Maniphest.Count(database).ToString(), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("PHABRICO-NUMBER-PROJECTS", Storage.Project.Count(database).ToString(), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("PHABRICO-NUMBER-USERS", Storage.User.Count(database).ToString(), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("PHABRICO-NUMBER-FILE-OBJECTS", Storage.File.Count(database).ToString(), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("PHABRICO-FILE-OBJECTS-MEDIAN-SIZE", ConvertFileSizeIntoReadableString(Storage.File.MedianSize(database)), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("PHABRICO-FILE-OBJECTS-MAXIMUM-SIZE", ConvertFileSizeIntoReadableString(Storage.File.MaximumSize(database)), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("PHABRICO-NUMBER-UNCOMMITTED-OBJECTS", Storage.Stage.CountUncommitted(database).ToString(), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("PHABRICO-NUMBER-FROZEN-OBJECTS", Storage.Stage.CountFrozen(database).ToString(), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("AUTHENTICATION-FACTOR", authenticationFactor, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlPartialViewPage.SetText("HAS-FAVORITES", favorites.Any() ? "True" : "False", HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlPartialViewPage.SetText("FAVORITES", JsonConvert.SerializeObject(favorites), HtmlViewPage.ArgumentOptions.JsonEncoding);

                        htmlPartialViewPage.Customize(browser);
                        htmlPartialViewPage.Merge();

                        int nbrMarkedFileObjects = database.GetAllMarkedFileIDs().Count();
                        Http.Server.SendNotificationError("/errorinaccessiblefiles/notification", nbrMarkedFileObjects.ToString());

                        htmlViewPage.SetContent(browser, GetViewData("HomePage.TreeView.Template"));
                        htmlViewPage.SetText("AUTOLOGOUTAFTERMINUTESOFINACTIVITY", database.GetAccountConfiguration()?.AutoLogOutAfterMinutesOfInactivity.ToString(), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("CONTENT", htmlPartialViewPage.Content, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlViewPage.SetText("AUTHENTICATION-FACTOR", authenticationFactor, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlViewPage.SetText("ANY-INACCESSIBLE-FILES", nbrMarkedFileObjects > 0 ? "True" : "False", ArgumentOptions.NoHtmlEncoding);
                        htmlViewPage.Customize(browser);

                        foreach (Plugin.PluginBase plugin in Http.Server.Plugins)
                        {
                            Plugin.PluginTypeAttribute pluginType = plugin.GetType().GetCustomAttributes(typeof(Plugin.PluginTypeAttribute), true).FirstOrDefault() as Plugin.PluginTypeAttribute;
                            if (pluginType != null && pluginType.Usage != Plugin.PluginTypeAttribute.UsageType.Navigator) continue;

                            plugin.CurrentUsageType = Plugin.PluginTypeAttribute.UsageType.Navigator;

                            if (plugin.IsVisibleInNavigator(browser)
                                && (browser.HttpServer.Customization.HidePlugins.ContainsKey(plugin.GetType().Name) == false
                                    || browser.HttpServer.Customization.HidePlugins[plugin.GetType().Name] == false
                                    )
                               )
                            {
                                if (plugin.State == Plugin.PluginBase.PluginState.Loaded)
                                {
                                    plugin.Database = new Storage.Database(database.EncryptionKey);
                                    plugin.Initialize();
                                    plugin.State = Plugin.PluginBase.PluginState.Initialized;
                                }

                                HtmlPartialViewPage htmlPluginNavigatorMenuItem = htmlViewPage.GetPartialView("PLUGINS");
                                if (htmlPluginNavigatorMenuItem != null)
                                {
                                    htmlPluginNavigatorMenuItem.SetText("PLUGIN-URL", plugin.URL, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                    htmlPluginNavigatorMenuItem.SetText("PLUGIN-ICON", plugin.Icon, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                    htmlPluginNavigatorMenuItem.SetText("PLUGIN-NAME", plugin.GetName(browser.Session.Locale), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                    htmlPluginNavigatorMenuItem.SetText("PLUGIN-DESCRIPTION", plugin.GetDescription(browser.Session.Locale), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                }
                            }

                            foreach (Plugin.PluginWithoutConfigurationBase pluginExtension in plugin.Extensions
                                                                                                    .Where(ext => ext.IsVisibleInNavigator(browser)
                                                                                                               && (browser.HttpServer.Customization.HidePlugins.ContainsKey(ext.GetType().Name) == false
                                                                                                                   || browser.HttpServer.Customization.HidePlugins[ext.GetType().Name] == false
                                                                                                                   )
                                                                                                          )
                                    )
                            {
                                if (pluginExtension.State == Plugin.PluginBase.PluginState.Loaded)
                                {
                                    pluginExtension.Database = new Storage.Database(database.EncryptionKey);
                                    pluginExtension.Initialize();
                                    pluginExtension.State = Plugin.PluginBase.PluginState.Initialized;
                                }

                                HtmlPartialViewPage htmlPluginNavigatorMenuItem = htmlViewPage.GetPartialView("PLUGINS");
                                if (htmlPluginNavigatorMenuItem != null)
                                {
                                    htmlPluginNavigatorMenuItem.SetText("PLUGIN-URL", pluginExtension.URL, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                    htmlPluginNavigatorMenuItem.SetText("PLUGIN-ICON", pluginExtension.Icon, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                    htmlPluginNavigatorMenuItem.SetText("PLUGIN-NAME", pluginExtension.GetName(browser.Session.Locale), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                    htmlPluginNavigatorMenuItem.SetText("PLUGIN-DESCRIPTION", pluginExtension.GetDescription(browser.Session.Locale), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                }
                            }
                        }

                        Storage.PhamePost phamePostStorage = new Storage.PhamePost();
                        foreach (string blogName in phamePostStorage.Get(database, browser.Session.Locale).Select(post => post.Blog).Distinct().OrderBy(blog => blog))
                        {
                            HtmlPartialViewPage htmlPhameBlogsMenuItem = htmlViewPage.GetPartialView("PHAME-BLOGS");
                            if (htmlPhameBlogsMenuItem != null)
                            {
                                htmlPhameBlogsMenuItem.SetText("PHAME-BLOG-NAME", blogName, HtmlViewPage.ArgumentOptions.Default);
                            }
                        }

                        htmlViewPage.Merge();
                        htmlPartialViewPage = htmlViewPage;

                        accountData = accountStorage.WhoAmI(database, browser);
                        userName = accountData.UserName;

                        string languageCookie = browser.GetCookie("language");
                        if (string.IsNullOrEmpty(languageCookie))
                        {
                            browser.Session.Locale = browser.Properties.Language;
                        }
                        else
                        {
                            browser.Session.Locale = languageCookie;
                        }

                        htmlPartialHeaderViewPage.SetContent(browser, GetViewData("HomePage.Authenticated.HeaderActions"));
                        htmlPartialHeaderViewPage.SetText("AUTHENTICATION-FACTOR", authenticationFactor, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlPartialHeaderViewPage.Customize(browser);
                        htmlPartialHeaderViewPage.Merge();
                    }

                    htmlViewPage = new HtmlViewPage(browser);
                    htmlViewPage.SetContent(browser, GetViewData("HomePage.Template"));
                    htmlViewPage.SetText("THEME", Theme, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("THEME-STYLE", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("LOCALE", Browser.Session.Locale, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("HEADERACTIONS", htmlPartialHeaderViewPage.Content, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                    htmlViewPage.SetText("ICON-USERNAME", char.ToUpper(userName.FirstOrDefault()).ToString(), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("LANGUAGE-OPTIONS", GetLanguageOptions(browser.Session.Locale), HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                    htmlViewPage.SetText("INTERNAL-HTML", InternalHtml, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                    htmlViewPage.SetText("CONTENT", htmlPartialViewPage.Content, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                    htmlViewPage.SetText("SYNCHRONIZE", "False", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("CONTENT-VIEW-NAME", "HomePage-Authenticated", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("PHABRICO-VERSION", VersionInfo.Version, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("PHABRICO-ROOTPATH", Http.Server.RootPath, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.Merge();
                    return htmlViewPage.Content;

                case HomePageStatus.AuthenticationError:
                    using (Storage.Database database = new Storage.Database(null))
                    {
                        authenticationFactor = database.GetAuthenticationFactor(browser);

                        htmlPartialViewPage.SetContent(browser, GetViewData("HomePage.AuthenticationDialog"));
                        htmlPartialViewPage.SetText("REDIRECT", Url, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("USERNAME", browser.Session.FormVariables[browser.Request.RawUrl]["username"], HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("PASSWORD", browser.Session.FormVariables[browser.Request.RawUrl]["password"], HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("STYLE.DISPLAY.ERROR", "flex", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("ERRORMESSAGE", Miscellaneous.Locale.TranslateText("Username or password are incorrect.", Browser.Session.Locale), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("AUTHENTICATION-FACTOR", authenticationFactor, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlPartialViewPage.Merge();

                        htmlViewPage.SetContent(browser, GetViewData("HomePage.Template"));
                        htmlViewPage.Customize(browser);
                        htmlViewPage.SetText("THEME", Theme, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("THEME-STYLE", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("LOCALE", Browser.Session.Locale, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("HEADERACTIONS", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("INTERNAL-HTML", InternalHtml, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlViewPage.SetText("CONTENT", htmlPartialViewPage.Content, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlViewPage.SetText("SYNCHRONIZE", "False", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("CONTENT-VIEW-NAME", "HomePage-AuthenticationError", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("PHABRICO-VERSION", VersionInfo.Version, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("PHABRICO-ROOTPATH", Http.Server.RootPath, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.Merge();
                    }
                    return htmlViewPage.Content;

                case HomePageStatus.EmptyDatabase:
                    Controllers.Configuration configurationController = new Controllers.Configuration();
                    configurationController.browser = browser;
                    configurationController.TokenId = browser.GetCookie("token");
                    userName = browser.Session.FormVariables["/auth/login"]["username"];
                    string password = browser.Session.FormVariables["/auth/login"]["password"];

                    // clean up auth FormVariables after 2nd authentication step
                    if (browser.Request.RawUrl.StartsWith("/auth/login") == false)
                    {
                        browser.Session.FormVariables.Remove("/auth/login");
                    }

                    configurationController.EncryptionKey = Encryption.GenerateEncryptionKey(userName, password);

                    HtmlViewPage configurationViewPage;
                    using (Storage.Database database = new Storage.Database(configurationController.EncryptionKey))
                    {
                        authenticationFactor = database.GetAuthenticationFactor(browser);
                        token = HttpServer.Session.GetToken(configurationController.TokenId);
                        database.PrivateEncryptionKey = token.PrivateEncryptionKey;

                        Phabricator.Data.Account newAccount = accountStorage.Get(database, token);

                        accountStorage.UpdateToken(database, newAccount.Token, newAccount.Token, newAccount.PublicXorCipher, newAccount.PrivateXorCipher);

                        configurationViewPage = new HtmlViewPage(HttpServer, browser, true, "configure", null);
                        configurationController.HttpGetLoadParameters(HttpServer, ref configurationViewPage, null, null);

                        int nbrMarkedFileObjects = database.GetAllMarkedFileIDs().Count();
                        Http.Server.SendNotificationError("/errorinaccessiblefiles/notification", nbrMarkedFileObjects.ToString());

                        htmlPartialViewPage.SetContent(browser, GetViewData("HomePage.TreeView.Template"));
                        htmlPartialViewPage.SetText("CONTENT", configurationViewPage.Content, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlPartialViewPage.SetText("AUTHENTICATION-FACTOR", authenticationFactor, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlPartialViewPage.SetText("ANY-INACCESSIBLE-FILES", nbrMarkedFileObjects > 0 ? "True" : "False", ArgumentOptions.NoHtmlEncoding);
                        htmlPartialViewPage.Merge();
                    }

                    htmlPartialHeaderViewPage.SetContent(browser, GetViewData("HomePage.Authenticated.HeaderActions"));
                    htmlPartialHeaderViewPage.SetText("AUTHENTICATION-FACTOR", authenticationFactor, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                    htmlPartialHeaderViewPage.Merge();

                    htmlViewPage.SetContent(browser, GetViewData("HomePage.Template"));
                    htmlViewPage.Customize(browser);
                    htmlViewPage.SetText("THEME", Theme, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("THEME-STYLE", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("LOCALE", Browser.Session.Locale, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("HEADERACTIONS", htmlPartialHeaderViewPage.Content, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                    htmlViewPage.SetText("ICON-USERNAME", char.ToUpper(userName.FirstOrDefault()).ToString(), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("LANGUAGE-OPTIONS", GetLanguageOptions(browser.Session.Locale), HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                    htmlViewPage.SetText("INTERNAL-HTML", InternalHtml, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                    htmlViewPage.SetText("CONTENT", htmlPartialViewPage.Content, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                    htmlViewPage.SetText("AUTOLOGOUTAFTERMINUTESOFINACTIVITY", "5", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("SYNCHRONIZE", "True", HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                    htmlViewPage.SetText("CONTENT-VIEW-NAME", "HomePage-EmptyDatabase", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("PHABRICO-VERSION", VersionInfo.Version, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("PHABRICO-ROOTPATH", Http.Server.RootPath, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.Merge();
                    return htmlViewPage.Content;

                case HomePageStatus.Initialized:
                    htmlPartialViewPage.SetContent(browser, GetViewData("HomePage.AuthenticationDialogCreateUser"));
                    htmlPartialViewPage.SetText("USERNAME", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlPartialViewPage.SetText("PASSWORD", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlPartialViewPage.SetText("CONDUITAPITOKEN", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlPartialViewPage.SetText("PHABRICATORURL", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlPartialViewPage.SetText("STYLE.DISPLAY.ERROR", "none", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlPartialViewPage.SetText("ERRORMESSAGE", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlPartialViewPage.SetText("LANGUAGE-OPTIONS", GetLanguageOptions(browser.Session.Locale), HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                    htmlPartialViewPage.Merge();

                    htmlViewPage.SetContent(browser, GetViewData("HomePage.Template"));
                    htmlViewPage.Customize(browser);
                    htmlViewPage.SetText("THEME", Theme, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("THEME-STYLE", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("LOCALE", Browser.Session.Locale, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("HEADERACTIONS", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("INTERNAL-HTML", InternalHtml, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                    htmlViewPage.SetText("CONTENT", htmlPartialViewPage.Content, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                    htmlViewPage.SetText("SYNCHRONIZE", "False", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("CONTENT-VIEW-NAME", "HomePage-Initialized", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("PHABRICO-VERSION", VersionInfo.Version, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("PHABRICO-ROOTPATH", Http.Server.RootPath, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.Merge();
                    return htmlViewPage.Content;

                case HomePageStatus.None:
                default:
                    using (Storage.Database database = new Storage.Database(null))
                    {
                        authenticationFactor = database.GetAuthenticationFactor(browser);

                        htmlPartialViewPage.SetContent(browser, GetViewData("HomePage.AuthenticationDialog"));
                        htmlPartialViewPage.SetText("REDIRECT", Url, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("USERNAME", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("PASSWORD", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("CONDUITAPITOKEN", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("PHABRICATORURL", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("STYLE.DISPLAY.ERROR", "none", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("ERRORMESSAGE", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("AUTHENTICATION-FACTOR", authenticationFactor, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlPartialViewPage.Merge();

                        htmlViewPage.SetContent(browser, GetViewData("HomePage.Template"));
                        htmlViewPage.Customize(browser);
                        htmlViewPage.SetText("THEME", Theme, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("THEME-STYLE", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("LOCALE", Browser.Session.Locale, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("HEADERACTIONS", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("INTERNAL-HTML", InternalHtml, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlViewPage.SetText("CONTENT", htmlPartialViewPage.Content, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlViewPage.SetText("SYNCHRONIZE", "False", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("CONTENT-VIEW-NAME", "HomePage-AuthenticationDialog", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("PHABRICO-VERSION", VersionInfo.Version, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("PHABRICO-ROOTPATH", Http.Server.RootPath, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.Merge();
                    }
                    return htmlViewPage.Content;
            }
        }

        /// <summary>
        /// Shows the default homepage or redirects to a given Phabrico URL
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="redirectUrl">If empty, the default homepage will be shown. Otherwise a HTTP redirection is executed</param>
        public void Send(Browser browser, string redirectUrl)
        {
            string sessionToken = browser.GetCookie("token");
            if (sessionToken != null && browser.HttpServer.Session.TokenValid(sessionToken) && Status != HomePageStatus.EmptyDatabase)
            {
                Status = HomePageStatus.Authenticated;

                // update review states of translations
                SessionManager.Token token = SessionManager.GetToken(browser);
                string encryptionKey = token.EncryptionKey;
                using (Storage.Database database = new Storage.Database(encryptionKey))
                {
                    database.PrivateEncryptionKey = token.PrivateEncryptionKey;
                    Storage.Content.SynchronizeReviewStatesWithMasterObjects(database);
                }
            }

            string html;
            if (Status == HomePageStatus.Authenticated && string.IsNullOrEmpty(redirectUrl) == false)
            {
                HtmlViewPage htmlViewPage = new HtmlViewPage(browser);
                htmlViewPage.SetContent(browser, GetViewData("HomePage.Template"));
                htmlViewPage.SetText("THEME", Theme, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                htmlViewPage.SetText("THEME-STYLE", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                htmlViewPage.SetText("LOCALE", browser.Session.Locale, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                htmlViewPage.SetText("HEADERACTIONS", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                htmlViewPage.SetText("INTERNAL-HTML", InternalHtml, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                htmlViewPage.SetText("CONTENT", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                htmlViewPage.SetText("CONTENT-VIEW-NAME", "HomePage-Template", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                htmlViewPage.SetText("PHABRICO-VERSION", VersionInfo.Version, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                htmlViewPage.SetText("PHABRICO-ROOTPATH", Http.Server.RootPath, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                htmlViewPage.Merge();
                html = htmlViewPage.Content;
            }
            else
            {
                if (Status == HomePageStatus.Authenticated &&
                    HttpServer.Customization.HideConfig &&
                    HttpServer.Customization.HideFiles &&
                    HttpServer.Customization.HideManiphest &&
                    HttpServer.Customization.HideOfflineChanges &&
                    HttpServer.Customization.HideProjects &&
                    HttpServer.Customization.HideUsers &&
                    HttpServer.Customization.HidePhriction == false &&
                    Http.Server.Plugins.All(plugin => plugin.IsVisibleInNavigator(browser) == false
                                                   || (browser.HttpServer.Customization.HidePlugins.ContainsKey(plugin.GetType().Name)
                                                       && browser.HttpServer.Customization.HidePlugins[plugin.GetType().Name] == true
                                                       )
                                           )
                   )
                {
                    HttpRedirect httpRedirect = new HttpRedirect(HttpServer, Browser, Http.Server.RootPath + "w", true);
                    httpRedirect.Send(Browser);
                    return;
                }
                else
                if (Status == HomePageStatus.Authenticated &&
                    HttpServer.Customization.HideConfig &&
                    HttpServer.Customization.HideFiles &&
                    HttpServer.Customization.HidePhriction &&
                    HttpServer.Customization.HideOfflineChanges &&
                    HttpServer.Customization.HideProjects &&
                    HttpServer.Customization.HideUsers &&
                    HttpServer.Customization.HideManiphest == false &&
                    Http.Server.Plugins.All(plugin => plugin.IsVisibleInNavigator(browser) == false
                                                   || (browser.HttpServer.Customization.HidePlugins.ContainsKey(plugin.GetType().Name)
                                                       && browser.HttpServer.Customization.HidePlugins[plugin.GetType().Name] == true
                                                       )
                                           )
                   )
                {
                    HttpRedirect httpRedirect = new HttpRedirect(HttpServer, Browser, Http.Server.RootPath + "maniphest", true);
                    httpRedirect.Send(Browser);
                    return;
                }
                else
                {
                    html = GetPageContent(browser);
                }
            }

            // send data
            byte[] data = UTF8Encoding.UTF8.GetBytes(html);
            base.Send(browser, data);
        }
    }
}