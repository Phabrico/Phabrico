using Phabrico.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Phabrico.Http.Response
{
    /// <summary>
    /// Represents the HTTP response to form a HTML page built from a View page (see View solution folder)
    /// </summary>
    public class HtmlViewPage : HtmlPage
    {
        private List<HtmlPartialViewPage> _partialSubViews = new List<HtmlPartialViewPage>();

        /// <summary>
        /// How the content of a HtmlViewPage parameter should be encoded
        /// </summary>
        [Flags]
        public enum ArgumentOptions
        {
            /// <summary>
            /// Parameter value will be HTML encoded.
            /// For example, if your parameter value contains a '&amp;' character, it will be transformed to '&amp;amp;'
            /// In case your parameter value is an empty string, it will be transformed to '(None)'
            /// </summary>
            Default = 0,

            /// <summary>
            /// In case your parameter value is an empty string, it will not be transformed to '(None)'
            /// </summary>
            AllowEmptyParameterValue = 1,

            /// <summary>
            /// Parameter value may contain HTML and should not be (re-)encoded in HTML
            /// </summary>
            NoHtmlEncoding = 2,

            /// <summary>
            /// Parameter value should be formatted in Javascript, i.e. includes AllowEmptyParameterValue and
            /// NoHtmlEncoding and special characters like a backslash and double quotes are escaped
            /// </summary>
            JavascriptEncoding = 7
        }

        /// <summary>
        /// Layout options
        /// </summary>
        [Flags]
        public enum ContentOptions
        {
            /// <summary>
            /// There will be no extra content in the HTML generated
            /// </summary>
            NoFormatting = 0,

            /// <summary>
            /// The global menu navigator at the left and the header on top are merged into this HtmlViewPage
            /// </summary>
            Default = 1,

            /// <summary>
            /// The global menu navigator will not be merged into this HtmlViewPage
            /// </summary>
            HideGlobalTreeView = 2,

            /// <summary>
            /// A custom menu navigator instead of the global menu navigator will be merged into this HtmlViewPage
            /// </summary>
            UseLocalTreeView = 4,

            /// <summary>
            /// The header on top will not be merged into this HtmlViewPage
            /// </summary>
            HideHeader = 8,

            /// <summary>
            /// The content is shown in an IFrame: header and global treeview are not shown and no Phabrico CSS is put in the view
            /// </summary>
            IFrame = 2 + 8 + 16
        }

        /// <summary>
        /// Initializes a new instance of HtmlViewPage
        /// </summary>
        internal HtmlViewPage(Browser browser)
            : base(browser.HttpServer, browser, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of HtmlViewPage
        /// </summary>
        internal HtmlViewPage(Http.Server httpServer, Browser browser, bool doTranslateContent)
            : base(httpServer, browser, null)
        {
            DoTranslateContent = doTranslateContent;
        }

        /// <summary>
        /// Initializes a new instance of HtmlViewPage
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="doTranslateContent"></param>
        /// <param name="url"></param>
        /// <param name="arguments"></param>
        public HtmlViewPage(Http.Server httpServer, Browser browser, bool doTranslateContent, string url, string[] arguments = null) : base(httpServer, browser, url)
        {
            DoTranslateContent = doTranslateContent;

            Content = GetViewData(url);
            Customize(browser);
        }


        /// <summary>
        /// Completes the HtmlViewPage with some configured parameter values
        /// </summary>
        /// <param name="defaultParameterValues">Dictionary of parameters (key=name, value=value)</param>
        public void AddDefaultParameterValues(Dictionary<string, string> defaultParameterValues)
        {
            foreach (KeyValuePair<string, string> parameter in defaultParameterValues)
            {
                string regParameterName = "";
                foreach (char c in parameter.Key) regParameterName += "[" + c + "]";

                // search for input tags
                Regex regValue = new Regex("value=[\"']([^\"']*)[\"']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                Regex regInput = new Regex("<input.*name=[\"']" + regParameterName + "[\"'][^>]*", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                foreach (Match inputTag in regInput.Matches(Content).OfType<Match>().Reverse().ToList())
                {
                    Match valueAttribute = regValue.Match(inputTag.Value);
                    if (valueAttribute.Success)
                    {
                        Content = Content.Substring(0, valueAttribute.Groups[1].Index) + parameter.Value + Content.Substring(valueAttribute.Groups[1].Index + valueAttribute.Groups[1].Length);
                    }
                    else
                    {
                        Content = Content.Substring(0, inputTag.Index + inputTag.Length) + " value='" + parameter.Value + "'" + Content.Substring(inputTag.Index + inputTag.Length);
                    }
                }

                // search for select tags
                Regex regOption = new Regex("<option[^>]*value=[\"']([^\"']*)[\"'][^>]*>[^<]*</option>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                Regex regOptionSelected = new Regex("<option.+?(selected=[\"']selected[\"'])?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                Regex regSelect = new Regex("<select.*name=[\"']" + regParameterName + "[\"'][^>]*>([^<]*<option[^>]*>[^<]*</option>)+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                foreach (Match selectTag in regSelect.Matches(Content).OfType<Match>().Reverse().ToList())
                {
                    foreach (Match optionTag in regOption.Matches(selectTag.Value).OfType<Match>().Reverse().ToList())
                    {
                        Match optionSelectedAttribute = regOptionSelected.Match(optionTag.Value);
                        if (optionTag.Groups[1].Value.Equals(parameter.Value))
                        {
                            // default selection
                            if (optionSelectedAttribute.Groups[1].Success == false)
                            {
                                // not selected: make it selected
                                Content = Content.Substring(0, selectTag.Index + optionTag.Index + optionSelectedAttribute.Length) + " selected='selected' " + Content.Substring(selectTag.Index + optionTag.Index + optionSelectedAttribute.Length);
                            }
                        }
                        else
                        {
                            // non-default selection
                            if (optionSelectedAttribute.Groups[1].Success)
                            {
                                // selected: make it non-selected
                                Content = Content.Substring(0, selectTag.Index + optionTag.Index + optionSelectedAttribute.Groups[1].Index) 
                                        + Content.Substring(selectTag.Index + optionTag.Index + optionSelectedAttribute.Groups[1].Index + optionSelectedAttribute.Groups[1].Length);
                            }
                        }
                    }
                }
            }
        }
        
        public void Customize(Browser browser)
        {
            string customApplicationLogo = HttpServer.Customization.CustomApplicationLogoBase64;
            string customApplicationName = HttpServer.Customization.ApplicationName;

            bool useDefaultApplicationName = string.IsNullOrWhiteSpace(customApplicationLogo) && customApplicationName.Equals("Phabrico");

            SetText("DEFAULT-APPLICATION-NAME", useDefaultApplicationName ? "True" : "False", ArgumentOptions.NoHtmlEncoding | ArgumentOptions.AllowEmptyParameterValue);
            SetText("GLOBAL-CSS", HttpServer.Customization.ApplicationCSS, ArgumentOptions.NoHtmlEncoding | ArgumentOptions.AllowEmptyParameterValue);

            // do we need to rename the application ?
            if (useDefaultApplicationName == false)
            {
                // custom name and/or logo using instead of "Phabrico"
                // check for missing css styles
                if (HttpServer.Customization.ApplicationLogoStyle.ContainsKey("max-height") == false)
                {
                    HttpServer.Customization.ApplicationLogoStyle["max-height"] = "36px";
                }

                if (HttpServer.Customization.ApplicationLogoStyle.ContainsKey("margin-right") == false)
                {
                    HttpServer.Customization.ApplicationLogoStyle["margin-right"] = "8px";
                }

                // set or overwrite line-height of custom name element (to limit the height of the header bar on top)
                HttpServer.Customization.ApplicationNameStyle["line-height"] = "44px";

                // correct css strings
                string customApplicationLogoStyle = string.Join(";", HttpServer.Customization.ApplicationLogoStyle.Where(css => css.Key != "").Select(css => css.Key + ":" + css.Value));
                string customApplicationNameStyle = string.Join(";", HttpServer.Customization.ApplicationNameStyle.Where(css => css.Key != "").Select(css => css.Key + ":" + css.Value));

                SetText("CUSTOM-APPLICATION-LOGO", customApplicationLogo, ArgumentOptions.NoHtmlEncoding | ArgumentOptions.AllowEmptyParameterValue);
                SetText("CUSTOM-APPLICATION-LOGO-STYLE", customApplicationLogoStyle, ArgumentOptions.NoHtmlEncoding | ArgumentOptions.AllowEmptyParameterValue);
                SetText("CUSTOM-APPLICATION-NAME-STYLE", customApplicationNameStyle, ArgumentOptions.NoHtmlEncoding | ArgumentOptions.AllowEmptyParameterValue);
            }

            // set the name of the Phabrico application
            SetText("CUSTOM-APPLICATION-NAME", customApplicationName, ArgumentOptions.AllowEmptyParameterValue);

            // set custom favicon (if any)
            if (HttpServer.Customization.FavIcon == null)
            {
                SetText("CUSTOM-FAVICON", "", ArgumentOptions.AllowEmptyParameterValue);
            }
            else
            {
                SetText("CUSTOM-FAVICON", HttpServer.Customization.CustomFavIconBase64, ArgumentOptions.NoHtmlEncoding | ArgumentOptions.AllowEmptyParameterValue);
            }

            if (HttpServer.IsHttpModule)
            {
                SetText("IIS-MODULE", "True", ArgumentOptions.AllowEmptyParameterValue);

                // disable periodical check to see if we have the latest Phabrico version installed
                SetText("CHECK-FOR-LATEST-VERSION", "False", ArgumentOptions.AllowEmptyParameterValue);

                // if public, ignore AuthenticationFactor from database (set to public)
                if (HttpServer.Customization.AuthenticationFactor == ApplicationCustomization.ApplicationAuthenticationFactor.Public)
                {
                    browser.Token.AuthenticationFactor = AuthenticationFactor.Public;
                    SetText("AUTHENTICATION-FACTOR", AuthenticationFactor.Public, ArgumentOptions.AllowEmptyParameterValue);
                }
            }
            else
            {
                SetText("CHECK-FOR-LATEST-VERSION", "True", ArgumentOptions.AllowEmptyParameterValue);
                SetText("IIS-MODULE", "False", ArgumentOptions.AllowEmptyParameterValue);
            }

            // configure access management
            bool hideChangeLanguage = string.IsNullOrWhiteSpace(HttpServer.Customization.Language) == false;
            bool hideChangePassword = browser.Token.AuthenticationFactor != AuthenticationFactor.Knowledge && browser.Token.AuthenticationFactor != AuthenticationFactor.Experience;
            bool hideConfig = HttpServer.Customization.HideConfig || browser.Token.AuthenticationFactor == AuthenticationFactor.Experience || browser.Token.AuthenticationFactor == AuthenticationFactor.Public;
            bool hideFiles = HttpServer.Customization.HideFiles;
            bool hideManiphest = HttpServer.Customization.HideManiphest;
            bool hideNavigatorTooltips = HttpServer.Customization.HideNavigatorTooltips;
            bool hideOfflineChanges = HttpServer.Customization.HideOfflineChanges;
            bool hidePhame = HttpServer.Customization.HidePhame;
            bool hidePhriction = HttpServer.Customization.HidePhriction;
            bool hidePhrictionChanges = HttpServer.Customization.HidePhrictionChanges;
            bool hidePhrictionFavorites = HttpServer.Customization.HidePhrictionFavorites;
            bool hideProjects = HttpServer.Customization.HideProjects || browser.Token.AuthenticationFactor == AuthenticationFactor.Experience || browser.Token.AuthenticationFactor == AuthenticationFactor.Public;
            bool hideSearch = HttpServer.Customization.HideSearch;
            bool hideUsers = HttpServer.Customization.HideUsers || browser.Token.AuthenticationFactor == AuthenticationFactor.Experience || browser.Token.AuthenticationFactor == AuthenticationFactor.Public;
            bool isReadOnly = HttpServer.Customization.IsReadonly;
            bool masterDataIsAccessible = HttpServer.Customization.MasterDataIsAccessible && browser.Token.AuthenticationFactor != AuthenticationFactor.Experience && browser.Token.AuthenticationFactor != AuthenticationFactor.Public;

            SetText("ACCESS-HIDE-CHANGE-LANGUAGE", hideChangeLanguage.ToString(), ArgumentOptions.AllowEmptyParameterValue);
            SetText("ACCESS-HIDE-CHANGE-PASSWORD", hideChangePassword.ToString(), ArgumentOptions.AllowEmptyParameterValue);
            SetText("ACCESS-HIDE-CONFIG", hideConfig.ToString(), ArgumentOptions.AllowEmptyParameterValue);
            SetText("ACCESS-HIDE-FILES", hideFiles.ToString(), ArgumentOptions.AllowEmptyParameterValue);
            SetText("ACCESS-HIDE-MANIPHEST", hideManiphest.ToString(), ArgumentOptions.AllowEmptyParameterValue);
            SetText("ACCESS-HIDE-NAVIGATOR-TOOLTIPS", hideNavigatorTooltips.ToString(), ArgumentOptions.AllowEmptyParameterValue);
            SetText("ACCESS-HIDE-OFFLINE-CHANGES", hideOfflineChanges.ToString(), ArgumentOptions.AllowEmptyParameterValue);
            SetText("ACCESS-HIDE-PHAME", hidePhame.ToString(), ArgumentOptions.AllowEmptyParameterValue);
            SetText("ACCESS-HIDE-PHRICTION", hidePhriction.ToString(), ArgumentOptions.AllowEmptyParameterValue);
            SetText("ACCESS-HIDE-PHRICTION-CHANGES", hidePhrictionChanges.ToString(), ArgumentOptions.AllowEmptyParameterValue);
            SetText("ACCESS-HIDE-PHRICTION-FAVORITES", hidePhrictionFavorites.ToString(), ArgumentOptions.AllowEmptyParameterValue);
            SetText("ACCESS-HIDE-PROJECTS", hideProjects.ToString(), ArgumentOptions.AllowEmptyParameterValue);
            SetText("ACCESS-HIDE-SEARCH", hideSearch.ToString(), ArgumentOptions.AllowEmptyParameterValue);
            SetText("ACCESS-HIDE-USERS", hideUsers.ToString(), ArgumentOptions.AllowEmptyParameterValue);
            SetText("ACCESS-READONLY", isReadOnly.ToString(), ArgumentOptions.AllowEmptyParameterValue);
            SetText("ACCESS-MASTER-DATA", masterDataIsAccessible.ToString(), ArgumentOptions.AllowEmptyParameterValue);

            bool hideGeneralTabInConfiguration = HttpServer.IsHttpModule && HttpServer.Customization.Theme != ApplicationCustomization.ApplicationTheme.Auto;
            SetText("ACCESS-HIDE-CONFIG-GENERAL", hideGeneralTabInConfiguration.ToString(), ArgumentOptions.AllowEmptyParameterValue);

            // show/hide user menu next to search field in case there are no visible menu items
            bool hideUserMenu = hideChangeLanguage
                             && ((HttpServer.IsHttpModule == false && browser.Token.AuthenticationFactor == AuthenticationFactor.Ownership)
                             ||  (HttpServer.IsHttpModule == true && browser.Token.AuthenticationFactor == AuthenticationFactor.Public)
                                );
            SetText("HIDE-USER-MENU", hideUserMenu.ToString(), ArgumentOptions.AllowEmptyParameterValue);

            // override visibility action menu in Phriction
            // if not needed to be hidden, take original visibility functionality
            if (HttpServer.Customization.HidePhrictionActionMenu)
            {
                SetText("SHOW-SIDE-WINDOW", "No", ArgumentOptions.AllowEmptyParameterValue);
            }

            // override theme if needed
            if (HttpServer.Customization.Theme != ApplicationCustomization.ApplicationTheme.Auto)
            {
                string theme = typeof(ApplicationCustomization.ApplicationTheme).GetMember(HttpServer.Customization.Theme.ToString())
                                                                                .FirstOrDefault(member => member.MemberType == MemberTypes.Field)
                                                                                .GetCustomAttributes(typeof(DescriptionAttribute), false)
                                                                                .Cast<DescriptionAttribute>()
                                                                                .FirstOrDefault()
                                                                                .Description;

                SetText("THEME", theme, ArgumentOptions.AllowEmptyParameterValue);
                SetText("THEME-STYLE", "", ArgumentOptions.AllowEmptyParameterValue);
                SetText("ACCESS-HIDE-THEME-CONFIG", "True", ArgumentOptions.AllowEmptyParameterValue);
            }
            else
            {
                SetText("ACCESS-HIDE-THEME-CONFIG", "False", ArgumentOptions.AllowEmptyParameterValue);
            }

            // override language if needed
            if (string.IsNullOrEmpty(HttpServer.Customization.Language) == false)
            {
                SetText("LOCALE", HttpServer.Customization.Language, ArgumentOptions.AllowEmptyParameterValue);
                browser.Language = HttpServer.Customization.Language;
                browser.Session.Locale = HttpServer.Customization.Language;
            }

            // verify if only Maniphest should be visible
            if (HttpServer.Customization.HideConfig &&
                HttpServer.Customization.HideFiles &&
                HttpServer.Customization.HideManiphest &&
                HttpServer.Customization.HideOfflineChanges &&
                HttpServer.Customization.HideProjects &&
                HttpServer.Customization.HideUsers &&
                HttpServer.Customization.HidePhriction == false &&
                Http.Server.Plugins.All(plugin => plugin.IsVisible(browser) == false)
               )
            {
                SetText("ONLY-MANIPHEST", "True", ArgumentOptions.AllowEmptyParameterValue);
            }
            else
            {
                SetText("ONLY-MANIPHEST", "False", ArgumentOptions.AllowEmptyParameterValue);
            }
        }

        /// <summary>
        /// Returns a given partial view
        /// </summary>
        /// <param name="templateName">Name of the partial view</param>
        /// <returns>Partial View object</returns>
        public HtmlPartialViewPage GetPartialView(string templateName)
        {
            string regName = new string(templateName.SelectMany(c => "[" + c + "]").ToArray());

            Regex regPartialView = new Regex(@"\@{" + regName + @"      # opening {
                                                 (                      # begin of content
                                                     (?>                # now match...
                                                        [^{}]+          # any characters except braces
                                                     |                  # or
                                                        \{  (?<DEPTH>)  # a {, increasing the depth counter
                                                     |                  # or
                                                        \}  (?<-DEPTH>) # a }, decreasing the depth counter
                                                     )*                 # any number of times
                                                     (?(DEPTH)(?!))     # until the depth counter is zero again
                                                 )                      # end of content
                                               \}@                      # then match the closing }",
                                        RegexOptions.IgnorePatternWhitespace);
            Match partialView = regPartialView.Match(Content);
            if (partialView.Success)
            {
                HtmlPartialViewPage newHtmlPartialViewPage = new HtmlPartialViewPage(Browser, templateName, partialView.Value, partialView.Groups[1].Value, partialView.Index);
                _partialSubViews.Add(newHtmlPartialViewPage);
                return newHtmlPartialViewPage;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the complete HTML for this HtmlViewPage
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="contentOptions"></param>
        /// <returns></returns>
        public string GetFullContent(Browser browser, ContentOptions contentOptions)
        {
            string themeStyle = "";
            string userName = "";
            string authenticationFactor = "";
            string encryptionKey = null;
            int autoLogOutAfterMinutesOfInactivity = 60;

            string html = Content;
            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token != null)
            {
                encryptionKey = token?.EncryptionKey;

                using (Storage.Database database = new Storage.Database(encryptionKey))
                {
                    // set private encryption key
                    database.PrivateEncryptionKey = token.PrivateEncryptionKey;

                    Storage.Account accountStorage = new Storage.Account();

                    Phabricator.Data.Account accountData = accountStorage.WhoAmI(database, browser);

                    switch (accountData.Parameters.DarkenBrightImages)
                    {
                        case Phabricator.Data.Account.DarkenImageStyle.Extreme:
                            themeStyle = "extreme";
                            break;

                        case Phabricator.Data.Account.DarkenImageStyle.Moderate:
                            themeStyle = "moderate";
                            break;

                        case Phabricator.Data.Account.DarkenImageStyle.Disabled:
                        default:
                            themeStyle = "";
                            break;
                    }

                    userName = accountData.UserName;
                    authenticationFactor = database.GetAuthenticationFactor(browser);
                    autoLogOutAfterMinutesOfInactivity = database.GetAccountConfiguration()?.AutoLogOutAfterMinutesOfInactivity ?? 60;
                }
            }

            HtmlViewPage htmlViewPage = new HtmlViewPage(browser);
            HtmlViewPage htmlPartialViewPage = new HtmlViewPage(browser);
            HtmlViewPage htmlPartialHeaderViewPage = new HtmlViewPage(browser);

            if (contentOptions.HasFlag(ContentOptions.UseLocalTreeView))
            {
                if (contentOptions.HasFlag(ContentOptions.HideHeader))
                {
                    htmlViewPage.SetContent(browser, GetViewData("HomePage.NoHeaderLocalTreeView.Template"));
                    htmlViewPage.SetText("THEME", Theme, ArgumentOptions.NoHtmlEncoding);
                    htmlViewPage.SetText("THEME-STYLE", themeStyle, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("LOCALE", Browser.Session.Locale, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("CONTENT", html, ArgumentOptions.NoHtmlEncoding);
                    htmlViewPage.SetText("PHABRICO-VERSION", VersionInfo.Version, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("PHABRICO-ROOTPATH", Http.Server.RootPath, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.Merge();
                    html = htmlViewPage.Content;
                }
                else
                {
                    // IMPOSSIBLE: should not happen
                    html = "You should not see this";
                }
            }
            else
            {
                if (contentOptions.HasFlag(ContentOptions.IFrame))
                {
                    htmlViewPage.SetContent(browser, GetViewData("HomePage.IFrameContent"));
                    htmlViewPage.SetText("THEME", Theme, ArgumentOptions.NoHtmlEncoding);
                    htmlViewPage.SetText("THEME-STYLE", themeStyle, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("LOCALE", Browser.Session.Locale, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("CONTENT", html, ArgumentOptions.NoHtmlEncoding);
                    htmlViewPage.Merge();
                    html = htmlViewPage.Content;
                }
                else
                if (contentOptions.HasFlag(ContentOptions.HideGlobalTreeView))
                {
                    if (contentOptions.HasFlag(ContentOptions.HideHeader))
                    {
                        htmlViewPage.SetContent(browser, GetViewData("HomePage.NoHeaderTreeView.Template"));
                        htmlViewPage.SetText("THEME", Theme, ArgumentOptions.NoHtmlEncoding);
                        htmlViewPage.SetText("THEME-STYLE", themeStyle, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("LOCALE", Browser.Session.Locale, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("CONTENT", html, ArgumentOptions.NoHtmlEncoding);
                        htmlViewPage.SetText("PHABRICO-VERSION", VersionInfo.Version, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("PHABRICO-ROOTPATH", Http.Server.RootPath, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.Merge();
                        html = htmlViewPage.Content;
                    }
                    else
                    {
                        htmlPartialViewPage.SetContent(browser, GetViewData("HomePage.NoTreeView.Template"));
                        htmlPartialViewPage.SetText("AUTOLOGOUTAFTERMINUTESOFINACTIVITY", autoLogOutAfterMinutesOfInactivity.ToString(), ArgumentOptions.NoHtmlEncoding);
                        htmlPartialViewPage.SetText("CONTENT", html, ArgumentOptions.NoHtmlEncoding);
                        htmlPartialViewPage.SetText("AUTHENTICATION-FACTOR", authenticationFactor, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlPartialViewPage.Merge();

                        htmlPartialHeaderViewPage.SetContent(browser, GetViewData("HomePage.Authenticated.HeaderActions"));
                        htmlPartialHeaderViewPage.SetText("AUTHENTICATION-FACTOR", authenticationFactor, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlPartialHeaderViewPage.Merge();

                        htmlViewPage.SetContent(browser, GetViewData("HomePage.Template"));
                        htmlViewPage.SetText("THEME", Theme, ArgumentOptions.NoHtmlEncoding);
                        htmlViewPage.SetText("THEME-STYLE", themeStyle, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("LOCALE", Browser.Session.Locale, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("SYNCHRONIZE", "", ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("HEADERACTIONS", htmlPartialHeaderViewPage.Content, ArgumentOptions.NoHtmlEncoding);
                        htmlViewPage.SetText("ICON-USERNAME", char.ToUpper(userName.FirstOrDefault()).ToString(), ArgumentOptions.NoHtmlEncoding);
                        htmlViewPage.SetText("LANGUAGE-OPTIONS", GetLanguageOptions(browser.Session.Locale), HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        htmlViewPage.SetText("CONTENT", htmlPartialViewPage.Content, ArgumentOptions.NoHtmlEncoding);
                        htmlViewPage.SetText("PHABRICO-VERSION", VersionInfo.Version, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.SetText("PHABRICO-ROOTPATH", Http.Server.RootPath, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        htmlViewPage.Merge();
                        html = htmlViewPage.Content;
                    }
                }
                else
                if (contentOptions.HasFlag(ContentOptions.Default))
                {
                    htmlPartialViewPage.SetContent(browser, GetViewData("HomePage.TreeView.Template"));
                    htmlPartialViewPage.Customize(browser);
                    htmlPartialViewPage.SetText("AUTOLOGOUTAFTERMINUTESOFINACTIVITY", autoLogOutAfterMinutesOfInactivity.ToString(), ArgumentOptions.NoHtmlEncoding);
                    htmlPartialViewPage.SetText("CONTENT", html, ArgumentOptions.NoHtmlEncoding);
                    htmlPartialViewPage.SetText("AUTHENTICATION-FACTOR", authenticationFactor, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);

                    if (encryptionKey != null)
                    {
                        using (Storage.Database database = new Storage.Database(encryptionKey))
                        {
                            // set private encryption key
                            database.PrivateEncryptionKey = token.PrivateEncryptionKey;

                            Storage.Account accountStorage = new Storage.Account();

                            foreach (Plugin.PluginBase plugin in Http.Server.Plugins)
                            {
                                if (plugin.State == Plugin.PluginBase.PluginState.Loaded)
                                {
                                    plugin.Database = new Storage.Database(database.EncryptionKey);
                                    plugin.Initialize();
                                    plugin.State = Plugin.PluginBase.PluginState.Initialized;
                                }

                                if (plugin.IsVisible(browser))
                                {
                                    HtmlPartialViewPage htmlPluginNavigatorMenuItem = htmlPartialViewPage.GetPartialView("PLUGINS");
                                    if (htmlPluginNavigatorMenuItem != null)
                                    {
                                        htmlPluginNavigatorMenuItem.SetText("PLUGIN-URL", plugin.URL, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                        htmlPluginNavigatorMenuItem.SetText("PLUGIN-ICON", plugin.Icon, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                        htmlPluginNavigatorMenuItem.SetText("PLUGIN-NAME", plugin.GetName(browser.Session.Locale), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                        htmlPluginNavigatorMenuItem.SetText("PLUGIN-DESCRIPTION", plugin.GetDescription(browser.Session.Locale), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                    }
                                }
                            }

                            Storage.PhamePost phamePostStorage = new Storage.PhamePost();
                            foreach (string blogName in phamePostStorage.Get(database).Select(post => post.Blog).Distinct().OrderBy(blog => blog))
                            {
                                HtmlPartialViewPage htmlPhameBlogsMenuItem = htmlPartialViewPage.GetPartialView("PHAME-BLOGS");
                                if (htmlPhameBlogsMenuItem != null)
                                {
                                    htmlPhameBlogsMenuItem.SetText("PHAME-BLOG-NAME", blogName, HtmlViewPage.ArgumentOptions.Default);
                                }
                            }
                        }
                    }

                    htmlPartialViewPage.Merge();

                    htmlPartialHeaderViewPage.SetContent(browser, GetViewData("HomePage.Authenticated.HeaderActions"));
                    htmlPartialHeaderViewPage.SetText("AUTHENTICATION-FACTOR", authenticationFactor, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                    htmlPartialHeaderViewPage.Merge();

                    htmlViewPage.SetContent(browser, GetViewData("HomePage.Template"));
                    htmlViewPage.Customize(browser);
                    htmlViewPage.SetText("THEME", Theme, ArgumentOptions.NoHtmlEncoding);
                    htmlViewPage.SetText("THEME-STYLE", themeStyle, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("LOCALE", Browser.Session.Locale, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("SYNCHRONIZE", "", ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("HEADERACTIONS", htmlPartialHeaderViewPage.Content, ArgumentOptions.NoHtmlEncoding);
                    htmlViewPage.SetText("ICON-USERNAME", char.ToUpper(userName.FirstOrDefault()).ToString(), ArgumentOptions.NoHtmlEncoding);
                    htmlViewPage.SetText("LANGUAGE-OPTIONS", GetLanguageOptions(browser.Session.Locale), HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                    htmlViewPage.SetText("CONTENT", htmlPartialViewPage.Content, ArgumentOptions.NoHtmlEncoding);
                    htmlViewPage.SetText("PHABRICO-VERSION", VersionInfo.Version, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.SetText("PHABRICO-ROOTPATH", Http.Server.RootPath, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    htmlViewPage.Merge();
                    html = htmlViewPage.Content;
                }
            }

            // set up content view name to identify the original view
            string contentViewName;
            if (Url == null)
            {
                contentViewName = "";
            }
            else
            {
                contentViewName = RegexSafe.Replace(Url, @"[A-Z]", m => "-" + m.ToString().ToLower());
                contentViewName = RegexSafe.Replace(contentViewName, @"^-", "");
            }
            html = html.Replace("@@CONTENT-VIEW-NAME@@", contentViewName.Replace(".", "-"));

            return html;
        }

        /// <summary>
        /// Merges all subviews with the current view
        /// </summary>
        public void Merge()
        {
            // parse conditional statements
            Regex regConditionalStatements = new Regex(@"\@{IF\x20*(NOT)?\x20+?([^=]*=[^@]*)@    # opening {
                                                 (                      # begin of content
                                                     (?>                # now match...
                                                        [^{}]+          # any characters except braces
                                                     |                  # or
                                                        \{  (?<DEPTH>)  # a {, increasing the depth counter
                                                     |                  # or
                                                        \}  (?<-DEPTH>) # a }, decreasing the depth counter
                                                     )*                 # any number of times
                                                     (?(DEPTH)(?!))     # until the depth counter is zero again
                                                 )                      # end of content
                                               \}@                     # then match the closing }
                                               (
                                                 [^{}]*{ELSE
                                                     (                          # begin of content
                                                         (?>                    # now match...
                                                            [^{}]+              # any characters except braces
                                                         |                      # or
                                                            \{  (?<ELSEDEPTH>)  # a {, increasing the depth counter
                                                         |                      # or
                                                            \}  (?<-ELSEDEPTH>) # a }, decreasing the depth counter
                                                         )*                     # any number of times
                                                         (?(ELSEDEPTH)(?!))     # until the depth counter is zero again
                                                     )                          # end of content
                                                   \}@                          # then match the closing }
                                               )?",
                                        RegexOptions.IgnorePatternWhitespace);

            // Merge subviews
            foreach (HtmlPartialViewPage partialSubView in _partialSubViews.OrderByDescending(subview => subview.Position)
                                                                           .ThenByDescending(subview => _partialSubViews.IndexOf(subview)))
            {
                partialSubView.Merge();

                if (Content.Contains(partialSubView.TemplateContent))
                {
                    Content = Content.Substring(0, partialSubView.Position) +
                              partialSubView.Content +
                              Content.Substring(partialSubView.Position + partialSubView.TemplateContent.Length);
                }
                else
                {
                    Content = Content.Substring(0, partialSubView.Position) +
                              partialSubView.Content +
                              Content.Substring(partialSubView.Position);
                }
            }

            // Process conditional IF statements
            while (true)
            {
                List<Match> conditionalStatements = regConditionalStatements.Matches(Content).OfType<Match>().OrderByDescending(m => m.Index).ToList();
                if (conditionalStatements.Any() == false) break;

                foreach (Match conditionalStatement in conditionalStatements)
                {
                    string[] conditionValues = conditionalStatement.Groups[2].Value.Split('=');
                    string firstCondition = string.Join("=", conditionValues.Take(conditionValues.Length - 1));
                    string secondCondition = conditionValues.Last();

                    bool positiveCondition = firstCondition.Equals(secondCondition);
                    if (conditionalStatement.Groups[1].Value.Equals("NOT"))
                    {
                        positiveCondition = !positiveCondition;
                    }

                    if (positiveCondition)
                    {
                        // conditional IF statement returned true -> remove IF statement, but keep content
                        Content = Content.Substring(0, conditionalStatement.Index)
                                + conditionalStatement.Groups[3].Value
                                + Content.Substring(conditionalStatement.Index + conditionalStatement.Length);
                    }
                    else
                    {
                        if (conditionalStatement.Groups[5].Success)
                        {
                            // conditional IF statement returned false -> remove IF statement, but keep ELSE content
                            Content = Content.Substring(0, conditionalStatement.Index)
                                    + conditionalStatement.Groups[5].Value
                                    + Content.Substring(conditionalStatement.Index + conditionalStatement.Length);
                        }
                        else
                        {
                            // conditional IF statement returned false -> remove IF statement and content
                            Content = Content.Substring(0, conditionalStatement.Index)
                                    + Content.Substring(conditionalStatement.Index + conditionalStatement.Length);
                        }
                    }
                }
            }

            _partialSubViews.Clear();

            // in case there are any unused partial views left, clear them all by removing them from the content
            foreach (Match unusedPartialView in RegexSafe.Matches(Content, "@{[^\r\n@}]+\r?\n.+?}@", RegexOptions.Singleline).OfType<Match>().OrderByDescending(match => match.Index).ToArray())
            {
                Content = Content.Substring(0, unusedPartialView.Index)
                        + Content.Substring(unusedPartialView.Index + unusedPartialView.Length);
            }
        }

        internal string Send(Browser browser, object htmlViewPageOptions)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes a partial view HTML part from the current view
        /// </summary>
        /// <param name="templateName"></param>
        public void RemovePartialView(string templateName)
        {
            string regName = new string(templateName.SelectMany(c => "[" + c + "]").ToArray());

            Regex regPartialView = new Regex(@"\@{" + regName + @"      # opening {
                                                 (                      # begin of content
                                                     (?>                # now match...
                                                        [^{}]+          # any characters except braces
                                                     |                  # or
                                                        \{  (?<DEPTH>)  # a {, increasing the depth counter
                                                     |                  # or
                                                        \}  (?<-DEPTH>) # a }, decreasing the depth counter
                                                     )*                 # any number of times
                                                     (?(DEPTH)(?!))     # until the depth counter is zero again
                                                 )                      # end of content
                                               \}@                      # then match the closing }",
                                        RegexOptions.IgnorePatternWhitespace);
            Match partialView = regPartialView.Match(Content);
            if (partialView.Success)
            {
                Content = Content.Substring(0, partialView.Index) + Content.Substring(partialView.Index + partialView.Length);
            }
        }

        /// <summary>
        /// Sends the HtmlViewPage content to the web browser
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public string Send(Browser browser, ContentOptions options)
        {
            string html = GetFullContent(browser, options);

            // send data
            Send(browser, html);

            return html;
        }

        /// <summary>
        /// Sends some text to the web browser
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="text"></param>
        public void Send(Browser browser, string text)
        {
            // send data
            byte[] data = UTF8Encoding.UTF8.GetBytes(text);
            base.Send(browser, data);
        }

        /// <summary>
        /// Sends a sequence of bytes to the web browser
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="data"></param>
        public override void Send(Browser browser, byte[] data = null)
        {
            if (data == null)
            {
                data = UTF8Encoding.UTF8.GetBytes(Content);
            }

            base.Send(browser, data);
        }

        /// <summary>
        /// Overwrites the content of this HtmlViewPage with new content
        /// </summary>
        /// <param name="newContent"></param>
        public void SetContent(Browser browser, string newContent)
        {
            Content = newContent;
            Customize(browser);
        }

        /// <summary>
        /// Sets the value for a given HtmlViewPage parameter.
        /// HtmlViewPage parameters are defined as @@NAME@@ in the HTML code
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="parameterValue"></param>
        /// <param name="argumentOptions"></param>
        public void SetText(string parameterName, string parameterValue, ArgumentOptions argumentOptions = ArgumentOptions.Default)
        {
            if (_partialSubViews.Any()) throw new Exception.PhabricoException("HtmlViewPage.SetText can not be executed between HtmlViewPage.GetPartialView and HtmlViewPage.Merge");

            string internalParameterName = "@@" + parameterName + "@@";

            if (argumentOptions.HasFlag(ArgumentOptions.AllowEmptyParameterValue) == false &&
                string.IsNullOrEmpty(parameterValue))
            {
                parameterValue = Locale.TranslateText("EmptyParameter", Browser.Session.Locale);
            }

            if (parameterValue == null)
            {
                parameterValue = "";
            }
            
            if (argumentOptions.HasFlag(ArgumentOptions.NoHtmlEncoding) == false)
            {
                char temporaryAmpersandReplacement = (char)1;
                string[] nonHtmlCharacters = RegexSafe.Matches(parameterValue, @"[^A-Za-z0-9 ,./?!@#$%*()\-_+={}\[\]:;]")
                                                      .OfType<Match>()
                                                      .Select(match => match.Value)
                                                      .Where(character => character[0] <= 0xFF)
                                                      .ToArray();
                foreach (string character in nonHtmlCharacters)
                {
                    parameterValue = parameterValue.Replace(character, string.Format("{0}#{1};", temporaryAmpersandReplacement, (int)character[0]));
                }

                parameterValue = parameterValue.Replace(temporaryAmpersandReplacement, '&');
            }

            if (argumentOptions.HasFlag(ArgumentOptions.JavascriptEncoding))
            {
                parameterValue = parameterValue.Replace("\\", "\\\\")   // escape backslash
                                               .Replace("\"", "\\\"");  // escape double-quote
            }

            Content = Content.Replace(internalParameterName, parameterValue);
        }
    }
}