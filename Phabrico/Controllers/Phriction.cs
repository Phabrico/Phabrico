﻿using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Data.References;
using Phabrico.Http;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;
using Phabrico.Parsers.Remarkup;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace Phabrico.Controllers
{
    /// <summary>
    /// Represents the controller being used for the Phriction-functionality in Phabrico
    /// </summary>
    public class Phriction : Controller
    {
        /// <summary>
        /// Subclass for visualizing subscribers to a Phriction document (in read and edit mode)
        /// </summary>
        public class Subscriber
        {
            /// <summary>
            /// User token of subscriber (=also part of "/user/info" URL)
            /// </summary>
            public string Token { get; set; }

            /// <summary>
            /// Readable name of subscriber
            /// </summary>
            public string Name { get; set; }
        }

        /// <summary>
        /// internal static dictionary for speeding up the GenerateCrumbs method
        /// </summary>
        private static object lockCachedCrumbTranslations = new object();
        private static TransientDictionary<string, Tuple<Phabricator.Data.Phriction, Content.Translation>> cachedCrumbTranslations = new TransientDictionary<string, Tuple<Phabricator.Data.Phriction, Content.Translation>>(TimeSpan.FromSeconds(2), false);

        /// <summary>
        /// Location of root Phriction document (=homepage of Phriction)
        /// </summary>
        private static string rootDocumentPath = null;

        /// <summary>
        /// Creates a path for a new Phriction document
        /// </summary>
        /// <param name="parentPath"></param>
        /// <param name="documentTitle"></param>
        /// <returns></returns>
        private string FormatPhabricatorSlug(string parentPath, string documentTitle)
        {
            return string.Format("{0}{1}/", parentPath.TrimStart('/'), documentTitle.TrimEnd('/'));
        }

        /// <summary>
        /// Creates the breadcrumb navigation on top
        /// </summary>
        /// <param name="database"></param>
        /// <param name="phrictionDocument"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public static string GenerateCrumbs(Database database, Phabricator.Data.Phriction phrictionDocument, Language language)
        {
            string completeCrumb = "";
            List<JObject> crumbs = new List<JObject>();
            Storage.Stage stageStorage = new Storage.Stage();
            Storage.Phriction phrictionStorage = new Storage.Phriction();
            string[] urlParts = phrictionDocument.Path.Split('?');
            string url = urlParts.FirstOrDefault();
            Content content = new Content(database);
            Tuple<Phabricator.Data.Phriction, Content.Translation> crumbTranslation = null;

                if (rootDocumentPath == null)
                {
                    rootDocumentPath = phrictionStorage.Get(database, "/", language)
                                                       ?.Path
                                                       ?.TrimEnd('/');
                    if (rootDocumentPath == null)
                    {
                        rootDocumentPath = phrictionStorage.Get(database, language)
                                                           .OrderBy(document => document.Path.Length)
                                                           .FirstOrDefault()
                                                           ?.Path
                                                           ?.TrimEnd('/');
                    }
                }

            foreach (string slug in url.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                bool hiddenCrumb;
                bool documentIsStaged = true;

                completeCrumb += slug + "/";
                hiddenCrumb = rootDocumentPath != null
                            && completeCrumb.StartsWith(rootDocumentPath) == false;

                lock (lockCachedCrumbTranslations)
                {
                    if (cachedCrumbTranslations.TryGetValue(completeCrumb + language, out crumbTranslation) == false)
                    {
                        Content.Translation translation;
                        Phabricator.Data.Phriction crumbPhrictionReference = stageStorage.Get<Phabricator.Data.Phriction>(database, language)
                                                                                         .FirstOrDefault(document => document.Path.Equals(completeCrumb));
                        if (crumbPhrictionReference == null)
                        {
                            documentIsStaged = false;
                            crumbPhrictionReference = phrictionStorage.Get(database, completeCrumb, language);
                        }

                        if (documentIsStaged && crumbPhrictionReference.Language.Equals(language))
                        {
                            // we have a staged translation: keep staged data (=modified translation) instead of (original) translation
                            translation = null;
                        }
                        else
                        if (crumbPhrictionReference?.Token == null)
                        {
                            // this is our first wiki document
                            translation = null;
                        }
                        else
                        {
                            // get translation
                            translation = content.GetTranslation(crumbPhrictionReference.Token, language);
                        }

                        crumbTranslation = new Tuple<Phabricator.Data.Phriction, Content.Translation>(crumbPhrictionReference, translation);

                        cachedCrumbTranslations[completeCrumb + language] = crumbTranslation;
                    }
                }

                crumbs.Add(new JObject
                {
                    new JProperty("slug", hiddenCrumb ? "" : slug),
                    new JProperty("name", crumbTranslation.Item2?.TranslatedTitle
                                          ?? crumbTranslation.Item1?.Name
                                          ?? ConvertPhabricatorUrlPartToDescription(slug)
                                 ),
                    new JProperty("inexistant", crumbTranslation.Item1 == null),
                    new JProperty("hidden", hiddenCrumb)
                });
            }

            if (urlParts.Count() > 1)
            {
                Dictionary<string, string> arguments = string.Join("?", urlParts.Skip(1))
                                                            .Split('&')
                                                            .ToDictionary(arg => arg.Split('=').FirstOrDefault(),
                                                                          arg => HttpUtility.UrlDecode(arg.Split('=').Skip(1).FirstOrDefault() ?? "")
                                                                         );
                string title;
                if (arguments.TryGetValue("title", out title))
                {
                    crumbs.LastOrDefault()["name"] = title;
                }
            }

            return new JArray(crumbs).ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <summary>
        /// This URL is fired when browsing through Phriction documents
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="viewPage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/phriction", Alias = "/w", HtmlViewPageOptions = Http.Response.HtmlViewPage.ContentOptions.HideGlobalTreeView)]
        public void HttpGetLoadParameters(Http.Server httpServer, ref HtmlViewPage viewPage, string[] parameters, string parameterActions)
        {
            if (httpServer.Customization.HidePhriction) throw new Phabrico.Exception.HttpNotFound("/phriction");

            Storage.Phriction phrictionStorage = new Storage.Phriction();
            Storage.Project projectStorage = new Storage.Project();
            Storage.Stage stageStorage = new Storage.Stage();
            Storage.User userStorage = new Storage.User();
            Storage.Account accountStorage = new Storage.Account();
            string subscriberTokens = "";
            string projectTokens = "";
            Content.Translation translation = null;

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                bool editMode = false;
                bool documentIsStaged = false;
                string documentState = "";
                string unreviewedTranslation = "";
                string url = string.Join("/", parameters.TakeWhile(parameter => parameter.StartsWith("?action=") == false));
                url = url.Split(new string[] { "?" }, StringSplitOptions.None).FirstOrDefault();
                url = url.TrimEnd('/') + "/";

                if (rootDocumentPath == null)
                {
                    rootDocumentPath = phrictionStorage.Get(database, "/", browser.Session.Locale)
                                                       ?.Path
                                                       ?.TrimEnd('/');
                    if (rootDocumentPath == null)
                    {
                        rootDocumentPath = phrictionStorage.Get(database, browser.Session.Locale)
                                                           .OrderBy(document => document.Path.Length)
                                                           .FirstOrDefault()
                                                           ?.Path
                                                           ?.TrimEnd('/');
                    }
                }
                string unaliasedUrl = (rootDocumentPath + "/" + string.Join("/", url.Split('/').Skip(1))).Replace("//", "/");

                // set private encryption key
                database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                Phabricator.Data.Phriction phrictionDocument = null;

                // search for a staged phriction document
                foreach (Phabricator.Data.Phriction stagedPhrictionDocument in stageStorage.Get<Phabricator.Data.Phriction>(database, browser.Session.Locale))
                {
                    if (stagedPhrictionDocument.Path != null &&
                        (stagedPhrictionDocument.Path.Equals(url, StringComparison.OrdinalIgnoreCase) ||
                          stagedPhrictionDocument.Path.Equals(unaliasedUrl, StringComparison.OrdinalIgnoreCase)
                        )
                       )
                    {
                        phrictionDocument = stagedPhrictionDocument;
                        documentIsStaged = true;
                        break;
                    }
                }

                if (phrictionDocument == null)
                {
                    // no staged document found -> search for a downloaded document
                    if (parameters.Any())
                    {
                        phrictionDocument = phrictionStorage.Get(database, HttpUtility.UrlDecode(url), browser.Session.Locale);
                        if (phrictionDocument == null)
                        {
                            phrictionDocument = phrictionStorage.Get(database, HttpUtility.UrlDecode(unaliasedUrl), browser.Session.Locale);
                        }
                    }
                    else
                    {
                        phrictionDocument = phrictionStorage.Get(database, "/", browser.Session.Locale);
                    }

                    if (phrictionDocument == null)
                    {
                        if (parameterActions != null &&
                            parameterActions.StartsWith("action=new"))
                        {
                            documentState = "modified";

                            phrictionDocument = new Phabricator.Data.Phriction();
                            if (parameterActions.Contains("&title="))
                            {
                                if (parameters.FirstOrDefault().StartsWith("?action=new"))
                                {
                                    // new root page
                                    phrictionDocument.Path = "/";
                                }
                                else
                                {
                                    // document was created by means of a link: title is already known
                                    phrictionDocument.Name = parameters[parameters.Length - 2];
                                    phrictionDocument.Path = string.Join( "/",
                                                                          parameters.Take(parameters.Length - 2)
                                                                                    .Select(pathPart => HttpUtility.UrlDecode(pathPart))
                                                                        )
                                                           + "/";
                                }
                            }
                            else
                            {
                                // should not happen, but if it does: new root page
                                phrictionDocument.Path = "/";
                            }
                        }
                        else
                        {
                            if (httpServer.Customization.IsReadonly)
                            {
                                throw new Phabrico.Exception.HttpNotFound("/phriction");
                            }
                            else
                            {
                                phrictionDocument = new Phabricator.Data.Phriction();
                                phrictionDocument.Path = HttpUtility.UrlDecode(url);
                                if (parameterActions != null && parameterActions.StartsWith("title="))
                                {
                                    phrictionDocument.Path += "?" + parameterActions;
                                }

                                viewPage = new Http.Response.HtmlViewPage(httpServer, browser, true, "PhrictionNoDocumentFound", parameters);
                                viewPage.SetText("OPERATION", "new");
                                viewPage.SetText("DOCUMENT-CRUMBS", GenerateCrumbs(database, phrictionDocument, browser.Session.Locale), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue | HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                                viewPage.HttpStatusCode = 202;  // send notification to browser that document doesn't exist
                                viewPage.HttpStatusMessage = "No Content";
                            }
                            return;
                        }
                    }
                }
                else
                {
                    // if document was modified, draw a flame next to the document's title and take the document info from the staging area
                    if (documentIsStaged)
                    {
                        Content content = new Content(database);
                        translation = content.GetTranslation(phrictionDocument.Token, browser.Session.Locale);
                        if (translation != null)
                        {
                            documentState = "translated";
                            if (translation.IsReviewed == false)
                            {
                                unreviewedTranslation = "unreviewed";
                            }
                        }
                        else
                        if (phrictionDocument.Token.StartsWith("PHID-NEWTOKEN-"))
                        {
                            documentState = "created";
                        }
                        else
                        {
                            if (stageStorage.IsFrozen(database, browser, phrictionDocument.Token))
                            {
                                documentState = "frozen";
                            }
                            else
                            {
                                documentState = "unfrozen";
                            }
                        }
                    }
                    else
                    {
                        documentState = "";
                    }
                }

                if (phrictionDocument != null)
                {
                    if (documentIsStaged && phrictionDocument.Language.Equals(browser.Session.Locale))
                    {
                        // we have a staged translation: keep staged data (=modified translation) instead of (original) translation
                        translation = null;
                    }
                    else
                    if (phrictionDocument.Token == null)
                    {
                        // this is our first wiki document
                        translation = null;
                    }
                    else
                    {
                        // get translation
                        Content content = new Content(database);
                        translation = content.GetTranslation(phrictionDocument.Token, browser.Session.Locale);
                    }
                }

                if (httpServer.ValidUserRoles(database, browser, phrictionDocument) == false)
                {
                    throw new Phabrico.Exception.HttpNotFound(phrictionDocument.Path);
                }

                if (phrictionDocument?.Path != null && rootDocumentPath != null && phrictionDocument.Path.StartsWith(rootDocumentPath))
                {
                    phrictionDocument.Path = phrictionDocument.Path.Substring(rootDocumentPath.Length);
                }

                RemarkupParserOutput remarkupParserOutput;
                string formattedDocumentContent;

                string action = "";
                if (parameterActions != null &&
                    parameterActions.StartsWith("action="))
                {
                    action = parameterActions.Substring("action=".Length).ToLower().Split('&')[0];
                    switch (action)
                    {
                        case "undo":
                            documentState = "";
                            formattedDocumentContent = ConvertRemarkupToHTML(database, phrictionDocument.Path, phrictionDocument.Content, out remarkupParserOutput, false, phrictionDocument.Token);
                            break;

                        case "edit":
                        case "translate":
                            if (action.Equals("translate") == false)
                            {
                                documentState = "";
                            }
                            editMode = true;
                            viewPage = new Http.Response.HtmlViewPage(httpServer, browser, true, "PhrictionEdit", parameters);
                            viewPage.SetText("OPERATION", action);
                            if (translation == null)
                            {
                                formattedDocumentContent = ConvertRemarkupToHTML(database, phrictionDocument.Path, phrictionDocument.Content, out remarkupParserOutput, true, phrictionDocument.Token);
                            }
                            else
                            {
                                formattedDocumentContent = ConvertRemarkupToHTML(database, phrictionDocument.Path, translation.TranslatedRemarkup, out remarkupParserOutput, true, phrictionDocument.Token);
                            }
                            break;

                        case "new":
                            string title = parameterActions.Substring("action=".Length + action.Length).TrimStart('&');
                            if (title.StartsWith("title="))
                            {
                                if (phrictionDocument.Path.Equals("/"))
                                {
                                    title = "";
                                }
                                else
                                {
                                    title = HttpUtility.UrlDecode(title.Substring("title=".Length));
                                }
                            }
                            else
                            {
                                phrictionDocument.Path += Locale.TranslateText("(New)", browser.Session.Locale);
                            }

                            documentState = "modified";
                            viewPage = new Http.Response.HtmlViewPage(httpServer, browser, true, "PhrictionEdit", parameters);
                            viewPage.SetText("OPERATION", "new");
                            phrictionDocument.DateModified = DateTimeOffset.UtcNow;
                            phrictionDocument.Name = title;
                            phrictionDocument.Content = "";
                            formattedDocumentContent = "";
                            break;

                        case "subscribe":
                            documentState = "";
                            formattedDocumentContent = ConvertRemarkupToHTML(database, phrictionDocument.Path, phrictionDocument.Content, out remarkupParserOutput, false, phrictionDocument.Token);
                            break;

                        default:
                            if (translation == null)
                            {
                                formattedDocumentContent = ConvertRemarkupToHTML(database, phrictionDocument.Path, phrictionDocument.Content, out remarkupParserOutput, false, phrictionDocument.Token);
                            }
                            else
                            {
                                formattedDocumentContent = ConvertRemarkupToHTML(database, phrictionDocument.Path, translation.TranslatedRemarkup, out remarkupParserOutput, false, phrictionDocument.Token);
                            }
                            break;
                    }
                }
                else
                {
                    if (translation == null)
                    {
                        formattedDocumentContent = ConvertRemarkupToHTML(database, phrictionDocument.Path, phrictionDocument.Content, out remarkupParserOutput, false, phrictionDocument.Token);
                    }
                    else
                    {
                        formattedDocumentContent = ConvertRemarkupToHTML(database, phrictionDocument.Path, translation.TranslatedRemarkup, out remarkupParserOutput, false, phrictionDocument.Token);

                        documentState = "translated";
                        if (translation.IsReviewed == false)
                        {
                            unreviewedTranslation = "unreviewed";
                        }
                    }
                }

                if (phrictionDocument.Token != null && 
                    action.Equals("new") == false && 
                    action.Equals("edit") == false &&
                    action.Equals("translate") == false
                   )
                {
                    int depthLevel = string.IsNullOrWhiteSpace(phrictionDocument.Content) ? 3 : 2;
                    PhrictionDocumentTree documentHierarchy = phrictionStorage.GetHierarchy(database, browser, phrictionDocument.Token, depthLevel);
                    if (documentHierarchy.Any())
                    {
                        Http.Response.HtmlViewPage documentHierarchyViewPage = new Http.Response.HtmlViewPage(httpServer, browser, true, "PhrictionHierarchy", parameters);
                        documentHierarchyViewPage.SetText("TREE-CONTENT", documentHierarchy.ToHTML(rootDocumentPath), HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        viewPage.SetText("DOCUMENT-HIERARCHY", documentHierarchyViewPage.Content, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue | HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                    }
                    else
                    {
                        viewPage.SetText("DOCUMENT-HIERARCHY", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    }
                }

                Phabricator.Data.Account currentAccount = accountStorage.WhoAmI(database, browser);

                if (translation == null)
                {
                    viewPage.SetText("DOCUMENT-TITLE", phrictionDocument.Name, editMode ? HtmlViewPage.ArgumentOptions.Default : HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                }
                else
                {
                    viewPage.SetText("DOCUMENT-TITLE", translation.TranslatedTitle, editMode ? HtmlViewPage.ArgumentOptions.Default : HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                }

                viewPage.SetText("DOCUMENT-TOKEN", phrictionDocument.Token, editMode ? HtmlViewPage.ArgumentOptions.Default : HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                viewPage.SetText("DOCUMENT-PATH", phrictionDocument.Path, HtmlViewPage.ArgumentOptions.NoHtmlEncoding | HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                viewPage.SetText("DOCUMENT-CONTENT", formattedDocumentContent, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue | HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                viewPage.SetText("DOCUMENT-CRUMBS", GenerateCrumbs(database, phrictionDocument, browser.Session.Locale), HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                viewPage.SetText("PHABRICATOR-URL", currentAccount.PhabricatorUrl.TrimEnd('/') + "/", HtmlViewPage.ArgumentOptions.NoHtmlEncoding);

                if (phrictionDocument.Token != null && phrictionDocument.Token.StartsWith(Phabricator.Data.Phriction.PrefixCoverPage))
                {
                    viewPage.SetText("IS-COVERPAGE", "yes");
                    viewPage.SetText("SHOW-SIDE-WINDOW", "no");
                    viewPage.SetText("HIDE-NEW-DOCUMENT-ACTION", "yes");
                    viewPage.SetText("HIDE-PHRICTION-IN-CRUMBS", "True");
                }
                else
                {
                    viewPage.SetText("IS-COVERPAGE", "no");
                    viewPage.SetText("SHOW-SIDE-WINDOW", "yes");
                    viewPage.SetText("HIDE-NEW-DOCUMENT-ACTION", "no");
                    viewPage.SetText("DOCUMENT-STATE", documentState, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    viewPage.SetText("UNREVIEWED-TRANSLATION", unreviewedTranslation, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    viewPage.SetText("DOCUMENT-TIMESTAMP", phrictionDocument.DateModified.ToUnixTimeSeconds().ToString(), editMode ? HtmlViewPage.ArgumentOptions.Default : HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    viewPage.SetText("DOCUMENT-DATE", FormatDateTimeOffset(phrictionDocument.DateModified, browser.Session.Locale ?? browser.Properties.Language), HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                    viewPage.SetText("DOCUMENT-LAST-MODIFIED-BY", getAccountName(phrictionDocument.LastModifiedBy), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);

                    if (translation == null)
                    {
                        viewPage.SetText("DOCUMENT-RAW-CONTENT", phrictionDocument.Content, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    }
                    else
                    {
                        viewPage.SetText("DOCUMENT-RAW-CONTENT", translation.TranslatedRemarkup, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    }

                    // verify if only Phriction should be visible
                    if ((httpServer.Customization.HideConfig &&
                         httpServer.Customization.HideFiles &&
                         httpServer.Customization.HideManiphest &&
                         httpServer.Customization.HideOfflineChanges &&
                         httpServer.Customization.HideProjects &&
                         httpServer.Customization.HideUsers &&
                         httpServer.Customization.HidePhriction == false &&
                         Http.Server.Plugins.All(plugin => plugin.IsVisibleInNavigator(browser) == false
                                                        || (browser.HttpServer.Customization.HidePlugins.ContainsKey(plugin.GetType().Name)
                                                            && browser.HttpServer.Customization.HidePlugins[plugin.GetType().Name] == true
                                                           )
                                                )
                        )
                        || 
                        (rootDocumentPath != null &&
                         rootDocumentPath.Equals("/") == false
                        )
                       )
                    {
                        viewPage.SetText("HIDE-PHRICTION-IN-CRUMBS", "True", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    }
                    else
                    {
                        viewPage.SetText("HIDE-PHRICTION-IN-CRUMBS", "False", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    }


                    if (phrictionStorage.IsFavorite(database, phrictionDocument, currentAccount.UserName))
                    {
                        viewPage.SetText("IS-MEMBER-OF-FAVORITES", "yes");
                    }
                    else
                    {
                        viewPage.SetText("IS-MEMBER-OF-FAVORITES", "no");
                    }

                    if (phrictionStorage.IsHiddenFromSearchResults(database, browser, phrictionDocument.Path, currentAccount.UserName))
                    {
                        viewPage.SetText("IS-HIDDEN-BY-SEARCH-FILTER", "yes");
                    }
                    else
                    {
                        viewPage.SetText("IS-HIDDEN-BY-SEARCH-FILTER", "no");
                    }

                    if (phrictionDocument.Token != null && database.GetDependentObjects(phrictionDocument.Token, browser.Session.Locale).Any())
                    {
                        viewPage.SetText("HAS-REFERENCES", "yes");
                    }
                    else
                    {
                        viewPage.SetText("HAS-REFERENCES", "no");
                    }

                    if (currentAccount.Parameters.AutoClosePhrictionAppSideWindow)
                    {
                        viewPage.SetText("AUTO-CLOSE-PHRICTION-APPSIDE-WINDOW", "yes");
                    }
                    else
                    {
                        viewPage.SetText("AUTO-CLOSE-PHRICTION-APPSIDE-WINDOW", "no");
                    }

                    if (currentAccount.Parameters.ShowPhrictionMetadata)
                    {
                        viewPage.SetText("SHOW-PHRICTION-METADATA", "yes");
                    }
                    else
                    {
                        viewPage.SetText("SHOW-PHRICTION-METADATA", "no");
                    }

                    // add Diagram icon to toolbar if DiagramsNet plugin is installed
                    if (Http.Server.Plugins.Any(plugin => plugin.GetType().FullName.Equals("Phabrico.Plugin.DiagramsNet")))
                    {
                        viewPage.SetText("PLUGIN-DIAGRAM-AVAILABLE", "yes", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    }
                    else
                    {
                        viewPage.SetText("PLUGIN-DIAGRAM-AVAILABLE", "no", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    }

                    if (documentState == "created")
                    {
                        viewPage.SetText("DOCUMENT-CONFIRMATION-UNDO-LOCAL-CHANGES", "Are you sure you want to delete this document and all underlying documents ?");
                    }
                    else
                    {
                        viewPage.SetText("DOCUMENT-CONFIRMATION-UNDO-LOCAL-CHANGES", "Are you sure you want to undo all your local changes for this document ?");
                    }

                    if (phrictionDocument.Token != null) // if first wiki-document -> no translation
                    {
                        // prepare content translation
                        Content content = new Content(database);
                        int numberUnreviewedTranslations = content.GetUnreviewedTranslations(browser.Session.Locale).Count();
                        translation = content.GetTranslation(phrictionDocument.Token, browser.Session.Locale);
                        if (numberUnreviewedTranslations > 0)
                        {
                            viewPage.SetText("SHOW-ALL-UNREVIEWED-TRANSLATIONS", "yes");
                            viewPage.SetText("NUMBER-UNREVIEWED-TRANSLATIONS", numberUnreviewedTranslations.ToString(), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);

                            if (translation != null && translation.IsReviewed == false)
                            {
                                viewPage.SetText("SHOW-APPROVE-TRANSLATION", "yes");
                            }
                        }
                    }

                    if (translation == null)
                    {
                        viewPage.SetText("CONTENT-IS-TRANSLATION", "no");
                    }
                    else
                    {
                        viewPage.SetText("CONTENT-IS-TRANSLATION", "yes");
                    }
                }

                if (phrictionDocument.Projects != null)
                {
                    List<Phabricator.Data.Project> projects = phrictionDocument.Projects
                                                                               .Split(',')
                                                                               .Where(token => string.IsNullOrEmpty(token) == false)
                                                                               .Select(token => projectStorage.Get(database, token, browser.Session.Locale))
                                                                               .Where(p => p != null)
                                                                               .OrderBy(p => p.Name)
                                                                               .ToList();

                    if (projects.Any())
                    {
                        viewPage.SetText("PROJECTS-ASSIGNED", "yes");
                    }
                    else
                    {
                        viewPage.SetText("PROJECTS-ASSIGNED", "no");
                    }

                    foreach (Phabricator.Data.Project project in projects)
                    {
                        HtmlPartialViewPage projectData = viewPage.GetPartialView("PROJECTS");
                        if (projectData != null)
                        {
                            string rgbColor = "rgb(0, 128, 255)";
                            if (project != null && string.IsNullOrWhiteSpace(project.Color) == false)
                            {
                                rgbColor = project.Color;
                            }

                            string style = string.Format("background: {0}; color: {1}; border-color: {1}",
                                                rgbColor,
                                                ColorFunctionality.WhiteOrBlackTextOnBackground(rgbColor));

                            projectData.SetText("DOCUMENT-PROJECT-TOKEN", project.Token);
                            projectData.SetText("DOCUMENT-PROJECT-STYLE", style);
                            projectData.SetText("DOCUMENT-PROJECT-NAME", project.Name);
                        }

                        projectTokens += "," + project.Token;
                    }
                }
                else
                {
                    viewPage.SetText("PROJECTS-ASSIGNED", "no");
                }

                if (phrictionDocument.Token != null)
                {
                    foreach (Plugin.PluginBase plugin in Server.Plugins)
                    {
                        Plugin.PluginTypeAttribute pluginType = plugin.GetType()
                                                                      .GetCustomAttributes(typeof(Plugin.PluginTypeAttribute), true)
                                                                      .OfType<Plugin.PluginTypeAttribute>()
                                                                      .FirstOrDefault(pluginTypeAttribute => pluginTypeAttribute.Usage == Plugin.PluginTypeAttribute.UsageType.PhrictionDocument);
                        if (pluginType == null) continue;

                        plugin.CurrentUsageType = Plugin.PluginTypeAttribute.UsageType.PhrictionDocument;

                        if (plugin.IsVisibleInApplication(database, browser, phrictionDocument.Token)
                            && (httpServer.Customization.HidePlugins.ContainsKey(plugin.GetType().Name) == false
                                || httpServer.Customization.HidePlugins[plugin.GetType().Name] == false
                                )
                           )
                        {
                            HtmlPartialViewPage phrictionPluginData = viewPage.GetPartialView("PHRICTION-PLUGINS");
                            if (phrictionPluginData == null) break;  // we're in edit-mode, no need for plugins

                            phrictionPluginData.SetText("PHRICTION-PLUGIN-URL", plugin.URL, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                            phrictionPluginData.SetText("PHRICTION-PLUGIN-ICON", plugin.Icon, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                            phrictionPluginData.SetText("PHRICTION-PLUGIN-NAME", plugin.GetName(browser.Session.Locale));
                            phrictionPluginData.SetText("PHRICTION-PLUGIN-KEYBOARD-SHORTCUT", pluginType.KeyboardShortcut.ToUpper(), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        }

                        foreach (Plugin.PluginWithoutConfigurationBase pluginExtension in plugin.Extensions
                                                                                                .Where(ext => ext.IsVisibleInApplication(database, browser, phrictionDocument.Token)
                                                                                                           && (httpServer.Customization.HidePlugins.ContainsKey(ext.GetType().Name) == false
                                                                                                               || httpServer.Customization.HidePlugins[ext.GetType().Name] == false
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

                            HtmlPartialViewPage htmlPluginNavigatorMenuItem = viewPage.GetPartialView("PHRICTION-PLUGINS");
                            if (htmlPluginNavigatorMenuItem != null)
                            {
                                htmlPluginNavigatorMenuItem.SetText("PHRICTION-PLUGIN-URL", pluginExtension.URL, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                htmlPluginNavigatorMenuItem.SetText("PHRICTION-PLUGIN-ICON", pluginExtension.Icon, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                                htmlPluginNavigatorMenuItem.SetText("PHRICTION-PLUGIN-NAME", pluginExtension.GetName(browser.Session.Locale), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                            }
                        }
                    }
                }

                if (phrictionDocument.Subscribers != null)
                {
                    List<Subscriber> subscribers = phrictionDocument.Subscribers
                                                                    .Split(',')
                                                                    .Where(token => string.IsNullOrEmpty(token) == false)
                                                                    .Select(token =>
                                                                    {
                                                                        var user = userStorage.Get(database, token, browser.Session.Locale);
                                                                        if (user != null)
                                                                        {
                                                                            return new Subscriber
                                                                            {
                                                                                Token = user.Token,
                                                                                Name = user.RealName
                                                                            };
                                                                        }
                                                                        else
                                                                        {
                                                                            var project = projectStorage.Get(database, token, browser.Session.Locale);
                                                                            if (project == null) return null;

                                                                            return new Subscriber
                                                                            {
                                                                                Token = project.Token,
                                                                                Name = project.Name
                                                                            };
                                                                        }
                                                                    })
                                                                    .Where(s => s != null)
                                                                    .OrderBy(s => s.Name)
                                                                    .ToList();
                    foreach (Subscriber subscriber in subscribers)
                    {
                        HtmlPartialViewPage subscriberData = viewPage.GetPartialView("SUBSCRIBERS");
                        if (subscriberData != null)
                        {
                            subscriberData.SetText("DOCUMENT-SUBSCRIBER-TOKEN", subscriber.Token);
                            subscriberData.SetText("DOCUMENT-SUBSCRIBER-NAME", subscriber.Name);
                        }

                        subscriberTokens += "," + subscriber.Token;
                    }

                    if (subscribers.Any() == false)
                    {
                        HtmlPartialViewPage subscriberData = viewPage.GetPartialView("SUBSCRIBERS");
                        if (subscriberData != null)
                        {
                            subscriberData.Content = string.Format("<li class='list-item-view list-item-link' style='white-space: normal;'>{0}</li>", Locale.TranslateText("No users assigned", browser.Session.Locale));
                        }
                    }

                    if (phrictionDocument.Token != null)
                    {
                        foreach (Phabricator.Data.PhabricatorObject dependentObject in database.GetDependentObjects(phrictionDocument.Token, Language.NotApplicable))
                        {
                            Phabricator.Data.Phriction phrictionDocumentReferencer = dependentObject as Phabricator.Data.Phriction;
                            Phabricator.Data.Maniphest maniphestTaskReferencer = dependentObject as Phabricator.Data.Maniphest;
                            if (phrictionDocumentReferencer == null && maniphestTaskReferencer == null) continue;

                            HtmlPartialViewPage referencedData = viewPage.GetPartialView("REFERENCES");
                            if (referencedData != null)
                            {
                                if (phrictionDocumentReferencer != null)
                                {
                                    referencedData.SetText("REFERENCE-URL", "w/" + phrictionDocumentReferencer.Path);
                                    referencedData.SetText("REFERENCE-TEXT", phrictionDocumentReferencer.Name);
                                }

                                if (maniphestTaskReferencer != null)
                                {
                                    referencedData.SetText("REFERENCE-URL", string.Format("maniphest/T{0}/", maniphestTaskReferencer.ID));
                                    referencedData.SetText("REFERENCE-TEXT", maniphestTaskReferencer.Name);
                                }
                            }
                        }
                    }

                    viewPage.Merge();

                    viewPage.SetText("DOCUMENT-TAGS", projectTokens.Trim('\n'), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    viewPage.SetText("DOCUMENT-SUBSCRIBERS", subscriberTokens.Trim('\n'), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                }
                else
                {
                    viewPage.Merge();
                }
            }
        }

        /// <summary>
        /// This method is fired when the user adds a Phriction document to his/her favorites
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/phriction/addToFavorites")]
        public Http.Response.HttpMessage HttpPostAddToFavorites(Http.Server httpServer, string[] parameters)
        {
            if (httpServer.Customization.HidePhriction) throw new Phabrico.Exception.HttpNotFound("/phriction/addToFavorites");

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");

            Storage.Account accountStorage = new Storage.Account();

            Storage.FavoriteObject favoriteObjectStorage = new Storage.FavoriteObject();
            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // set private encryption key
                database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                string phrictionToken = browser.Session.FormVariables[browser.Request.RawUrl]["token"];
                Phabricator.Data.FavoriteObject[] allFavoriteObjects = favoriteObjectStorage.Get(database, browser.Session.Locale).ToArray();
                Phabricator.Data.Account accountData = accountStorage.WhoAmI(database, browser);
                Phabricator.Data.FavoriteObject favoriteObject = allFavoriteObjects.FirstOrDefault(fav => fav.Token.Equals(phrictionToken) && fav.AccountUserName.Equals(accountData.UserName));
                if (favoriteObject == null)
                {
                    favoriteObject = new Phabricator.Data.FavoriteObject();

                    favoriteObject.AccountUserName = accountData.UserName;
                    favoriteObject.Token = phrictionToken;
                    favoriteObject.DisplayOrder = allFavoriteObjects.Any() 
                                                                    ? allFavoriteObjects.Max(fav => fav.DisplayOrder) + 1 
                                                                    : 1;
                    favoriteObjectStorage.Add(database, favoriteObject);

                    InvalidatePhrictionDocumentFromCache(httpServer, database, phrictionToken, browser.Session.Locale);
                }
            }

            return null;
        }

        /// <summary>
        /// This method is fired when the user changes the sequence order of the favorite items in the hompage screen
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/phriction/changeOrderFavorites")]
        public Http.Response.HttpMessage HttpPostChangeOrderFavorites(Http.Server httpServer, string[] parameters)
        {
            if (httpServer.Customization.HidePhriction) throw new Phabrico.Exception.HttpNotFound("/phriction/changeOrderFavorites");

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");

            Storage.Account accountStorage = new Storage.Account();

            Storage.FavoriteObject favoriteObjectStorage = new Storage.FavoriteObject();
            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // set private encryption key
                database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                Phabricator.Data.Account accountData = accountStorage.WhoAmI(database, browser);

                string[] orderedFavoriteTokens = browser.Session.FormVariables[browser.Request.RawUrl]["tokens"]
                                                                .Split(',')
                                                                .ToArray();
                int displayOrder = 1;
                foreach (string favoriteToken in orderedFavoriteTokens)
                {
                    if (string.IsNullOrWhiteSpace(favoriteToken) == false)
                    {
                        // is not a splitter but a favorite item -> update item
                        Phabricator.Data.FavoriteObject favoriteObject = favoriteObjectStorage.Get(database, accountData.UserName, favoriteToken);
                        if (favoriteObject != null)
                        {
                            favoriteObject.DisplayOrder = displayOrder;
                            favoriteObjectStorage.Add(database, favoriteObject);
                        }
                    }

                    displayOrder++;
                }
            }

            return null;
        }

        /// <summary>
        /// This method is fired when the user enters 2 brackets in a Remarkup-editor.
        /// This method will return the items for the context menu item, that will be shown next.
        /// This context menu contains all the wiki documents, based on the filter the user is entering.
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/phriction/query")]
        public JsonMessage HttpPostPopulatePhrictionDocumentsContextMenu(Http.Server httpServer, string[] parameters)
        {
            if (httpServer.Customization.HidePhriction) throw new Phabrico.Exception.HttpNotFound("/phriction/query");

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");
            if (token.PrivateEncryptionKey == null) throw new Phabrico.Exception.AccessDeniedException("/user/query", "You don't have sufficient rights to configure Phabrico");

            Storage.Keyword keywordStorage = new Storage.Keyword();
            Storage.Phriction phrictionStorage = new Storage.Phriction();
            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                string[] phrictionTokens = new string[0];
                string requestedTitlePartial = "";

                if (parameters.Any())
                {
                    string[] words = System.Web.HttpUtility.UrlDecode(parameters[0])
                                                           .TrimStart(' ')
                                                           .Split(' ')
                                                           .Where(w => w.Any())
                                                           .ToArray();
                    if (words.Any())
                    {
                        requestedTitlePartial = string.Join(" ", words);
                        string[] titleWords = words.Select(p => "title:" + p).ToArray();
                        phrictionTokens = keywordStorage.GetTokensByWords(database, titleWords, Phabricator.Data.Phriction.Prefix, browser.Session.Locale)
                                                        .Take(50)
                                                        .ToArray();
                    }
                }

                Uri referer = new Uri(browser.Request.Referer);
                string localUrl = referer.LocalPath.PadRight(3, ' ').Substring("/w/".Length).TrimEnd('/');
                string parentUrl = string.Join("/", localUrl.Split('/').Reverse().Skip(1).Reverse());

                // translate tokens from search result into list of urls and titles
                List<Tuple<string, string>> records = new List<Tuple<string, string>>(
                    phrictionTokens.Select(t => new
                    {
                        Path = phrictionStorage.Get(database, t, browser.Session.Locale, false).Path,
                        Title = phrictionStorage.Get(database, t, browser.Session.Locale, false).Name
                    })
                    .Where(r => r.Title.IndexOf(requestedTitlePartial, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(r => new Tuple<string, string>(r.Path, r.Title))
                );

                string jsonData = JsonConvert.SerializeObject(new
                {
                    nbrSelected = records.Count(),
                    fontAwesomeIcon = "fa-book",
                    records = records.Select(r => new { Path = r.Item1, Title = r.Item2 })
                                     .Select(r => new
                                     {
                                         // convert path to relative path if possible
                                         Path = r.Path.StartsWith(localUrl)
                                                            ? ("[" + r.Path).Replace("[" + localUrl, ".")
                                                            : (parentUrl.Any() && r.Path.StartsWith(parentUrl))
                                                              ? ("[" + r.Path).Replace("[" + parentUrl, "..")
                                                              : r.Path,
                                         Title = r.Title
,                    fontAwesomeIcon = "fa-book",
                                     })
                                     .OrderBy(r => r.Title.StartsWith(requestedTitlePartial, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                                     .ThenBy(r => r.Path.StartsWith("..")
                                                  ? 2
                                                  : r.Path.StartsWith(".")
                                                    ? 1
                                                    : 3
                                            )
                                     .ThenBy(r => r.Title)
                                     .Take(5)
                });
                return new JsonMessage(jsonData);
            }
        }

        /// <summary>
        /// This method is fired when the user adds a Phriction document to his/her favorites
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/phriction/removeFromFavorites")]
        public Http.Response.HttpMessage HttpPostRemoveFromFavorites(Http.Server httpServer, string[] parameters)
        {
            if (httpServer.Customization.HidePhriction) throw new Phabrico.Exception.HttpNotFound("/phriction/removeFromFavorites");

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");

            Storage.Account accountStorage = new Storage.Account();

            Storage.FavoriteObject favoriteObjectStorage = new Storage.FavoriteObject();
            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // set private encryption key
                database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                Phabricator.Data.Account accountData = accountStorage.WhoAmI(database, browser);

                string phrictionToken = browser.Session.FormVariables[browser.Request.RawUrl]["token"];
                Phabricator.Data.FavoriteObject favoriteObject = favoriteObjectStorage.Get(database, accountData.UserName, phrictionToken);
                if (favoriteObject != null)
                {
                    favoriteObjectStorage.Remove(database, favoriteObject);

                    InvalidatePhrictionDocumentFromCache(httpServer, database, phrictionToken, browser.Session.Locale);
                }
            }

            return null;
        }

        [UrlController(URL = "/phriction/hideInSearchResults")]
        public Http.Response.HttpMessage HttpPostHideInSearchResults(Http.Server httpServer, string[] parameters)
        {
            if (httpServer.Customization.HidePhriction) throw new Phabrico.Exception.HttpNotFound("/phriction/hideInSearchResults");

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");

            Storage.Account accountStorage = new Storage.Account();

            Storage.Phriction phrictionStorage = new Storage.Phriction();
            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // set private encryption key
                database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                Phabricator.Data.Account accountData = accountStorage.WhoAmI(database, browser);

                string phrictionToken = browser.Session.FormVariables[browser.Request.RawUrl]["token"];
                Phabricator.Data.Phriction phrictionDocument =  phrictionStorage.Get(database, phrictionToken, Language.NotApplicable, false);
                if (phrictionDocument != null)
                {
                    phrictionStorage.HideFromSearchResults(database, phrictionDocument, accountData.UserName, true);
                    Http.Server.InvalidateNonStaticCache(database, DateTime.UtcNow);
                }
            }

            return null;
        }
        
        [UrlController(URL = "/phriction/showInSearchResults")]
        public Http.Response.HttpMessage HttpPostShowInSearchResults(Http.Server httpServer, string[] parameters)
        {
            if (httpServer.Customization.HidePhriction) throw new Phabrico.Exception.HttpNotFound("/phriction/hideInSearchResults");

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");

            Storage.Account accountStorage = new Storage.Account();

            Storage.Phriction phrictionStorage = new Storage.Phriction();
            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // set private encryption key
                database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                Phabricator.Data.Account accountData = accountStorage.WhoAmI(database, browser);

                string phrictionToken = browser.Session.FormVariables[browser.Request.RawUrl]["token"];
                Phabricator.Data.Phriction phrictionDocument =  phrictionStorage.Get(database, phrictionToken, Language.NotApplicable, false);
                if (phrictionDocument != null)
                {
                    phrictionStorage.HideFromSearchResults(database, phrictionDocument, accountData.UserName, false);
                    Http.Server.InvalidateNonStaticCache(database, DateTime.UtcNow);
                }
            }

            return null;
        }

        /// <summary>
        /// This method is fired when the user modifies a Phriction document
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/phriction", Alias = "/w")]
        public Http.Response.HttpMessage HttpPostSaveParameters(Http.Server httpServer, string[] parameters)
        {
            if (browser.InvalidCSRF(browser.Request.RawUrl)) throw new Phabrico.Exception.InvalidCSRFException();
            if (httpServer.Customization.HidePhriction) throw new Phabrico.Exception.HttpNotFound("/phriction");

            try
            {
                Storage.Phriction phrictionStorage = new Storage.Phriction();
                using (Storage.Database database = new Storage.Database(EncryptionKey))
                {
                    // set private encryption key
                    database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                    Phabricator.Data.Phriction parentPhrictionDocument = null;
                    Content content = new Content(database);
                    string tokenCurrentDocument = browser.Session.FormVariables[browser.Request.RawUrl]["token"];

                    string action = parameters.FirstOrDefault(parameter => parameter.StartsWith("?action="));
                    if (action == null)
                    {
                        action = "";
                    }
                    else
                    {
                        action = action.Substring("?action=".Length);
                    }

                    if (action.Equals("cancel"))
                    {
                        // cancel editing: remove all unreferenced files from database
                        Storage.Stage stageStorage = new Storage.Stage();

                        string phrictionDocumentToken = browser.Session.FormVariables[browser.Request.RawUrl]["token"];
                        Phabricator.Data.Phriction originalPhrictionDocument = stageStorage.Get<Phabricator.Data.Phriction>(database, phrictionDocumentToken, browser.Session.Locale);
                        if (originalPhrictionDocument == null || originalPhrictionDocument.Language.Equals(browser.Session.Locale) == false)
                        {
                            originalPhrictionDocument = phrictionStorage.Get(database, phrictionDocumentToken, browser.Session.Locale);
                        }

                        string documentContent = originalPhrictionDocument?.Content;
                        if (originalPhrictionDocument.Language.Equals(browser.Session.Locale) == false)
                        {
                            Content.Translation translation = content.GetTranslation(tokenCurrentDocument, browser.Session.Locale);
                            if (translation != null)
                            {
                                documentContent = translation.TranslatedRemarkup;
                            }
                        }

                        List<int> referencedFileIDs = browser.Session.FormVariables[browser.Request.RawUrl]["referencedFiles"]
                                                                     .Split(',')
                                                                     .Where(fileID => string.IsNullOrEmpty(fileID) == false)
                                                                     .Select(fileID => Int32.Parse(fileID))
                                                                     .ToList();

                        if (originalPhrictionDocument != null)
                        {
                            // analyze original wiki content and remove all the file-references from
                            // the current wiki content that were also found in the original one
                            Regex matchFileAttachments = new Regex("{F(-?[0-9]+)[^}]*}");
                            foreach (Match match in matchFileAttachments.Matches(documentContent).OfType<Match>().ToArray())
                            {
                                int fileID;

                                if (Int32.TryParse(match.Groups[1].Value, out fileID))
                                {
                                    referencedFileIDs.Remove(fileID);
                                }
                            }
                        }

                        Phabricator.Data.File[] stagedFiles = stageStorage.Get<Phabricator.Data.File>(database, browser.Session.Locale).ToArray();

                        foreach (int unreferencedFileID in referencedFileIDs)
                        {
                            Phabricator.Data.File unreferencedFile = stagedFiles.FirstOrDefault(stagedFile => stagedFile.ID == unreferencedFileID);
                            if (unreferencedFile != null)
                            {
                                stageStorage.Remove(database, browser, unreferencedFile);
                            }
                        }

                        return null;
                    }
                    else
                    if (action.Equals("save"))
                    {
                        Content.Translation translation = content.GetTranslation(tokenCurrentDocument, browser.Session.Locale);
                        bool isTranslation = translation != null;

                        Phabricator.Data.Phriction originalPhrictionDocument = phrictionStorage.Get(database, tokenCurrentDocument, browser.Session.Locale, isTranslation == false);
                        parentPhrictionDocument = phrictionStorage.Get(database, tokenCurrentDocument, browser.Session.Locale, false);

                        if (parentPhrictionDocument == null ||
                            browser.Session.FormVariables[browser.Request.RawUrl]["operation"] == "new")
                        {
                            Stage newStage = new Stage();
                            Phabricator.Data.Phriction newPhrictionDocument = new Phabricator.Data.Phriction();

                            newPhrictionDocument.Name = browser.Session.FormVariables[browser.Request.RawUrl]["title"];
                            newPhrictionDocument.Content = browser.Session.FormVariables[browser.Request.RawUrl]["textarea"];
                            newPhrictionDocument.DateModified = DateTimeOffset.UtcNow;
                            newPhrictionDocument.Projects = browser.Session.FormVariables[browser.Request.RawUrl]["tags"];
                            newPhrictionDocument.Subscribers = browser.Session.FormVariables[browser.Request.RawUrl]["subscribers"];

                            if (parentPhrictionDocument == null)
                            {
                                newPhrictionDocument.Path = "/";

                                newPhrictionDocument.Token = newStage.Create(database, browser, newPhrictionDocument);
                            }
                            else
                            {
                                newPhrictionDocument.Path = browser.Session.FormVariables[browser.Request.RawUrl]["path"];
                                if (newPhrictionDocument.Path.StartsWith(parentPhrictionDocument.Path))
                                {
                                    // if working with an aliased coverpage
                                    newPhrictionDocument.Path = FormatPhabricatorSlug("/", newPhrictionDocument.Path);
                                }
                                else
                                {
                                    // default
                                    newPhrictionDocument.Path = FormatPhabricatorSlug(parentPhrictionDocument.Path, newPhrictionDocument.Path);
                                }

                                string[] slugParts = newPhrictionDocument.Path.TrimEnd('/').Split('/');
                                if (slugParts.LastOrDefault().Equals(Locale.TranslateText("(New)", browser.Session.Locale)))
                                {
                                    // invalid slug "(New)" -> convert title to slug
                                    string newLastSlugPart = newPhrictionDocument.Name;
                                    newLastSlugPart = RegexSafe.Replace(newLastSlugPart, "[ <>&#%+={}\\[\\]\"'?/\\\\:]", "_");  // replace all invalid characters by _
                                    newLastSlugPart = newLastSlugPart.Replace("__", "_");  // no duplicated _ allowed
                                    newLastSlugPart = newLastSlugPart.ToLowerInvariant();  // all characters in lowercase

                                    newPhrictionDocument.Path = string.Join("/", slugParts.Take(slugParts.Length - 1)) + "/" + newLastSlugPart + "/";
                                }

                                // remove Url encoding
                                newPhrictionDocument.Path = HttpUtility.UrlDecode(newPhrictionDocument.Path);

                                // re-encode Url again
                                newPhrictionDocument.Path = Parsers.Remarkup.Rules.RemarkupRule.UrlEncode(newPhrictionDocument.Path);

                                // verify parent document and build them if they are not existing
                                // this can happen when a document is created by means of hyperlink
                                List<Phabricator.Data.Phriction> inexistantParentDocuments = new List<Phabricator.Data.Phriction>();
                                string[] pathElements = newPhrictionDocument.Path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries).ToArray();
                                while (true)
                                {
                                    pathElements = pathElements.Take(pathElements.Length - 1).ToArray();
                                    if (pathElements.Any() == false) break;  // we're at the root -> stop

                                    string parentPath = string.Join("/", pathElements) + "/";
                                    parentPhrictionDocument = phrictionStorage.Get(database, parentPath, browser.Session.Locale, false);
                                    if (parentPhrictionDocument != null) break;  // parent document found -> stop

                                    Phabricator.Data.Phriction newParentPhrictionDocument = new Phabricator.Data.Phriction();
                                    newParentPhrictionDocument.Name = pathElements.Last();
                                    newParentPhrictionDocument.Path = parentPath;
                                    newParentPhrictionDocument.Content = " ";
                                    newParentPhrictionDocument.DateModified = DateTimeOffset.UtcNow;
                                    newParentPhrictionDocument.Projects = "";
                                    newParentPhrictionDocument.Subscribers = "";

                                    inexistantParentDocuments.Add(newParentPhrictionDocument);
                                }

                                // create parent documents
                                inexistantParentDocuments.Reverse();
                                foreach (Phabricator.Data.Phriction newParentPhrictionDocument in inexistantParentDocuments)
                                {
                                    newParentPhrictionDocument.Token = newStage.Create(database, browser, newParentPhrictionDocument);

                                    if (parentPhrictionDocument != null)
                                    {
                                        database.DescendTokenFrom(parentPhrictionDocument.Token, newParentPhrictionDocument.Token);
                                    }

                                    parentPhrictionDocument = newParentPhrictionDocument;
                                }

                                // create document
                                newPhrictionDocument.Token = newStage.Create(database, browser, newPhrictionDocument);

                                if (parentPhrictionDocument != null)
                                {
                                    database.DescendTokenFrom(parentPhrictionDocument.Token, newPhrictionDocument.Token);

                                    // start uncaching parent and grandparent (otherwise, the document hierarchy at the bottom is not updated)
                                    // uncache parent
                                    InvalidatePhrictionDocumentFromCache(httpServer, database, parentPhrictionDocument.Token, browser.Session.Locale);

                                    string grandparentPath = string.Join("/", newPhrictionDocument.Path
                                                                                                  .Split('#')[0]
                                                                                                  .Split('/')
                                                                                                  .Reverse()
                                                                                                  .SkipWhile(p => string.IsNullOrWhiteSpace(p))
                                                                                                  .Skip(2)
                                                                                                  .Reverse());
                                    Phabricator.Data.Phriction grandparentPhrictionDocument = phrictionStorage.Get(database, grandparentPath, browser.Session.Locale, false);
                                    if (grandparentPhrictionDocument != null)
                                    {
                                        // uncache grandparent
                                        InvalidatePhrictionDocumentFromCache(httpServer, database, grandparentPhrictionDocument.Token, browser.Session.Locale);
                                    }
                                }
                            }

                            Storage.Keyword keywordStorage = new Storage.Keyword();
                            keywordStorage.AddPhabricatorObject(this, database, newPhrictionDocument);

                            // (re)assign dependent Phabricator objects
                            database.ClearAssignedTokens(newPhrictionDocument.Token, Language.NotApplicable);
                            RemarkupParserOutput remarkupParserOutput;
                            ConvertRemarkupToHTML(database, newPhrictionDocument.Path, newPhrictionDocument.Content, out remarkupParserOutput, false, newPhrictionDocument.Token);
                            foreach (Phabricator.Data.PhabricatorObject linkedPhabricatorObject in remarkupParserOutput.LinkedPhabricatorObjects)
                            {
                                database.AssignToken(newPhrictionDocument.Token, linkedPhabricatorObject.Token, Language.NotApplicable);
                            }

                            string redirectUrl = string.Format("/w/{0}", newPhrictionDocument.Path);
                            while (redirectUrl.EndsWith("//"))
                            {
                                // make sure we have no url ending with multiple slashes
                                redirectUrl = redirectUrl.Substring(0, redirectUrl.Length - 1);
                            }

                            // clear cached crumbs translations
                            lock (lockCachedCrumbTranslations)
                            {
                                cachedCrumbTranslations = new TransientDictionary<string, Tuple<Phabricator.Data.Phriction, Content.Translation>>(TimeSpan.FromSeconds(2), false);
                            }

                            redirectUrl = Http.Server.RootPath + redirectUrl;
                            redirectUrl = redirectUrl.Replace("//", "/");

                            return new Http.Response.HttpRedirect(httpServer, browser, redirectUrl);
                        }
                        else
                        if (parentPhrictionDocument != null)
                        {
                            // verify if content has been changed
                            string modifiedContent = browser.Session.FormVariables[browser.Request.RawUrl]["textarea"];
                            if (originalPhrictionDocument != null &&  modifiedContent.Replace("\r", "").Equals(originalPhrictionDocument.Content.Replace("\r", "")))
                            {
                                // content has not been changed -> stop processing
                                return null;
                            }

                            Phabricator.Data.Phriction modifiedPhrictionDocument = new Phabricator.Data.Phriction(parentPhrictionDocument);
                            modifiedPhrictionDocument.Name = browser.Session.FormVariables[browser.Request.RawUrl]["title"];
                            modifiedPhrictionDocument.Content = modifiedContent;
                            modifiedPhrictionDocument.Projects = browser.Session.FormVariables[browser.Request.RawUrl]["tags"]?.Trim();
                            modifiedPhrictionDocument.Subscribers = browser.Session.FormVariables[browser.Request.RawUrl]["subscribers"]?.Trim();
                            modifiedPhrictionDocument.DateModified = DateTimeOffset.UtcNow;

                            Language language = browser.Session.Locale;
                            if (isTranslation == false)
                            {
                                language = Language.NotApplicable;
                            }

                            modifiedPhrictionDocument.Language = language;

                            Stage stageStorage = new Stage();
                            stageStorage.Modify(database, modifiedPhrictionDocument, browser);

                            content.DisapproveTranslationForAllLanguages(modifiedPhrictionDocument.Token);

                            bool doFreezeReferencedFiles = stageStorage.Get(database, browser.Session.Locale)
                                                                       .FirstOrDefault(stagedObject => stagedObject.Token.Equals(modifiedPhrictionDocument.Token))
                                                                       .Frozen;

                            Storage.Keyword keywordStorage = new Storage.Keyword();
                            keywordStorage.DeletePhabricatorObject(database, parentPhrictionDocument);
                            keywordStorage.AddPhabricatorObject(this, database, modifiedPhrictionDocument);

                            // (re)assign dependent Phabricator objects
                            List<Phabricator.Data.PhabricatorObject> referencedObjects = database.GetReferencedObjects(modifiedPhrictionDocument.Token, browser.Session.Locale).ToList();
                            string[] unassignedStagedTokens = database.ClearAssignedTokens(modifiedPhrictionDocument.Token, language)
                                                                      .Where(token => token.StartsWith("PHID-NEWTOKEN-"))
                                                                      .ToArray();
                            RemarkupParserOutput remarkupParserOutput;
                            List<Phabricator.Data.PhabricatorObject> linkedPhabricatorObjects;
                            List<int> originalInvalidLinkedFileIDs = new List<int>();
                            ConvertRemarkupToHTML(database, modifiedPhrictionDocument.Path, modifiedPhrictionDocument.Content, out remarkupParserOutput, false, modifiedPhrictionDocument.Token);
                            linkedPhabricatorObjects = remarkupParserOutput.LinkedPhabricatorObjects;
                            List<int> newInvalidLinkedFileIDs = remarkupParserOutput.InvalidLinkedFileObjectIDs;
                            referencedObjects.AddRange(unassignedStagedTokens.Select(unassignedStagedToken =>
                            {
                                return stageStorage.Get<Phabricator.Data.File>(database, unassignedStagedToken, browser.Session.Locale);
                            }));
                            referencedObjects = referencedObjects.Where(obj => obj != null)
                                                                 .GroupBy(obj => obj.Token)
                                                                 .Select(g => g.FirstOrDefault())
                                                                 .ToList();
                            if (originalPhrictionDocument != null)
                            {
                                if (isTranslation)
                                {
                                    ConvertRemarkupToHTML(database, modifiedPhrictionDocument.Path, translation.TranslatedRemarkup, out remarkupParserOutput, false, modifiedPhrictionDocument.Token);  // remember also references in original content, so we can always undo our modifications
                                }
                                else
                                {
                                    ConvertRemarkupToHTML(database, modifiedPhrictionDocument.Path, originalPhrictionDocument.Content, out remarkupParserOutput, false, modifiedPhrictionDocument.Token);  // remember also references in original content, so we can always undo our modifications
                                }
                                linkedPhabricatorObjects.AddRange(remarkupParserOutput.LinkedPhabricatorObjects);
                                originalInvalidLinkedFileIDs = remarkupParserOutput.InvalidLinkedFileObjectIDs;
                            }
                            foreach (Phabricator.Data.PhabricatorObject linkedPhabricatorObject in linkedPhabricatorObjects.Distinct())
                            {
                                database.AssignToken(modifiedPhrictionDocument.Token, linkedPhabricatorObject.Token, language);

                                Phabricator.Data.File linkedFile = linkedPhabricatorObject as Phabricator.Data.File;
                                if (linkedFile != null && linkedFile.ID < 0)  // linkedFile.ID < 0: file is staged
                                {
                                    stageStorage.Freeze(database, browser, linkedFile.Token, doFreezeReferencedFiles);
                                }

                                referencedObjects.RemoveAll(obj => obj.Token.Equals(linkedPhabricatorObject.Token));
                            }

                            // delete all unreferenced Phabricator objects from staging area (if existant)
                            foreach (Phabricator.Data.PhabricatorObject oldReferencedObject in referencedObjects)
                            {
                                if (database.GetReferencedObjects(oldReferencedObject.Token, language).Any() == false &&
                                    database.GetDependentObjects(oldReferencedObject.Token, language).Any() == false
                                   )
                                {
                                    stageStorage.Remove(database, browser, oldReferencedObject, language);
                                }
                            }

                            // unmark previously marked invalid fileobjects which do not appear anymore in new content
                            foreach (int unreferencedFileID in originalInvalidLinkedFileIDs.Except(newInvalidLinkedFileIDs))
                            {
                                database.MarkFileObject(unreferencedFileID, false, "");
                            }
                        }
                    }
                }
            }
            finally
            {
                string urlAlias;
                string url = browser.Request.RawUrl.Split('?')[0].TrimEnd('/');
                
                url = RegexSafe.Replace(url, "[ ?]", "_");
                url = HttpUtility.UrlDecode(url);

                if (url.StartsWith("/phriction/"))
                {
                    urlAlias = "/w/" + url.Substring("/phriction/".Length);

                    httpServer.InvalidateNonStaticCache(EncryptionKey, url);
                    httpServer.InvalidateNonStaticCache(EncryptionKey, urlAlias);
                }
                else
                if (url.StartsWith("/w/"))
                {
                    urlAlias = "/phriction/" + url.Substring("/w/".Length);

                    httpServer.InvalidateNonStaticCache(EncryptionKey, url);
                    httpServer.InvalidateNonStaticCache(EncryptionKey, urlAlias);
                }
            }

            return null;
        }

        /// <summary>
        /// Removes a given Phriction document from cache, so that it will be reloaded from disk
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="database"></param>
        /// <param name="phrictionToken"></param>
        /// <param name="language"></param>
        private void InvalidatePhrictionDocumentFromCache(Http.Server httpServer, Storage.Database database, string phrictionToken, Language language)
        {
            Storage.Phriction phrictionStorage = new Storage.Phriction();
            Phabricator.Data.Phriction phrictionDocument = phrictionStorage.Get(database, phrictionToken, language);
            if (phrictionDocument != null)
            {
                string url = phrictionDocument.Path;
                httpServer.InvalidateNonStaticCache(EncryptionKey, url);
            }
        }
    }
}
