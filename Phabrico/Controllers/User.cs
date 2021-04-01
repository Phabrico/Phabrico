using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

using Phabrico.Http;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;

namespace Phabrico.Controllers
{
    /// <summary>
    /// Represents the controller being used for the user-functionality in Phabrico
    /// </summary>
    public class User : Controller
    {
        /// <summary>
        /// Model for table rows in the client backend
        /// </summary>
        public class JsonRecordData
        {
            /// <summary>
            /// Token of the user
            /// </summary>
            public string Token { get; set; }

            /// <summary>
            /// Internal username
            /// </summary>
            public string UserName { get; set; }

            /// <summary>
            /// Readable name of user
            /// </summary>
            public string RealName { get; set; }

            /// <summary>
            /// Synchronization selection mode
            /// </summary>
            public bool Selected { get; set; }
        }

        /// <summary>
        /// This method is fired when opening the Users screen
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="htmlViewPage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        /// <returns></returns>
        [UrlController(URL = "/user")]
        public Http.Response.HttpMessage HttpPostLoadUsersScreen(Http.Server httpServer, Browser browser, string[] parameters)
        {
            using (Storage.Database database = new Storage.Database(null))
            {
                SessionManager.Token token = SessionManager.GetToken(browser);
                Storage.Account accountStorage = new Storage.Account();
                if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/user", "You don't have sufficient rights to configure Phabrico");

                UInt64[] publicXorCipher = accountStorage.GetPublicXorCipher(database, token);

                // unmask encryption key
                EncryptionKey = Encryption.XorString(EncryptionKey, publicXorCipher);
            }

            Storage.User userStorage = new Storage.User();
            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // execute "(Un)Select All Users" button actions
                string firstParameter = parameters.FirstOrDefault() ?? "";
                bool doSelectAll = firstParameter.Equals("selectAll");
                bool doUnselectAll = firstParameter.Equals("unselectAll");
                bool doShowSelected = firstParameter.Equals("showSelected");

                string[] filtersUser = browser.Session.FormVariables["filterUser"]
                                                            .Split(' ');
                IEnumerable<Phabricator.Data.User> users = userStorage.Get(database)
                                                                      .Where(user => filtersUser.All(filter => user.RealName
                                                                                                                   .Split(' ', '-')
                                                                                                                   .Any(name => name.StartsWith(filter, System.StringComparison.OrdinalIgnoreCase)))
                                                                            );

                if (doSelectAll)
                {
                    foreach (Phabricator.Data.User user in users)
                    {
                        userStorage.SelectUser(database, user.Token, true);
                    }
                }
                else
                if (doUnselectAll)
                {
                    foreach (Phabricator.Data.User user in users)
                    {
                        userStorage.SelectUser(database, user.Token, false);
                    }
                }
            }

            return null;
        }
        
        /// <summary>
        /// This method is fired when opening the Users screen and when changing its search filter
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/user/query")]
        public JsonMessage HttpPostPopulateTableData(Http.Server httpServer, Browser browser, string[] parameters)
        {
            List<JsonRecordData> tableRows = new List<JsonRecordData>();

            using (Storage.Database database = new Storage.Database(null))
            {
                Storage.Account accountStorage = new Storage.Account();
                SessionManager.Token token = SessionManager.GetToken(browser);
                if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/user/query", "You don't have sufficient rights to configure Phabrico");

                UInt64[] publicXorCipher = accountStorage.GetPublicXorCipher(database, token);

                // unmask encryption key
                EncryptionKey = Encryption.XorString(EncryptionKey, publicXorCipher);
            }

            int totalNumberSelected;
            bool noneUserSelected;
            Storage.User userStorage = new Storage.User();
            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                IEnumerable<Phabricator.Data.User> users = userStorage.Get(database).OrderBy(user => user.RealName);
                totalNumberSelected = users.Count(user => user.Selected);
                noneUserSelected = users.Any(user => user.Selected && user.Token.Equals(Phabricator.Data.User.None));

                if (parameters.Any())
                {
                    string[] filtersUser = System.Web.HttpUtility.UrlDecode(parameters[0]).Split(' ');
                    users = users.Where(user => filtersUser.All(filter => user.RealName
                                                                              .Split(' ')
                                                                              .Any(name => name.StartsWith(filter, System.StringComparison.OrdinalIgnoreCase))
                                                               )
                                       );
                }

                bool showSelectedUsersOnly = browser.Session.FormVariables.ContainsKey("showusers") 
                                           && browser.Session.FormVariables["showusers"].Equals("selected");

                foreach (Phabricator.Data.User userData in users)
                {
                    if (showSelectedUsersOnly && userData.Selected == false)
                    {
                        continue;
                    }

                    JsonRecordData record = new JsonRecordData();

                    record.Token = userData.Token;
                    record.UserName = userData.UserName;
                    record.RealName = userData.RealName;
                    record.Selected = userData.Selected;

                    tableRows.Add(record);
                }

            }

            string jsonData = JsonConvert.SerializeObject(new
            {
                nbrSelected = totalNumberSelected,
                noneUserSelected = noneUserSelected ? "true" : "false",
                records = tableRows
            });
            return new JsonMessage(jsonData);
        }

        /// <summary>
        /// This method is fired when the user clicks on a 'select' button
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="parameters"></param>
        [UrlController(URL = "/user/select")]
        public void HttpPostSelectUser(Http.Server httpServer, Browser browser, string[] parameters)
        {
            using (Storage.Database database = new Storage.Database(null))
            {
                Storage.Account accountStorage = new Storage.Account();
                SessionManager.Token token = SessionManager.GetToken(browser);
                if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/user/select", "You don't have sufficient rights to configure Phabrico");

                UInt64[] publicXorCipher = accountStorage.GetPublicXorCipher(database, token);

                // unmask encryption key
                EncryptionKey = Encryption.XorString(EncryptionKey, publicXorCipher);
            }

            string userToken = browser.Session.FormVariables["item"];
            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                Storage.User userStorage = new Storage.User();
                userStorage.SelectUser(database, userToken, true);
            }
        }

        /// <summary>
        /// This method is fired when the user clicks on a 'unselect' button
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="parameters"></param>
        [UrlController(URL = "/user/unselect")]
        public void HttpPostUnselectUser(Http.Server httpServer, Browser browser, string[] parameters)
        {
            using (Storage.Database database = new Storage.Database(null))
            {
                Storage.Account accountStorage = new Storage.Account();
                SessionManager.Token token = SessionManager.GetToken(browser);
                if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/user/unselect", "You don't have sufficient rights to configure Phabrico");

                UInt64[] publicXorCipher = accountStorage.GetPublicXorCipher(database, token);

                // unmask encryption key
                EncryptionKey = Encryption.XorString(EncryptionKey, publicXorCipher);
            }

            string userToken = browser.Session.FormVariables["item"];
            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                Storage.User userStorage = new Storage.User();
                userStorage.SelectUser(database, userToken, false);

                if (userStorage.Get(database).Any(user => user.Selected) == false)
                {
                    // no users selected -> select 'None' user instead (otherwise we can't track the difference between 2 synchronizations)
                    userStorage.SelectUser(database, Phabricator.Data.User.None, true);
                }
            }
        }
    }
}