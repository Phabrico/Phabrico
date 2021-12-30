﻿using Newtonsoft.Json;
using Phabrico.Http;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phabrico.Controllers
{
    /// <summary>
    /// Represents the controller being used for the translation-functionality in Phabrico
    /// </summary>
    public class Translation : Controller
    {
        public class JsonRecord
        {
            public string Token;
            public string Title;
            public string OriginalTitle;
            public string URL;
        }

        /// <summary>
        /// This method is fired when the user clicks on 'Approve translation' for a Phriction document
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/translations/approve")]
        public Http.Response.HttpMessage HttpPostApproveTranslation(Http.Server httpServer, Browser browser, string[] parameters)
        {
            string jsonData;
            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // set private encryption key
                database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                string tokenToTranslate = browser.Session.FormVariables[browser.Request.RawUrl]["token"];

                Content content = new Content(database);

                Storage.Stage stageStorage = new Storage.Stage();
                Phabricator.Data.Phriction phrictionDocument = stageStorage.Get<Phabricator.Data.Phriction>(database, tokenToTranslate, browser.Session.Locale);

                if (phrictionDocument == null)
                {
                    Storage.Phriction phrictionStorage = new Storage.Phriction();
                    phrictionDocument = phrictionStorage.Get(database, tokenToTranslate, Language.NotApplicable, true);
                }
                else
                {
                    Content.Translation translation = content.GetTranslation(phrictionDocument.Token, browser.Session.Locale);

                    if (phrictionDocument.Language.Equals(Language.NotApplicable) &&
                        translation?.DateModified < phrictionDocument.DateModified
                       )
                    {
                        jsonData = JsonConvert.SerializeObject(new
                        {
                            Status = "MasterDataModified",
                            Message = Locale.TranslateText("The original content of this document has changed. You must update the translation before you can approve it.", browser.Session.Locale)
                        });
                        return new JsonMessage(jsonData);
                    }
                    else
                    if (phrictionDocument.Language.Equals(browser.Session.Locale))
                    {
                        // move stageInfo record to contentTranslation table
                        content.AddTranslation(tokenToTranslate, browser.Session.Locale, phrictionDocument.Name, phrictionDocument.Content);
                        stageStorage.Remove(database, browser, phrictionDocument);
                    }
                }

                content.ApproveTranslation(tokenToTranslate, browser.Session.Locale);

                if (phrictionDocument != null)
                {
                    httpServer.InvalidateNonStaticCache(phrictionDocument.Path);
                }
            }

            jsonData = JsonConvert.SerializeObject(new
            {
                Status = "OK"
            });
            return new JsonMessage(jsonData);
        }

        /// <summary>
        /// This method is executed as soon as the user enters some characters in the search field in the upper right corner.
        /// A JSONified array of SearchResult items is returned to visualize the search results
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/translations/unreviewed")]
        public void HttpGetUnreviewedTranslations(Http.Server httpServer, Browser browser, ref HtmlViewPage viewPage, string[] parameters, string parameterActions)
        {
            // show overview screen
            viewPage = new HtmlViewPage(httpServer, browser, true, "Translations.Unreviewed", parameters);
        }

        /// <summary>
        /// This method is fired when the user clicks on the 'Revert to original translation' button in the Phriction-Edit screen
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="parameters"></param>
        [UrlController(URL = "/translations/revert")]
        public void HttpPostRevertStagedTranslations(Http.Server httpServer, Browser browser, string[] parameters)
        {
            Staging stagingController = new Staging();
            stagingController.EncryptionKey = EncryptionKey;

            string token = browser.Session.FormVariables[browser.Request.RawUrl]["token"];
            browser.Session.FormVariables[browser.Request.RawUrl]["item"] = token + "[edit]";       // add required form-variable for stagingController.HttpPostUndo method
            browser.Session.FormVariables[browser.Request.RawUrl]["use-local-language"] = "true";   // add optional form-variable for stagingController.HttpPostUndo method
            stagingController.HttpPostUndo(httpServer, browser, parameters);
        }

        /// <summary>
        /// This method is fired when the list of unreviewed translations is requested by an AJAX call
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/translations/unreviewed")]
        public JsonMessage HttpPostPopulateUnreviewedTranslationsTableData(Http.Server httpServer, Browser browser, string[] parameters)
        {
            SessionManager.Token token = SessionManager.GetToken(browser);

            Storage.File fileStorage = new Storage.File();
            Storage.Phriction phrictionStorage = new Storage.Phriction();

            using (Phabrico.Storage.Database database = new Phabrico.Storage.Database(EncryptionKey))
            {
                // set private encryption key
                database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                Content content = new Content(database);
                Content.Translation[] translations = content.GetUnreviewedTranslations(browser.Session.Locale).ToArray();
                List<JsonRecord> unreviewedTranslations = translations.Select(translation => new
                                                            {
                                                                Token = translation.Token,
                                                                TranslatedTitle = translation.TranslatedTitle,
                                                                Language = translation.Language,
                                                                OriginalPhrictionDocument = phrictionStorage.Get(database, translation.Token, translation.Language, false)
                                                            })
                                                            .Where(translation => translation.OriginalPhrictionDocument != null)
                                                            .Select(translation => new  JsonRecord
                                                            {
                                                                Token = translation.Token,
                                                                Title = translation.TranslatedTitle,
                                                                OriginalTitle = string.IsNullOrWhiteSpace(translation.OriginalPhrictionDocument.Name)
                                                                              ? Locale.TranslateText("EmptyParameter", translation.Language)
                                                                              : translation.OriginalPhrictionDocument.Name,
                                                                URL = "w/" + translation.OriginalPhrictionDocument.Path
                                                            })
                                                            .ToList();

                unreviewedTranslations.AddRange(translations.Select(translation => new
                                                            {
                                                                Token = translation.Token,
                                                                TranslatedTitle = translation.TranslatedTitle,
                                                                Language = translation.Language,
                                                                OriginalFileObject = fileStorage.Get(database, translation.Token, translation.Language)
                                                            })
                                                            .Where(translation => translation.OriginalFileObject != null)
                                                            .Select(translation => new JsonRecord
                                                            {
                                                                Token = translation.Token,
                                                                Title = "F" + translation.OriginalFileObject.ID,
                                                                OriginalTitle = "Diagram " + translation.TranslatedTitle,
                                                                URL = "diagrams.net/F" + translation.OriginalFileObject.ID
                                                            })
                );

                if (parameters.Any())
                {
                    string filter = "";
                    string orderBy = System.Web.HttpUtility.UrlDecode(parameters[0]);
                    if (parameters.Length > 1)
                    {
                        filter = System.Web.HttpUtility.UrlDecode(parameters[1]);
                    }

                    unreviewedTranslations = unreviewedTranslations.Where(unreviewed => unreviewed.OriginalTitle.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                                                                                     || unreviewed.Title.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                                                                         )
                                                                   .ToList();

                    switch (orderBy.TrimEnd('-'))
                    {
                        default:
                            if (orderBy.Last() == '-')
                                unreviewedTranslations = unreviewedTranslations.OrderByDescending(o => o.Title).ToList();
                            else
                                unreviewedTranslations = unreviewedTranslations.OrderBy(o => o.Title).ToList();
                            break;
                    }
                }

                string jsonData = JsonConvert.SerializeObject(unreviewedTranslations);
                return new JsonMessage(jsonData);
            }
        }

        /// <summary>
        /// This method is fired when the user clicks on the 'Undo' button in the 'Unreviewed translations' screen
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/translations/undo")]
        public Http.Response.HttpMessage HttpPostRemoveTranslation(Http.Server httpServer, Browser browser, string[] parameters)
        {
            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                // set private encryption key
                database.PrivateEncryptionKey = browser.Token.PrivateEncryptionKey;

                string tokenToTranslate = browser.Session.FormVariables[browser.Request.RawUrl]["token"];

                Content content = new Content(database);
                content.DeleteTranslation(tokenToTranslate, browser.Session.Locale);

                // uncache document
                Storage.Phriction phrictionStorage = new Storage.Phriction();
                Phabricator.Data.Phriction phrictionDocument = phrictionStorage.Get(database, tokenToTranslate, Language.NotApplicable);
                if (phrictionDocument != null)
                {
                    httpServer.InvalidateNonStaticCache(phrictionDocument.Path);
                }
            }

            return null;
        }
    }
}