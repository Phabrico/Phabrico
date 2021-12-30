using Newtonsoft.Json;
using Phabrico.Http;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phabrico.Controllers
{
    /// <summary>
    /// Represents the controller being used for the tags-functionality in Phabrico.
    /// Tags can represent a project or a user (subscriber)
    /// </summary>
    public class Tag : Controller
    {
        /// <summary>
        /// Model for input-tag menu items in client backend via JSON
        /// </summary>
        public class JsonRecordData
        {
            /// <summary>
            /// Token that represents the input-tag item
            /// </summary>
            public string Token { get; set; }

            /// <summary>
            /// Name or description that is shown in the input-tag item
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Fontawesome icon that is shown in the input-tag item
            /// </summary>
            public string FontAwesomeIcon { get; set; }
        }

        /// <summary>
        /// This method is executed in Phriction and Maniphest screens to visualize the subscribers (=users and projects)
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/subscriber/get")]
        public void HttpGetInputTagSubscriberData(Http.Server httpServer, Browser browser, ref JsonMessage jsonMessage, string[] parameters, string parameterActions)
        {
            string tokensString;
            List<JsonRecordData> records = new List<JsonRecordData>();
            Dictionary<string, string> parameterValues = parameterActions.Split('&').ToDictionary(key => key.Split('=')[0], value => System.Web.HttpUtility.UrlDecode(value.Split('=')[1]));
            if (parameterValues.TryGetValue("tokens", out tokensString))
            {
                string[] tokens = tokensString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                Storage.Project projectStorage = new Storage.Project();
                Storage.User userStorage = new Storage.User();

                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    foreach (string token in tokens)
                    {
                        if (httpServer.Customization.HideUsers == false)
                        {
                            Phabricator.Data.User user = userStorage.Get(database, token, browser.Session.Locale);
                            if (user != null)
                            {
                                records.Add(new JsonRecordData { Token = user.Token, Name = user.RealName, FontAwesomeIcon = "fa-user" });
                                continue;
                            }
                        }

                        if (httpServer.Customization.HideProjects == false)
                        {
                            Phabricator.Data.Project project = projectStorage.Get(database, token, browser.Session.Locale);
                            if (project != null)
                            {
                                records.Add(new JsonRecordData { Token = project.Token, Name = project.Name, FontAwesomeIcon = "fa-briefcase" });
                                continue;
                            }
                        }
                    }
                }
            }

            string jsonData = JsonConvert.SerializeObject(records);
            jsonMessage = new JsonMessage(jsonData);
        }

        /// <summary>
        /// This method is executed when a user is entering text in a Subscriber input field.
        /// The entered text is used as filter on all available users and projects that can be used as subscriber.
        /// A JSONified array of these filtered users and projects is returned
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/subscriber/search")]
        public void HttpGetPopulateInputTagSubscriberData(Http.Server httpServer, Browser browser, ref JsonMessage jsonMessage, string[] parameters, string parameterActions)
        {
            Dictionary<string, string> parameterValues = parameterActions.Split('&').ToDictionary(key => key.Split('=')[0], value => value.Split('=')[1]);
            Storage.Project projectStorage = new Storage.Project();
            Storage.User userStorage = new Storage.User();
            List<Phabricator.Data.Project> projects = new List<Phabricator.Data.Project>();
            List<Phabricator.Data.User> users = new List<Phabricator.Data.User>();
            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                if (httpServer.Customization.HideUsers == false)
                {
                    users = userStorage.Get(database, Language.NotApplicable)
                                       .Where(user => System.Web.HttpUtility.UrlDecode(parameterValues["keyword"])
                                                                            .Split(' ')
                                                                            .All(part => user.RealName
                                                                                             .Split(' ')
                                                                                             .Any(keywordPart => keywordPart.StartsWith(part, System.StringComparison.OrdinalIgnoreCase))))
                                       .Where(token => parameterValues.ContainsKey("tags") == false ||
                                                       parameterValues["tags"].Contains(token.Token) == false)
                                       .Take(5)
                                       .ToList();
                }

                if (httpServer.Customization.HideUsers == false)
                {
                    projects = projectStorage.Get(database, Language.NotApplicable)
                                             .Where(project => project.Name.Split(' ', '-', '_').All(part => System.Web.HttpUtility.UrlDecode(parameterValues["keyword"]).Split(' ', '-', '_').Any(keywordPart => part.StartsWith(keywordPart, System.StringComparison.OrdinalIgnoreCase))))
                                             .Where(token => parameterValues.ContainsKey("tags") == false ||
                                                             parameterValues["tags"].Contains(token.Token) == false)
                                             .Take(5)
                                             .ToList();
                }
            }

            List<JsonRecordData> records = new List<JsonRecordData>();
            foreach (Phabricator.Data.User user in users.Take(10))
            {
                JsonRecordData newRecordData = new JsonRecordData();
                newRecordData.Token = user.Token;
                newRecordData.Name = user.RealName;
                newRecordData.FontAwesomeIcon = "fa-user";
                records.Add(newRecordData);
            }
            foreach (Phabricator.Data.Project project in projects.Take(10))
            {
                JsonRecordData newRecordData = new JsonRecordData();
                newRecordData.Token = project.Token;
                newRecordData.Name = project.Name;
                newRecordData.FontAwesomeIcon = "fa-briefcase";
                records.Add(newRecordData);
            }

            string jsonData = JsonConvert.SerializeObject(records.Take(10).OrderBy(r => r.Name.ToUpper()));
            jsonMessage = new JsonMessage(jsonData);
        }

        /// <summary>
        /// This method is executed in Phriction and Maniphest screens to visualize the projects
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/tag/get")]
        public void HttpGetInputTagTagData(Http.Server httpServer, Browser browser, ref JsonMessage jsonMessage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.HideProjects) throw new Phabrico.Exception.HttpNotFound("/tag/get");

            string tokensString;
            List<JsonRecordData> records = new List<JsonRecordData>();
            Dictionary<string, string> parameterValues = parameterActions.Split('&').ToDictionary(key => key.Split('=')[0], value => System.Web.HttpUtility.UrlDecode(value.Split('=')[1]));
            if (parameterValues.TryGetValue("tokens", out tokensString))
            {
                string[] tokens = tokensString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                Storage.Project projectStorage = new Storage.Project();

                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    foreach (string token in tokens)
                    {
                        Phabricator.Data.Project project = projectStorage.Get(database, token, browser.Session.Locale);
                        if (project != null)
                        {
                            records.Add(new JsonRecordData { Token = project.Token, Name = project.Name, FontAwesomeIcon = "fa-briefcase" });
                            continue;
                        }
                    }
                }
            }

            string jsonData = JsonConvert.SerializeObject(records);
            jsonMessage = new JsonMessage(jsonData);
        }

        /// <summary>
        /// This method is executed when a user is entering text in a Project input field.
        /// The entered text is used as filter on all available projects.
        /// A JSONified array of these filtered projects is returned
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/tag/search")]
        public void HttpGetPopulateInputTagTagData(Http.Server httpServer, Browser browser, ref JsonMessage jsonMessage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.HideProjects) throw new Phabrico.Exception.HttpNotFound("/tag/search");

            Dictionary<string,string> parameterValues = parameterActions.Split('&').ToDictionary(key => key.Split('=')[0], value => value.Split('=')[1]);
            Storage.Project projectStorage = new Storage.Project();
            List<Phabricator.Data.Project> projects;
            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                projects = projectStorage.Get(database, Language.NotApplicable)
                                         .Where(keyword => System.Web.HttpUtility.UrlDecode(parameterValues["keyword"])
                                                                                 .Split(' ', '-', '_')
                                                                                 .All(keywordPart => keyword.Name
                                                                                                            .Split(' ', '-', '_')
                                                                                                            .Any(project => project.StartsWith(keywordPart, System.StringComparison.OrdinalIgnoreCase))))
                                         .Where(project => parameterValues.ContainsKey("tags") == false ||
                                                           parameterValues["tags"].Contains(project.Token) == false)
                                         .Where(project => httpServer.ValidUserRoles(database, browser, project))
                                         .Take(5)
                                         .ToList();
            }

            List<JsonRecordData> records = new List<JsonRecordData>();
            foreach (Phabricator.Data.Project project in projects)
            {
                JsonRecordData newRecordData = new JsonRecordData();
                newRecordData.Token = project.Token;
                newRecordData.Name = project.Name;
                newRecordData.FontAwesomeIcon = "fa-briefcase";
                records.Add(newRecordData);
            }

            string jsonData = JsonConvert.SerializeObject(records);
            jsonMessage = new JsonMessage(jsonData);
        }

        /// <summary>
        /// This method is executed in Phriction and Maniphest screens to visualize the users
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/user/get")]
        public void HttpGetInputTagUserData(Http.Server httpServer, Browser browser, ref JsonMessage jsonMessage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.HideUsers) throw new Phabrico.Exception.HttpNotFound("/user/get");

            string tokensString;
            List<JsonRecordData> records = new List<JsonRecordData>();
            Dictionary<string, string> parameterValues = parameterActions.Split('&')
                                                                         .ToDictionary( key => key.Split('=')[0], 
                                                                                        value => System.Web.HttpUtility.UrlDecode(value.Split('=')[1])
                                                                                      );
            if (parameterValues.TryGetValue("tokens", out tokensString))
            {
                string[] tokens = tokensString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                Storage.User userStorage = new Storage.User();

                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    foreach (string token in tokens)
                    {
                        Phabricator.Data.User user = userStorage.Get(database, token, browser.Session.Locale);
                        if (user != null)
                        {
                            records.Add(new JsonRecordData { Token = user.Token, Name = user.RealName, FontAwesomeIcon = "fa-user" });
                            continue;
                        }
                    }
                }
            }

            string jsonData = JsonConvert.SerializeObject(records);
            jsonMessage = new JsonMessage(jsonData);
        }

        /// <summary>
        /// This method is executed when a user is entering text in a User input field.
        /// The entered text is used as filter on all available users.
        /// A JSONified array of these filtered users is returned
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/user/search")]
        public void HttpGetPopulateInputTagUserData(Http.Server httpServer, Browser browser, ref JsonMessage jsonMessage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.HideUsers) throw new Phabrico.Exception.HttpNotFound("/user/search");

            Dictionary<string, string> parameterValues = parameterActions.Split('&').ToDictionary(key => key.Split('=')[0], value => value.Split('=')[1]);
            Storage.User userStorage = new Storage.User();
            List<Phabricator.Data.User> users;
            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                string input = System.Web.HttpUtility.UrlDecode(parameterValues["keyword"]);
                users = userStorage.Get(database, Language.NotApplicable)
                                   .Where(user => user.IsBot == false
                                               && user.IsDisabled == false
                                               && input.Split(' ')
                                                       .All(keywordPart => user.RealName
                                                                        .Split(' ')
                                                                        .Any(part => part.StartsWith(keywordPart, System.StringComparison.OrdinalIgnoreCase))
                                                           )
                                         )
                                   .Take(5)
                                   .ToList();
            }

            List<JsonRecordData> records = new List<JsonRecordData>();
            foreach (Phabricator.Data.User user in users.Take(10))
            {
                JsonRecordData newRecordData = new JsonRecordData();
                newRecordData.Token = user.Token;
                newRecordData.Name = user.RealName;
                newRecordData.FontAwesomeIcon = "fa-user";
                records.Add(newRecordData);
            }

            string jsonData = JsonConvert.SerializeObject(records.Take(10).OrderBy(r => r.Name.ToUpper()));
            jsonMessage = new JsonMessage(jsonData);
        }
    }
}
