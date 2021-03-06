using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Http;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;
using Phabrico.Phabricator.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using static Phabrico.Phabricator.Data.Account;

namespace Phabrico.Controllers
{
    /// <summary>
    /// Represents the controller being used for the Configuration screen in Phabrico
    /// </summary>
    public class Configuration : Controller
    {
        /// <summary>
        /// This method is fired when the Configuration screen in Phabrico is opened
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="viewPage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/configure", ServerCache = false)]
        public void HttpGetLoadParameters(Http.Server httpServer, Browser browser, ref HtmlViewPage viewPage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.HideConfig) throw new Phabrico.Exception.HttpNotFound("/configure");

            Storage.Account accountStorage = new Storage.Account();
            if (accountStorage != null)
            {
                SessionManager.Token token = SessionManager.GetToken(browser);
                if (token == null) throw new Phabrico.Exception.AccessDeniedException("/configure", "session expired");

                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    if (token.AuthenticationFactor == AuthenticationFactor.Public) throw new Phabrico.Exception.AccessDeniedException("/configure", "You don't have sufficient rights to configure Phabrico");

                    // set private encryption key
                    database.PrivateEncryptionKey = token.PrivateEncryptionKey;

                    Account existingAccount = accountStorage.Get(database, token);
                    if (existingAccount != null)
                    {
                        string syncMethodManiphestValue = "ManiphestSelectedUsersOnly";
                        string syncMethodPhrictionValue = "PhrictionAllProjects";

                        string authenticationFactor = database.GetConfigurationParameter("AuthenticationFactor");

                        if (existingAccount.Parameters.Synchronization.HasFlag(SynchronizationMethod.ManiphestSelectedProjectsOnly))
                        {
                            syncMethodManiphestValue = "ManiphestSelectedProjectsOnly";
                        }
                        if (existingAccount.Parameters.Synchronization.HasFlag(SynchronizationMethod.PhrictionSelectedProjectsOnly))
                        {
                            syncMethodPhrictionValue = "PhrictionSelectedProjectsOnly";
                        }
                        if (existingAccount.Parameters.Synchronization.HasFlag(SynchronizationMethod.PhrictionSelectedProjectsOnlyIncludingDocumentTree))
                        {
                            syncMethodPhrictionValue = "PhrictionSelectedProjectsOnlyIncludingDocumentTree";
                        }
                        if (existingAccount.Parameters.Synchronization.HasFlag(SynchronizationMethod.PhrictionAllSelectedProjectsOnly))
                        {
                            syncMethodPhrictionValue = "PhrictionAllSelectedProjectsOnly";
                        }
                        if (existingAccount.Parameters.Synchronization.HasFlag(SynchronizationMethod.PhrictionAllSelectedProjectsOnlyIncludingDocumentTree))
                        {
                            syncMethodPhrictionValue = "PhrictionAllSelectedProjectsOnlyIncludingDocumentTree";
                        }
                        if (existingAccount.Parameters.Synchronization.HasFlag(SynchronizationMethod.PhrictionAllProjects))
                        {
                            syncMethodPhrictionValue = "PhrictionAllProjects";
                        }

                        viewPage.AddDefaultParameterValues(new Dictionary<string, string>()
                        {
                            { "conduitApiToken", existingAccount.ConduitAPIToken },
                            { "defaultStateModifiedManiphest", existingAccount.Parameters.DefaultStateModifiedManiphest.ToString() },
                            { "defaultStateModifiedPhriction", existingAccount.Parameters.DefaultStateModifiedPhriction.ToString() },
                            { "showPhrictionMetadata", existingAccount.Parameters.ShowPhrictionMetadata.ToString() },
                            { "forceDownloadAllPhrictionMetadata", existingAccount.Parameters.ForceDownloadAllPhrictionMetadata.ToString() },
                            { "phabricatorUrl", existingAccount.PhabricatorUrl },
                            { "removalPeriodClosedManiphests", existingAccount.Parameters.RemovalPeriodClosedManiphests.ToString() },
                            { "syncMethodManiphest", syncMethodManiphestValue.ToString() },
                            { "syncMethodPhriction", syncMethodPhrictionValue.ToString() },
                            { "clipboardCopyForCodeBlock", existingAccount.Parameters.ClipboardCopyForCodeBlock.ToString() },
                            { "uiTranslation", existingAccount.Parameters.UITranslation.ToString() },
                            { "autoLogon", authenticationFactor == null ? "False" :
                                           authenticationFactor == AuthenticationFactor.Knowledge ? "False" :
                                           authenticationFactor == AuthenticationFactor.Ownership ? "Windows" :
                                           "True" },
                            { "autoLogOutAfterMinutesOfInactivity", existingAccount.Parameters.AutoLogOutAfterMinutesOfInactivity.ToString() },
                            { "darkenImages", existingAccount.Parameters.DarkenBrightImages.ToString() }
                        });

                        string confidentialTableHeaders = "";
                        if (existingAccount.Parameters.ColumnHeadersToHide.Any())
                        {
                            confidentialTableHeaders = "'" + string.Join("', '", existingAccount.Parameters.ColumnHeadersToHide) + "'";
                        }
                        viewPage.SetText("CONFIG-CONFIDENTIAL-TABLE-HEADERS", confidentialTableHeaders, HtmlViewPage.ArgumentOptions.JavascriptEncoding);
                    }
                    else
                    {
                        viewPage.SetText("CONFIG-CONFIDENTIAL-TABLE-HEADERS", "", HtmlViewPage.ArgumentOptions.JavascriptEncoding);
                    }

                    // check if we need to show some help for the first time
                    Phabricator.Data.Account accountData = accountStorage.Get(database).FirstOrDefault();
                    if (accountData != null && accountData.Parameters.LastSynchronizationTimestamp == DateTimeOffset.MinValue)
                    {
                        viewPage.SetText("SHOW-FIRSTTIME-HELP", "Yes");
                    }
                    else
                    {
                        viewPage.SetText("SHOW-FIRSTTIME-HELP", "No");
                    }

                    viewPage.Customize(browser);

                    foreach (Plugin.PluginBase plugin in Http.Server.Plugins)
                    {
                        if (plugin.State == Plugin.PluginBase.PluginState.Loaded)
                        {
                            plugin.Database = new Storage.Database(database.EncryptionKey);
                            plugin.Initialize();
                            plugin.State = Plugin.PluginBase.PluginState.Initialized;
                        }

                        HtmlViewPage configurationViewPage = plugin.GetConfigurationViewPage(browser);
                        if (configurationViewPage != null)
                        {
                            HtmlPartialViewPage htmlPluginNavigatorTabHeader = viewPage.GetPartialView("CONFIGURABLE-PLUGIN-TAB-HEADER");
                            if (htmlPluginNavigatorTabHeader != null)
                            {
                                htmlPluginNavigatorTabHeader.SetText("PLUGIN-NAME", plugin.GetName(browser.Session.Locale ?? browser.Language), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                            }

                            HtmlPartialViewPage htmlPluginNavigatorTabContent = viewPage.GetPartialView("CONFIGURABLE-PLUGIN-TAB-CONTENT");
                            if (htmlPluginNavigatorTabContent != null)
                            {
                                htmlPluginNavigatorTabContent.SetText("PLUGIN-NAME", plugin.GetName(browser.Session.Locale ?? browser.Language), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                htmlPluginNavigatorTabContent.SetText("PLUGIN-SCREEN-CONTENT", configurationViewPage.Content, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);

                                Type loadConfigurationParametersControllerType = plugin.Assembly.GetExportedTypes()
                                                                                                .FirstOrDefault(t => t.BaseType == typeof(Plugin.PluginController)
                                                                                                                  && t.GetMethod("LoadConfigurationParameters", 
                                                                                                                                  BindingFlags.Public
                                                                                                                                  | BindingFlags.Instance
                                                                                                                                  | BindingFlags.DeclaredOnly
                                                                                                                                ) != null
                                                                                                               );
                                if (loadConfigurationParametersControllerType != null)
                                {
                                    try
                                    {
                                        MethodInfo loadConfigurationParameters = loadConfigurationParametersControllerType.GetMethod("LoadConfigurationParameters",
                                                                                                                                        BindingFlags.Public
                                                                                                                                        | BindingFlags.Instance
                                                                                                                                        | BindingFlags.DeclaredOnly
                                                                                                                                    );
                                        Controller pluginController = loadConfigurationParametersControllerType.GetConstructor(Type.EmptyTypes).Invoke(null) as Controller;
                                        pluginController.browser = browser;
                                        pluginController.EncryptionKey = EncryptionKey;
                                        pluginController.TokenId = TokenId;
                                        loadConfigurationParameters.Invoke(pluginController, new object[] { plugin, htmlPluginNavigatorTabContent });
                                    }
                                    catch (System.Exception loadConfigurationParametersException)
                                    {
                                        Logging.WriteException(plugin.GetName(browser.Session.Locale ?? browser.Language) + "::loadConfigurationParameters", loadConfigurationParametersException);
                                    }
                                }
                            }
                        }
                    }

                    viewPage.Merge();
                }
            }
        }

        /// <summary>
        /// This method is fired when a parameter in the Configuration screen is modified.
        /// This will happen when the input field loses focus
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="parameters"></param>
        [UrlController(URL = "/configure")]
        public Http.Response.HttpMessage HttpPostSave(Http.Server httpServer, Browser browser, string[] parameters)
        {
            if (browser.InvalidCSRF(browser.Request.RawUrl)) throw new Phabrico.Exception.InvalidCSRFException();
            if (httpServer.Customization.HideConfig) throw new Phabrico.Exception.HttpNotFound("/configure");

            if (ValidatePostSave(browser.Session.FormVariables[browser.Request.RawUrl]) == false)
            {
                return new Http.Response.HttpUnprocessableEntity(httpServer, browser, "/configure");
            }

            try
            {
                Storage.Account accountStorage = new Storage.Account();

                SessionManager.Token token = SessionManager.GetToken(browser);
                if (token == null) throw new Phabrico.Exception.AccessDeniedException("/configure", "session expired");

                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/configure", "You don't have sufficient rights to configure Phabrico");

                    // set private encryption key
                    database.PrivateEncryptionKey = token.PrivateEncryptionKey;

                    Account existingAccount = accountStorage.Get(database, token);
                    if (existingAccount != null)
                    {
                        existingAccount.ConduitAPIToken = browser.Session.FormVariables[browser.Request.RawUrl]["conduitApiToken"];
                        existingAccount.PhabricatorUrl = browser.Session.FormVariables[browser.Request.RawUrl]["phabricatorUrl"];

                        // determine synchronization method for Phriction and Maniphest
                        SynchronizationMethod newManiphestSynchronizationMethod, newPhrictionSynchronizationMethod;
                        if (Enum.TryParse<SynchronizationMethod>(browser.Session.FormVariables[browser.Request.RawUrl]["syncMethodManiphest"], out newManiphestSynchronizationMethod) == false)
                        {
                            newManiphestSynchronizationMethod = SynchronizationMethod.PerUsers;
                        }

                        if (Enum.TryParse<SynchronizationMethod>(browser.Session.FormVariables[browser.Request.RawUrl]["syncMethodPhriction"], out newPhrictionSynchronizationMethod) == false)
                        {
                            newPhrictionSynchronizationMethod = SynchronizationMethod.PerUsers;
                        }

                        // check if synchronization methods have been modified
                        SynchronizationMethod activeSynchronizationMethod = existingAccount.Parameters.Synchronization;
                        if (activeSynchronizationMethod != (newManiphestSynchronizationMethod | newPhrictionSynchronizationMethod))
                        {
                            // synchronization methods have been modified => check if we need to download more the next synchronization than we did before
                            bool downloadEverythingAtNextSynchronization = false;

                            if (activeSynchronizationMethod.HasFlag(SynchronizationMethod.PhrictionAllProjects) == false &&
                                newPhrictionSynchronizationMethod.HasFlag(SynchronizationMethod.PhrictionAllProjects))
                            {
                                downloadEverythingAtNextSynchronization = true;
                            }

                            if (activeSynchronizationMethod.HasFlag(SynchronizationMethod.PhrictionAllProjects) == false &&
                                activeSynchronizationMethod.HasFlag(SynchronizationMethod.PhrictionSelectedProjectsOnlyIncludingDocumentTree) == false &&
                                newPhrictionSynchronizationMethod.HasFlag(SynchronizationMethod.PhrictionSelectedProjectsOnlyIncludingDocumentTree))
                            {
                                downloadEverythingAtNextSynchronization = true;
                            }

                            if (activeSynchronizationMethod.HasFlag(SynchronizationMethod.PerProjects) == false &&
                                newPhrictionSynchronizationMethod.HasFlag(SynchronizationMethod.PerProjects))
                            {
                                downloadEverythingAtNextSynchronization = true;
                            }

                            if (activeSynchronizationMethod.HasFlag(SynchronizationMethod.PerUsers) == false &&
                                newPhrictionSynchronizationMethod.HasFlag(SynchronizationMethod.PerUsers))
                            {
                                downloadEverythingAtNextSynchronization = true;
                            }

                            if (downloadEverythingAtNextSynchronization)
                            {
                                ResetLastSynchronizationTimeProjects(database);
                                ResetLastSynchronizationTimeUsers(database);
                            }

                            existingAccount.Parameters.Synchronization = newManiphestSynchronizationMethod | newPhrictionSynchronizationMethod;
                        }


                        existingAccount.Parameters.RemovalPeriodClosedManiphests = (RemovalPeriod)Enum.Parse(typeof(RemovalPeriod), (string)browser.Session.FormVariables[browser.Request.RawUrl]["removalPeriodClosedManiphests"]);

                        try
                        {
                            existingAccount.Parameters.AutoLogOutAfterMinutesOfInactivity = Int32.Parse(browser.Session.FormVariables[browser.Request.RawUrl]["autoLogOutAfterMinutesOfInactivity"].ToString());
                            if (existingAccount.Parameters.AutoLogOutAfterMinutesOfInactivity >= 1440)
                            {
                                // set max inactivity to 1 day 
                                // the AutoLogOff javascript timer may not work correctly if you use a very large value in here.
                                // I guess 1 day should be enough
                                existingAccount.Parameters.AutoLogOutAfterMinutesOfInactivity = 1440;
                            }
                        }
                        catch
                        {
                            existingAccount.Parameters.AutoLogOutAfterMinutesOfInactivity = 5;
                        }

                        existingAccount.Parameters.DefaultStateModifiedManiphest = (DefaultStateModification)Enum.Parse(typeof(DefaultStateModification), (string)browser.Session.FormVariables[browser.Request.RawUrl]["defaultStateModifiedManiphest"]);
                        existingAccount.Parameters.DefaultStateModifiedPhriction = (DefaultStateModification)Enum.Parse(typeof(DefaultStateModification), (string)browser.Session.FormVariables[browser.Request.RawUrl]["defaultStateModifiedPhriction"]);

                        existingAccount.Parameters.ShowPhrictionMetadata = bool.Parse((string)browser.Session.FormVariables[browser.Request.RawUrl]["showPhrictionMetadata"]);
                        existingAccount.Parameters.ForceDownloadAllPhrictionMetadata = bool.Parse((string)browser.Session.FormVariables[browser.Request.RawUrl]["forceDownloadAllPhrictionMetadata"]);

                        existingAccount.Theme = browser.Session.FormVariables[browser.Request.RawUrl]["theme"];

                        string darkenImages = "Disabled";
                        if (browser.Session.FormVariables[browser.Request.RawUrl]?.ContainsKey("darkenImages") == true)
                        {
                            darkenImages = (string)browser.Session.FormVariables[browser.Request.RawUrl]["darkenImages"];
                        }

                        existingAccount.Parameters.DarkenBrightImages = (DarkenImageStyle)Enum.Parse(typeof(DarkenImageStyle), darkenImages);

                        existingAccount.Parameters.ClipboardCopyForCodeBlock = bool.Parse((string)browser.Session.FormVariables[browser.Request.RawUrl]["clipboardCopyForCodeBlock"]);
                        existingAccount.Parameters.UITranslation = bool.Parse((string)browser.Session.FormVariables[browser.Request.RawUrl]["uiTranslation"]);

                        string autoLogonValue = (string)browser.Session.FormVariables[browser.Request.RawUrl]["autoLogon"];
                        switch (autoLogonValue)
                        {
                            case "Windows":
                                database.SetConfigurationParameter("EncryptionKey", null);
                                database.SetConfigurationParameter("AuthenticationFactor", AuthenticationFactor.Ownership);

                                string newPassword = Encryption.GetDPAPIKey();
                                UInt64[] newPublicDpapiXorValue = Authentication.GetXorValue(EncryptionKey, newPassword);
                                UInt64[] newPrivateDpapiXorValue = Authentication.GetXorValue(database.PrivateEncryptionKey, newPassword);

                                accountStorage.UpdateDpapiXorCipher(database, newPublicDpapiXorValue, newPrivateDpapiXorValue);
                                break;

                            case "True":
                                database.SetConfigurationParameter("EncryptionKey", database.EncryptionKey);
                                database.SetConfigurationParameter("AuthenticationFactor", AuthenticationFactor.Public);
                                break;

                            case "False":
                                database.SetConfigurationParameter("EncryptionKey", null);
                                database.SetConfigurationParameter("AuthenticationFactor", AuthenticationFactor.Knowledge);
                                break;
                        }

                        accountStorage.Set(database, existingAccount);

                        Http.Server.InvalidateNonStaticCache(database, DateTime.MaxValue);
                    }
                }
            }
            catch
            {
                // in case we requested too many save calls, we might get some exceptions because the FormVariables has been reinitialized (and some variables might be missing)
            }

            return null;
        }

        /// <summary>
        /// This method is fired when one or more confidential table headers are modified in the Configuration screen
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="parameters"></param>
        [UrlController(URL = "/configure/table-headers")]
        public void HttpPostSaveConfidentialTableHeaders(Http.Server httpServer, Browser browser, string[] parameters)
        {
            if (httpServer.Customization.HideConfig) throw new Phabrico.Exception.HttpNotFound("/configure/table-headers");

            Storage.Account accountStorage = new Storage.Account();

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException("/configure", "session expired");

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/configure", "You don't have sufficient rights to configure Phabrico");

                // set private encryption key
                database.PrivateEncryptionKey = token.PrivateEncryptionKey;

                Account existingAccount = accountStorage.Get(database, token);
                if (existingAccount != null)
                {
                    string jsonArrayConfidentialTableHeaders = browser.Session.FormVariables[browser.Request.RawUrl]["data"];
                    JArray confidentialTableHeaders = JsonConvert.DeserializeObject(jsonArrayConfidentialTableHeaders) as JArray;

                    existingAccount.Parameters.ColumnHeadersToHide = confidentialTableHeaders.Select(jtoken => jtoken.ToString()).ToArray();
                    accountStorage.Set(database, existingAccount);
                }
            }
        }

        /// <summary>
        /// Last-sync time of the selected users will be reset.
        /// This will cause during the next Phabricator-sync, that all Users records will be downloaded
        /// </summary>
        /// <param name="database"></param>
        private void ResetLastSynchronizationTimeProjects(Storage.Database database)
        {
            Storage.Project projectStorage = new Storage.Project();

            IEnumerable<Phabricator.Data.Project> selectedProjects = projectStorage.Get(database)
                                                                                   .Where(project => project.Selected == Phabricator.Data.Project.Selection.Selected);

            foreach (Phabricator.Data.Project selectedProject in selectedProjects.ToList())
            {
                selectedProject.DateSynchronized = DateTimeOffset.MinValue;
                projectStorage.Add(database, selectedProject);
            }
        }

        /// <summary>
        /// Last-sync time of the selected users will be reset.
        /// This will cause during the next Phabricator-sync, that all Users records will be downloaded
        /// </summary>
        /// <param name="database"></param>
        private void ResetLastSynchronizationTimeUsers(Storage.Database database)
        {
            Storage.User userStorage = new Storage.User();

            IEnumerable<Phabricator.Data.User> selectedUsers = userStorage.Get(database)
                                                                                   .Where(user => user.Selected == true);

            foreach (Phabricator.Data.User selectedUser in selectedUsers.ToList())
            {
                selectedUser.DateSynchronized = DateTimeOffset.MinValue;
                userStorage.Add(database, selectedUser);
            }
        }

        /// <summary>
        /// Verifies if all POST variables have valid data
        /// </summary>
        /// <param name="formVariables"></param>
        private bool ValidatePostSave(DictionarySafe<string, string> formVariables)
        {
            try
            {
                if (formVariables == null)
                {
                    throw new System.Exception("no formvariables");
                }

                if (string.IsNullOrEmpty(formVariables["conduitApiToken"]) == false &&
                    RegexSafe.IsMatch(formVariables["conduitApiToken"], "^api-[a-zA-Z0-9]{28}", RegexOptions.None) == false
                   )
                {
                    throw new System.Exception("conduitAPIToken");
                }

                if (string.IsNullOrEmpty(formVariables["phabricatorUrl"]) == false &&
                    RegexSafe.IsMatch(formVariables["phabricatorUrl"], "^https?://", RegexOptions.None) == false
                   )
                {
                    throw new System.Exception("phabricatorUrl");
                }

                DefaultStateModification defaultStateModification;
                if (Enum.TryParse<DefaultStateModification>(formVariables["defaultStateModifiedManiphest"], out defaultStateModification) == false)
                {
                    throw new System.Exception("defaultStateModifiedManiphest");
                }

                if (Enum.TryParse<DefaultStateModification>(formVariables["defaultStateModifiedPhriction"], out defaultStateModification) == false)
                {
                    throw new System.Exception("defaultStateModifiedPhriction");
                }

                bool boolValue;
                if (bool.TryParse(formVariables["showPhrictionMetadata"], out boolValue) == false)
                {
                    throw new System.Exception("showPhrictionMetadata");
                }

                if (bool.TryParse(formVariables["forceDownloadAllPhrictionMetadata"], out boolValue) == false)
                {
                    throw new System.Exception("forceDownloadAllPhrictionMetadata");
                }

                if (bool.TryParse(formVariables["clipboardCopyForCodeBlock"], out boolValue) == false)
                {
                    throw new System.Exception("clipboardCopyForCodeBlock");
                }

                if (bool.TryParse(formVariables["uiTranslation"], out boolValue) == false)
                {
                    throw new System.Exception("uiTranslation");
                }

                RemovalPeriod removalPeriod;
                if (Enum.TryParse<RemovalPeriod>(formVariables["removalPeriodClosedManiphests"], out removalPeriod) == false)
                {
                    throw new System.Exception("removalPeriodClosedManiphests");
                }

                SynchronizationMethod synchronizationMethod;
                if (Enum.TryParse<SynchronizationMethod>(formVariables["syncMethodManiphest"], out synchronizationMethod) == false)
                {
                    throw new System.Exception("syncMethodManiphest");
                }

                if (Enum.TryParse<SynchronizationMethod>(formVariables["syncMethodPhriction"], out synchronizationMethod) == false)
                {
                    throw new System.Exception("syncMethodPhriction");
                }

                if (formVariables["autoLogon"].Equals("True") == false
                    && formVariables["autoLogon"].Equals("False") == false
                    && formVariables["autoLogon"].Equals("Windows") == false)
                {
                    throw new System.Exception("autoLogon");
                }

                int intValue;
                if (int.TryParse(formVariables["autoLogOutAfterMinutesOfInactivity"], out intValue) == false)
                {
                    throw new System.Exception("autoLogOutAfterMinutesOfInactivity");
                }

                DarkenImageStyle darkenImageStyle;
                if (Enum.TryParse<DarkenImageStyle>(formVariables["darkenImages"], out darkenImageStyle) == false)
                {
                    throw new System.Exception("darkenImages");
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}