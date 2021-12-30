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
    /// Represents the controller being used for the Project-functionality in Phabrico
    /// </summary>
    public class Project : Controller
    {
        /// <summary>
        /// Model for table rows in the client backend
        /// </summary>
        public class JsonRecordData
        {
            /// <summary>
            /// Color of the project tag
            /// </summary>
            public string Color { get; set; }

            /// <summary>
            /// Token of the Project
            /// </summary>
            public string Token { get; set; }

            /// <summary>
            /// Slug name of the project
            /// </summary>
            public string InternalName { get; set; }

            /// <summary>
            /// Readable name of the project
            /// </summary>
            public string ProjectName { get; set; }

            /// <summary>
            /// Synchronization selection mode
            /// </summary>
            public Phabricator.Data.Project.Selection Selected { get; set; }
        }

        /// <summary>
        /// This method is fired when opening the Projects screen
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="htmlViewPage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        /// <returns></returns>
        [UrlController(URL = "/project")]
        public Http.Response.HttpMessage HttpPostLoadProjectScreen(Http.Server httpServer, Browser browser, string[] parameters)
        {
            if (httpServer.Customization.HideProjects) throw new Phabrico.Exception.HttpNotFound("/project");

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");
            if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/project", "You don't have sufficient rights to configure Phabrico");

            Storage.Project projectStorage = new Storage.Project();
            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // set private encryption key
                database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                // execute "(Un)Select All Projects" button actions
                string firstParameter = parameters.FirstOrDefault() ?? "";
                bool doSelectAll = firstParameter.Equals("selectAll");
                bool doUnselectAll = firstParameter.Equals("unselectAll");
                bool doDisallowAll = firstParameter.Equals("disallowAll");
                bool doShowSelected = firstParameter.Equals("showSelected");
                bool doSetColorForAll = firstParameter.Equals("setColorForAll");

                string[] filtersProject = browser.Session.FormVariables[browser.Request.RawUrl]["filterProject"]
                                                         .Split(' ');
                IEnumerable<Phabricator.Data.Project> projects = projectStorage.Get(database, Language.NotApplicable)
                                                                                .Where(project => filtersProject.All(filter => project.Name
                                                                                                                                        .Split(' ', '-')
                                                                                                                                        .Any(name => name.StartsWith(filter, System.StringComparison.OrdinalIgnoreCase)))
                                                                                        );

                if (doSelectAll)
                {
                    foreach (Phabricator.Data.Project project in projects)
                    {
                        projectStorage.SelectProject(database, browser.Session.Locale, project.Token, Phabricator.Data.Project.Selection.Selected);
                    }
                }
                else
                if (doUnselectAll)
                {
                    foreach (Phabricator.Data.Project project in projects)
                    {
                        projectStorage.SelectProject(database, browser.Session.Locale, project.Token, Phabricator.Data.Project.Selection.Unselected);
                    }
                }
                else
                if (doDisallowAll)
                {
                    foreach (Phabricator.Data.Project project in projects)
                    {
                        if (project.Token.Equals(Phabricator.Data.Project.None)) continue;

                        projectStorage.SelectProject(database, browser.Session.Locale, project.Token, Phabricator.Data.Project.Selection.Disallowed);
                    }
                }
                else
                if (doSetColorForAll)
                {
                    string newColorForAll = browser.Session.FormVariables[browser.Request.RawUrl]["colorForAll"];

                    foreach (Phabricator.Data.Project project in projects.ToArray())
                    {
                        project.Color = newColorForAll;
                        projectStorage.Add(database, project);
                    }

                    Http.Server.InvalidateNonStaticCache(database, DateTime.Now);
                }
            }

            return null;
        }

        /// <summary>
        /// This method is fired when opening the Projects screen and when changing its search filter
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/project/query")]
        public JsonMessage HttpPostPopulateTableData(Http.Server httpServer, Browser browser, string[] parameters)
        {
            if (httpServer.Customization.HideProjects) throw new Phabrico.Exception.HttpNotFound("/project/query");

            List<JsonRecordData> tableRows = new List<JsonRecordData>();

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");
            if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/project/query", "You don't have sufficient rights to configure Phabrico");

            int totalNumberSelected;
            bool noneProjectSelected;
            Storage.Project projectStorage = new Storage.Project();
            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // set private encryption key
                database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                IEnumerable<Phabricator.Data.Project> projects = projectStorage.Get(database, Language.NotApplicable).OrderBy(project => project.Name);
                totalNumberSelected = projects.Count(project => project.Selected == Phabricator.Data.Project.Selection.Selected);
                noneProjectSelected = projects.Any(project => project.Selected == Phabricator.Data.Project.Selection.Selected && project.Token.Equals(Phabricator.Data.Project.None));

                if (parameters.Any())
                {
                    string[] filtersProject = System.Web.HttpUtility.UrlDecode(parameters[0]).Split(' ');
                    projects = projects.Where(project => filtersProject.All(filter => project.Name
                                                                                             .Split(' ', '-')
                                                                                             .Any(name => name.StartsWith(filter, System.StringComparison.OrdinalIgnoreCase))
                                                                           )
                                             );
                }

                // add all project-tag-records to the result
                bool showSelectedProjectsOnly = browser.Session.FormVariables[browser.Request.RawUrl]?.ContainsKey("showprojects") == true
                                              && browser.Session.FormVariables[browser.Request.RawUrl]["showprojects"].Equals("selected");
                foreach (Phabricator.Data.Project projectData in projects)
                {
                    if (showSelectedProjectsOnly && projectData.Selected != Phabricator.Data.Project.Selection.Selected)
                    {
                        continue;
                    }

                    JsonRecordData record = new JsonRecordData();

                    record.Token = projectData.Token;
                    record.ProjectName = projectData.Name;
                    record.Color = projectData.Color;
                    record.InternalName = projectData.InternalName;
                    record.Selected = projectData.Selected;

                    tableRows.Add(record);
                }
            }

            string jsonData = JsonConvert.SerializeObject(new
            {
                nbrSelected = totalNumberSelected,
                noneProjectSelected = noneProjectSelected ? "true" : "false",
                fontAwesomeIcon = "fa-briefcase",
                records = tableRows
            });
            return new JsonMessage(jsonData);
        }

        /// <summary>
        /// This method is fired when the user clicks on a 'Disallow' button
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="parameters"></param>
        [UrlController(URL = "/project/disallow")]
        public void HttpPostDisallowProject(Http.Server httpServer, Browser browser, string[] parameters)
        {
            if (httpServer.Customization.HideProjects) throw new Phabrico.Exception.HttpNotFound("/project/disallow");

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");
            if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/project/disallow", "You don't have sufficient rights to configure Phabrico");

            string projectToken = browser.Session.FormVariables[browser.Request.RawUrl]["item"];
            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                Storage.Project projectStorage = new Storage.Project();
                projectStorage.SelectProject(database, browser.Session.Locale, projectToken, Phabricator.Data.Project.Selection.Disallowed);
            }
        }

        /// <summary>
        /// This method is fired when the user clicks on a 'select' button
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="parameters"></param>
        [UrlController(URL = "/project/select")]
        public void HttpPostSelectProject(Http.Server httpServer, Browser browser, string[] parameters)
        {
            if (httpServer.Customization.HideProjects) throw new Phabrico.Exception.HttpNotFound("/project/select");

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");
            if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/project/select", "You don't have sufficient rights to configure Phabrico");

            string projectToken = browser.Session.FormVariables[browser.Request.RawUrl]["item"];
            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                Storage.Project projectStorage = new Storage.Project();
                projectStorage.SelectProject(database, browser.Session.Locale, projectToken, Phabricator.Data.Project.Selection.Selected);
            }
        }

        /// <summary>
        /// This method is fired when the user clicks on a 'unselect' button
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="parameters"></param>
        [UrlController(URL = "/project/unselect")]
        public void HttpPostUnselectProject(Http.Server httpServer, Browser browser, string[] parameters)
        {
            if (httpServer.Customization.HideProjects) throw new Phabrico.Exception.HttpNotFound("/project/unselect");

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");
            if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/project/unselect", "You don't have sufficient rights to configure Phabrico");

            string projectToken = browser.Session.FormVariables[browser.Request.RawUrl]["item"];
            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                Storage.Project projectStorage = new Storage.Project();
                projectStorage.SelectProject(database, browser.Session.Locale, projectToken, Phabricator.Data.Project.Selection.Unselected);

                if (projectStorage.Get(database, browser.Session.Locale).Any(project => project.Selected == Phabricator.Data.Project.Selection.Selected) == false)
                {
                    // no projects selected -> select 'None' project instead (otherwise we can't track the difference between 2 synchronizations)
                    projectStorage.SelectProject(database, browser.Session.Locale, Phabricator.Data.Project.None, Phabricator.Data.Project.Selection.Selected);
                }
            }
        }


        /// <summary>
        /// This method is fired when the user clicks on a SetColor button
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/project/setcolor")]
        public JsonMessage HttpPostSetColor(Http.Server httpServer, Browser browser, string[] parameters)
        {
            if (httpServer.Customization.HideProjects) throw new Phabrico.Exception.HttpNotFound("/project/setcolor");

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");
            if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/project", "You don't have sufficient rights to configure Phabrico");

            try
            {
                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    // set private encryption key
                    database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                    string projectToken = browser.Session.FormVariables[browser.Request.RawUrl]["token"];
                    string projectColor = browser.Session.FormVariables[browser.Request.RawUrl]["color"];

                    Storage.Project projectStorage = new Storage.Project();
                    Phabricator.Data.Project project = projectStorage.Get(database, projectToken, Language.NotApplicable);
                    if (project == null) throw new ArgumentException();

                    project.Color = projectColor;

                    projectStorage.Add(database, project);

                    Http.Server.InvalidateNonStaticCache(database, DateTime.Now);

                    string jsonData = JsonConvert.SerializeObject(new
                    {
                        status = "OK"
                    });
                    return new JsonMessage(jsonData);
                }
            }
            catch
            {
                string jsonData = JsonConvert.SerializeObject(new
                {
                    status = "NOK"
                });
                return new JsonMessage(jsonData);
            }
        }
    }
}
