using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Phabrico.Miscellaneous;

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
            /// The global menu navigator at the right and the header on top are merged into this HtmlViewPage
            /// </summary>
            Default = 0,

            /// <summary>
            /// The global menu navigator will not be merged into this HtmlViewPage
            /// </summary>
            HideGlobalTreeView = 1,

            /// <summary>
            /// A custom menu navigator instead of the global menu navigator will be merged into this HtmlViewPage
            /// </summary>
            UseLocalTreeView = 2,

            /// <summary>
            /// The header on top will not be merged into this HtmlViewPage
            /// </summary>
            HideHeader = 4,

            /// <summary>
            /// The content is shown in an IFrame: header and global treeview are not shown and no Phabrico CSS is put in the view
            /// </summary>
            IFrame = 1 + 4 + 8
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
            string html = Content;
            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token != null)
            {
                string encryptionKey = token?.EncryptionKey;

                using (Storage.Database database = new Storage.Database(null))
                {
                    Storage.Account accountStorage = new Storage.Account();
                    UInt64[] publicXorCipher = accountStorage.GetPublicXorCipher(database, token);

                    // unmask encryption key
                    encryptionKey = Encryption.XorString(encryptionKey, publicXorCipher);
                }

                using (Storage.Database database = new Storage.Database(encryptionKey))
                {
                    // unmask private encryption key
                    Storage.Account accountStorage = new Storage.Account();
                    if (token.PrivateEncryptionKey != null)
                    {
                        UInt64[] privateXorCipher = accountStorage.GetPrivateXorCipher(database, token);
                        database.PrivateEncryptionKey = Encryption.XorString(token.PrivateEncryptionKey, privateXorCipher);
                    }

                    HtmlViewPage htmlViewPage = new HtmlViewPage(browser);
                    HtmlViewPage htmlPartialViewPage = new HtmlViewPage(browser);
                    HtmlViewPage htmlPartialHeaderViewPage = new HtmlViewPage(browser);

                    Phabricator.Data.Account accountData = accountStorage.WhoAmI(database);

                    string themeStyle = "";
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

                    if (contentOptions.HasFlag(ContentOptions.UseLocalTreeView))
                    {
                        if (contentOptions.HasFlag(ContentOptions.HideHeader))
                        {
                            htmlViewPage.SetContent(GetViewData("HomePage.NoHeaderLocalTreeView.Template"));
                            htmlViewPage.SetText("THEME", Theme, ArgumentOptions.NoHtmlEncoding);
                            htmlViewPage.SetText("THEME-STYLE", themeStyle, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                            htmlViewPage.SetText("LOCALE", Browser.Session.Locale, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                            htmlViewPage.SetText("CONTENT", html, ArgumentOptions.NoHtmlEncoding);
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
                        string userName = accountData.UserName;
                        string authenticationFactor = database.GetAuthenticationFactor();

                        if (contentOptions.HasFlag(ContentOptions.IFrame))
                        {
                            htmlViewPage.SetContent(GetViewData("HomePage.IFrameContent"));
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
                                htmlViewPage.SetContent(GetViewData("HomePage.NoHeaderTreeView.Template"));
                                htmlViewPage.SetText("THEME", Theme, ArgumentOptions.NoHtmlEncoding);
                                htmlViewPage.SetText("LOCALE", Browser.Session.Locale, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                htmlViewPage.SetText("CONTENT", html, ArgumentOptions.NoHtmlEncoding);
                                htmlViewPage.Merge();
                                html = htmlViewPage.Content;
                            }
                            else
                            {
                                htmlPartialViewPage.SetContent(GetViewData("HomePage.NoTreeView.Template"));
                                htmlPartialViewPage.SetText("AUTOLOGOUTAFTERMINUTESOFINACTIVITY", database.GetAccountConfiguration()?.AutoLogOutAfterMinutesOfInactivity.ToString(), ArgumentOptions.NoHtmlEncoding);
                                htmlPartialViewPage.SetText("CONTENT", html, ArgumentOptions.NoHtmlEncoding);
                                htmlPartialViewPage.SetText("AUTHENTICATION-FACTOR", authenticationFactor, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                                htmlPartialViewPage.Merge();

                                htmlPartialHeaderViewPage.SetContent(GetViewData("HomePage.Authenticated.HeaderActions"));
                                htmlPartialHeaderViewPage.SetText("AUTHENTICATION-FACTOR", authenticationFactor, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                                htmlPartialHeaderViewPage.Merge();

                                htmlViewPage.SetContent(GetViewData("HomePage.Template"));
                                htmlViewPage.SetText("THEME", Theme, ArgumentOptions.NoHtmlEncoding);
                                htmlViewPage.SetText("THEME-STYLE", themeStyle, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                htmlViewPage.SetText("LOCALE", Browser.Session.Locale, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                htmlViewPage.SetText("SYNCHRONIZE", "", ArgumentOptions.AllowEmptyParameterValue);
                                htmlViewPage.SetText("HEADERACTIONS", htmlPartialHeaderViewPage.Content, ArgumentOptions.NoHtmlEncoding);
                                htmlViewPage.SetText("ICON-USERNAME", char.ToUpper(userName.FirstOrDefault()).ToString(), ArgumentOptions.NoHtmlEncoding);
                                htmlViewPage.SetText("LANGUAGE-OPTIONS", GetLanguageOptions(browser.Session.Locale), HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                                htmlViewPage.SetText("CONTENT", htmlPartialViewPage.Content, ArgumentOptions.NoHtmlEncoding);
                                htmlViewPage.SetText("PHABRICO-VERSION", VersionInfo.Version, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                htmlViewPage.Merge();
                                html = htmlViewPage.Content;
                            }
                        }
                        else
                        {
                            htmlPartialViewPage.SetContent(GetViewData("HomePage.TreeView.Template"));
                            htmlPartialViewPage.SetText("AUTOLOGOUTAFTERMINUTESOFINACTIVITY", database.GetAccountConfiguration()?.AutoLogOutAfterMinutesOfInactivity.ToString(), ArgumentOptions.NoHtmlEncoding);
                            htmlPartialViewPage.SetText("CONTENT", html, ArgumentOptions.NoHtmlEncoding);
                            htmlPartialViewPage.SetText("AUTHENTICATION-FACTOR", authenticationFactor, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
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
                            htmlPartialViewPage.Merge();

                            htmlPartialHeaderViewPage.SetContent(GetViewData("HomePage.Authenticated.HeaderActions"));
                            htmlPartialHeaderViewPage.SetText("AUTHENTICATION-FACTOR", authenticationFactor, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                            htmlPartialHeaderViewPage.Merge();

                            htmlViewPage.SetContent(GetViewData("HomePage.Template"));
                            htmlViewPage.SetText("THEME", Theme, ArgumentOptions.NoHtmlEncoding);
                            htmlViewPage.SetText("THEME-STYLE", themeStyle, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                            htmlViewPage.SetText("LOCALE", Browser.Session.Locale, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                            htmlViewPage.SetText("SYNCHRONIZE", "", ArgumentOptions.AllowEmptyParameterValue);
                            htmlViewPage.SetText("HEADERACTIONS", htmlPartialHeaderViewPage.Content, ArgumentOptions.NoHtmlEncoding);
                            htmlViewPage.SetText("ICON-USERNAME", char.ToUpper(userName.FirstOrDefault()).ToString(), ArgumentOptions.NoHtmlEncoding);
                            htmlViewPage.SetText("LANGUAGE-OPTIONS", GetLanguageOptions(browser.Session.Locale), HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                            htmlViewPage.SetText("CONTENT", htmlPartialViewPage.Content, ArgumentOptions.NoHtmlEncoding);
                            htmlViewPage.SetText("PHABRICO-VERSION", VersionInfo.Version, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                            htmlViewPage.Merge();
                            html = htmlViewPage.Content;
                        }
                    }
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
            foreach (Match conditionalStatement in regConditionalStatements.Matches(Content).OfType<Match>().OrderByDescending(m => m.Index).ToList())
            {
                string[] conditionValues = conditionalStatement.Groups[2].Value.Split('=');
                bool positiveCondition = conditionValues[0].Equals(conditionValues[1]);
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
        public void SetContent(string newContent)
        {
            Content = newContent;
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