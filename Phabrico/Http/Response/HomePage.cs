using Phabrico.Miscellaneous;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
                    browser.Session.Locale = browser.Language;  // restore locale

                    string encryptionKey = token.EncryptionKey;

                    using (Storage.Database database = new Storage.Database(encryptionKey))
                    {
                        authenticationFactor = database.GetAuthenticationFactor();

                        database.ClearOldSessionVariables(browser);

                        accountData = accountStorage.WhoAmI(database);

                        // get favorites
                        string htmlFavoriteObjects = "";
                        long previousFavoriteIndex = 0;
                        Storage.Phriction phrictionStorage = new Storage.Phriction();
                        Storage.FavoriteObject favoriteObjectStorage = new Storage.FavoriteObject();
                        List<Phabricator.Data.Phriction> favoritePhrictionDocuments = new List<Phabricator.Data.Phriction>();
                        if (HttpServer.Customization.HidePhrictionFavorites == false)
                        {
                            favoritePhrictionDocuments = phrictionStorage.GetFavorites(database, accountData.UserName).ToList();
                        }

                        foreach (Phabricator.Data.Phriction favoritePhrictionDocument in favoritePhrictionDocuments)
                        {
                            // check if we have a splitter
                            bool previousItemIsSplitter = false;
                            if (previousFavoriteIndex + 1 < favoritePhrictionDocument.DisplayOrderInFavorites)
                            {
                                previousFavoriteIndex = favoritePhrictionDocument.DisplayOrderInFavorites;

                                // insert splitter
                                htmlFavoriteObjects += @"<div class='favorite-item splitter'>
                                                            <span class='combine-favorite-items fa fa-arrows-v'></span>
                                                            <hr />
                                                         </div>";

                                previousItemIsSplitter = true;
                            }

                            string[] crumbs = favoritePhrictionDocument.Path.TrimEnd('/').Split('/');
                            string crumbDescriptions = "";
                            string currentPath = "";
                            foreach (string crumb in crumbs.Take(crumbs.Length - 1))
                            {
                                currentPath += crumb + "/";
                                Phabricator.Data.Phriction parentDocument = phrictionStorage.Get(database, currentPath);
                                if (parentDocument == null)
                                {
                                    string[] camelCasedCrumbs = crumb.Split(' ', '_')
                                                                     .Select(word => word.Length > 1
                                                                                   ? char.ToUpper(word[0]) + word.Substring(1)
                                                                                   : word.ToUpper()
                                                                            )
                                                                     .Select(word => word.Replace('_', ' '))
                                                                     .ToArray();

                                    crumbDescriptions += " > " + string.Join(" ", camelCasedCrumbs);
                                }
                                else
                                {
                                    crumbDescriptions += " > " + parentDocument.Name;
                                }
                            }

                            if (string.IsNullOrEmpty(crumbDescriptions) == false)
                            {
                                crumbDescriptions = crumbDescriptions.Substring(" > ".Length);
                            }

                            // insert favorite-item
                            string favoriteItemDescription = crumbDescriptions;
                            if (string.IsNullOrEmpty(favoriteItemDescription) == false)
                            {
                                favoriteItemDescription += " > ";
                            }
                            favoriteItemDescription += favoritePhrictionDocument.Name;

                            // if Phriction root is set as favorite, don't add a 2nd slash character to a-href tag
                            if (favoritePhrictionDocument.Path.Equals("/"))
                            {
                                favoritePhrictionDocument.Path = "";
                            }

                            Phabricator.Data.FavoriteObject favoriteObject = favoriteObjectStorage.Get(database, accountData.UserName, favoritePhrictionDocument.Token);

                            htmlFavoriteObjects += string.Format(@"
                                                            <div class='favorite-item{0}' data-token='{1}'>
                                                                <span class='favorite-items-cutter'>
                                                                    <span class='no-cut-favorite-item fa fa-circle'></span>
                                                                    <span class='cut-favorite-item fa fa-cut'></span>
                                                                </span>
                                                                <span class='link'>
                                                                    <a href=""w/{2}"">{3}</a>
                                                                </span>
                                                            </div>",
                                                        previousItemIsSplitter ? " first-of-fav-group" : "",
                                                        favoritePhrictionDocument.Token,
                                                        favoritePhrictionDocument.Path,
                                                        favoriteItemDescription
                                                    );

                            previousFavoriteIndex = favoritePhrictionDocument.DisplayOrderInFavorites;
                        }

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
                        htmlPartialViewPage.SetText("HAS-FAVORITES", htmlFavoriteObjects.Any() ? "True" : "False", HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlPartialViewPage.SetText("FAVORITES", htmlFavoriteObjects, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlPartialViewPage.Customize(browser);
                        htmlPartialViewPage.Merge();

                        htmlViewPage.SetContent(browser, GetViewData("HomePage.TreeView.Template"));
                        htmlViewPage.SetText("AUTOLOGOUTAFTERMINUTESOFINACTIVITY", database.GetAccountConfiguration()?.AutoLogOutAfterMinutesOfInactivity.ToString(), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("CONTENT", htmlPartialViewPage.Content, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlViewPage.SetText("AUTHENTICATION-FACTOR", authenticationFactor, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlViewPage.Customize(browser);

                        foreach (Plugin.PluginBase plugin in Http.Server.Plugins)
                        {
                            Plugin.PluginTypeAttribute pluginType = plugin.GetType().GetCustomAttributes(typeof(Plugin.PluginTypeAttribute), true).FirstOrDefault() as Plugin.PluginTypeAttribute;
                            if (pluginType != null && pluginType.Usage != Plugin.PluginTypeAttribute.UsageType.Navigator) continue;

                            if (plugin.IsVisible(browser))
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
                        }

                        htmlViewPage.Merge();
                        htmlPartialViewPage = htmlViewPage;

                        accountData = accountStorage.WhoAmI(database);
                        userName = accountData.UserName;

                        string languageCookie = browser.GetCookie("language");
                        if (string.IsNullOrEmpty(languageCookie))
                        {
                            browser.Session.Locale = browser.Language;
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
                        authenticationFactor = database.GetAuthenticationFactor();

                        htmlPartialViewPage.SetContent(browser, GetViewData("HomePage.AuthenticationDialog"));
                        htmlPartialViewPage.SetText("REDIRECT", Url, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("USERNAME", browser.Session.FormVariables["username"], HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlPartialViewPage.SetText("PASSWORD", browser.Session.FormVariables["password"], HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
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
                    configurationController.TokenId = browser.GetCookie("token");
                    userName = browser.Session.FormVariables["username"];
                    string password = browser.Session.FormVariables["password"];

                    configurationController.EncryptionKey = Encryption.GenerateEncryptionKey(userName, password);

                    HtmlViewPage configurationViewPage;
                    using (Storage.Database database = new Storage.Database(configurationController.EncryptionKey))
                    {
                        authenticationFactor = database.GetAuthenticationFactor();
                        token = HttpServer.Session.GetToken(configurationController.TokenId);
                        database.PrivateEncryptionKey = token.PrivateEncryptionKey;

                        Phabricator.Data.Account newAccount = accountStorage.Get(database, token);

                        accountStorage.UpdateToken(database, newAccount.Token, newAccount.Token, newAccount.PublicXorCipher, newAccount.PrivateXorCipher);

                        configurationViewPage = new HtmlViewPage(HttpServer, browser, true, "configure", null);
                        configurationController.HttpGetLoadParameters(HttpServer, browser, ref configurationViewPage, null, null);
                    }

                    htmlPartialViewPage.SetContent(browser, GetViewData("HomePage.TreeView.Template"));
                    htmlPartialViewPage.SetText("CONTENT", configurationViewPage.Content, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                    htmlPartialViewPage.SetText("AUTHENTICATION-FACTOR", authenticationFactor, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                    htmlPartialViewPage.Merge();

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
                        authenticationFactor = database.GetAuthenticationFactor();

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
                    Http.Server.Plugins.All(plugin => plugin.IsVisible(browser) == false)
                   )
                {
                    HttpRedirect httpRedirect = new HttpRedirect(HttpServer, Browser, Http.Server.RootPath + "w");
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
                    Http.Server.Plugins.All(plugin => plugin.IsVisible(browser) == false)
                   )
                {
                    HttpRedirect httpRedirect = new HttpRedirect(HttpServer, Browser, Http.Server.RootPath + "maniphest");
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