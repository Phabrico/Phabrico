using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Phabrico.Http;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;
using Phabrico.Phabricator.Data;
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
            Storage.Account accountStorage = new Storage.Account();
            if (accountStorage != null)
            {
                SessionManager.Token token = SessionManager.GetToken(browser);

                using (Storage.Database database = new Storage.Database(null))
                {
                    UInt64[] publicXorCipher = accountStorage.GetPublicXorCipher(database, token);

                    // unmask encryption key
                    EncryptionKey = Encryption.XorString(EncryptionKey, publicXorCipher);
                }

                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/configure", "You don't have sufficient rights to configure Phabrico");

                    // unmask private encryption key
                    UInt64[] privateXorCipher = accountStorage.GetPrivateXorCipher(database, token);
                    database.PrivateEncryptionKey = Encryption.XorString(token.PrivateEncryptionKey, privateXorCipher);

                    Account existingAccount = accountStorage.Get(database, token);
                    if (existingAccount != null)
                    {
                        string syncMethodManiphestValue = "ManiphestSelectedUsersOnly";
                        string syncMethodPhrictionValue = "PhrictionAllProjects";

                        string publicEncryptionKey = database.GetConfigurationParameter("EncryptionKey");

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
                            { "autoLogon", publicEncryptionKey == null ? "False" : "True" },
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
                                htmlPluginNavigatorTabHeader.SetText("PLUGIN-NAME", plugin.GetName(browser.Session.Locale), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                            }

                            HtmlPartialViewPage htmlPluginNavigatorTabContent = viewPage.GetPartialView("CONFIGURABLE-PLUGIN-TAB-CONTENT");
                            if (htmlPluginNavigatorTabContent != null)
                            {
                                htmlPluginNavigatorTabContent.SetText("PLUGIN-NAME", plugin.GetName(browser.Session.Locale), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
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
                                        Logging.WriteException(plugin.GetName(browser.Session.Locale) + "::loadConfigurationParameters", loadConfigurationParametersException);
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
        public void HttpPostSave(Http.Server httpServer, Browser browser, string[] parameters)
        {
            Storage.Account accountStorage = new Storage.Account();

            SessionManager.Token token = SessionManager.GetToken(browser);

            using (Storage.Database database = new Storage.Database(null))
            {
                UInt64[] publicXorCipher = accountStorage.GetPublicXorCipher(database, token);

                // unmask encryption key
                EncryptionKey = Encryption.XorString(EncryptionKey, publicXorCipher);
            }

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/configure", "You don't have sufficient rights to configure Phabrico");

                // unmask private encryption key
                UInt64[] privateXorCipher = accountStorage.GetPrivateXorCipher(database, token);
                database.PrivateEncryptionKey = Encryption.XorString(token.PrivateEncryptionKey, privateXorCipher);

                Account existingAccount = accountStorage.Get(database, token);
                if (existingAccount != null)
                {
                    existingAccount.ConduitAPIToken = browser.Session.FormVariables["conduitApiToken"];
                    existingAccount.PhabricatorUrl = browser.Session.FormVariables["phabricatorUrl"];
                    
                    // determine synchronization method for Phriction and Maniphest
                    SynchronizationMethod newManiphestSynchronizationMethod, newPhrictionSynchronizationMethod;
                    if (Enum.TryParse<SynchronizationMethod>(browser.Session.FormVariables["syncMethodManiphest"], out newManiphestSynchronizationMethod) == false)
                    {
                        newManiphestSynchronizationMethod = SynchronizationMethod.PerUsers;
                    }

                    if (Enum.TryParse<SynchronizationMethod>(browser.Session.FormVariables["syncMethodPhriction"], out newPhrictionSynchronizationMethod) == false)
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
                    
                    
                    existingAccount.Parameters.RemovalPeriodClosedManiphests = (RemovalPeriod)Enum.Parse(typeof(RemovalPeriod), (string)browser.Session.FormVariables["removalPeriodClosedManiphests"]);

                    try
                    {
                        existingAccount.Parameters.AutoLogOutAfterMinutesOfInactivity = Int32.Parse(browser.Session.FormVariables["autoLogOutAfterMinutesOfInactivity"].ToString());
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

                    existingAccount.Parameters.DefaultStateModifiedManiphest = (DefaultStateModification)Enum.Parse(typeof(DefaultStateModification), (string)browser.Session.FormVariables["defaultStateModifiedManiphest"]);
                    existingAccount.Parameters.DefaultStateModifiedPhriction = (DefaultStateModification)Enum.Parse(typeof(DefaultStateModification), (string)browser.Session.FormVariables["defaultStateModifiedPhriction"]);

                    existingAccount.Parameters.ShowPhrictionMetadata = bool.Parse((string)browser.Session.FormVariables["showPhrictionMetadata"]);
                    existingAccount.Parameters.ForceDownloadAllPhrictionMetadata = bool.Parse((string)browser.Session.FormVariables["forceDownloadAllPhrictionMetadata"]);

                    existingAccount.Theme = browser.Session.FormVariables["theme"];

                    existingAccount.Parameters.DarkenBrightImages = (DarkenImageStyle)Enum.Parse(typeof(DarkenImageStyle), (string)browser.Session.FormVariables["darkenImages"]);

                    existingAccount.Parameters.ClipboardCopyForCodeBlock = bool.Parse((string)browser.Session.FormVariables["clipboardCopyForCodeBlock"]);
                    existingAccount.Parameters.UITranslation = bool.Parse((string)browser.Session.FormVariables["uiTranslation"]);

                    if (bool.Parse((string)browser.Session.FormVariables["autoLogon"]))
                    {
                        database.SetConfigurationParameter("EncryptionKey", database.EncryptionKey);
                    }
                    else
                    {
                        database.SetConfigurationParameter("EncryptionKey", null);
                    }

                    accountStorage.Set(database, existingAccount);

                    Http.Server.InvalidateNonStaticCache(database, DateTime.MaxValue);
                }
            }
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
            Storage.Account accountStorage = new Storage.Account();

            SessionManager.Token token = SessionManager.GetToken(browser);

            using (Storage.Database database = new Storage.Database(null))
            {
                UInt64[] publicXorCipher = accountStorage.GetPublicXorCipher(database, token);

                // unmask encryption key
                EncryptionKey = Encryption.XorString(EncryptionKey, publicXorCipher);
            }

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/configure", "You don't have sufficient rights to configure Phabrico");

                // unmask private encryption key
                UInt64[] privateXorCipher = accountStorage.GetPrivateXorCipher(database, token);
                database.PrivateEncryptionKey = Encryption.XorString(token.PrivateEncryptionKey, privateXorCipher);

                Account existingAccount = accountStorage.Get(database, token);
                if (existingAccount != null)
                {
                    string jsonArrayConfidentialTableHeaders = browser.Session.FormVariables["data"];
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
    }
}