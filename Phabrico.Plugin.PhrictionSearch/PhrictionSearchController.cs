using Newtonsoft.Json;
using Phabrico.Controllers;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Phabrico.Plugin
{
    /// <summary>
    /// Represents the controller for all the PhrictionSearch functionalities
    /// </summary>
    public class PhrictionSearchController : PluginController
    {
        /// <summary>
        /// This method is fired when the user clicks on the 'Search text' in the Phriction action pane.
        /// It will search for this corresponding text in the current document and all of its underlying documents
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/PhrictionSearch")]
        public JsonMessage HttpPostValidateDocument(Http.Server httpServer, string[] parameters)
        {
            Phabrico.Storage.Phriction phrictionStorage = new Phabrico.Storage.Phriction();
            DictionarySafe<string, string> formVariables = browser.Session.FormVariables[browser.Request.RawUrl];

            List<string> underlyingPhrictionTokens = new List<string>();
            using (Phabrico.Storage.Database database = new Phabrico.Storage.Database(EncryptionKey))
            {
                string jsonData;
                Phabricator.Data.Phriction phrictionDocument = phrictionStorage.Get(database, PhrictionData.Path, browser.Session.Locale);

                if (phrictionDocument != null)
                {
                    RetrieveUnderlyingPhrictionDocuments(database, phrictionDocument.Token, ref underlyingPhrictionTokens);
                }

                List<Phabricator.Data.Phriction> phrictionDocumentsContainingText = new List<Phabricator.Data.Phriction>();
                if (formVariables.ContainsKey("search-text") == false)
                {
                    string dlgSearchText = string.Format($@"
                        <input type='text' name='search-text' value='' />
                    ");

                    jsonData = JsonConvert.SerializeObject(new
                    {
                        Status = "Prepare",
                        DialogTitle = Locale.TranslateText("Enter search text", browser.Session.Locale),
                        DialogHTML = dlgSearchText,
                    });

                    return new JsonMessage(jsonData);
                }

                string text = formVariables["search-text"];
                if (string.IsNullOrWhiteSpace(text))
                {
                    jsonData = JsonConvert.SerializeObject(new
                    {
                        Status = "Finished",
                    });

                    return new JsonMessage(jsonData);
                }

                if (FindTextInDocument(database, phrictionDocument?.Token, text) != null)
                {
                    phrictionDocumentsContainingText.Add(phrictionDocument);
                }
                foreach (string underlyingPhrictionToken in underlyingPhrictionTokens)
                {
                    Phabricator.Data.Phriction underlyingPhrictionDocumentContainingSearchText = FindTextInDocument(database, underlyingPhrictionToken, text);
                    if (underlyingPhrictionDocumentContainingSearchText != null)
                    {
                        phrictionDocumentsContainingText.Add(underlyingPhrictionDocumentContainingSearchText);
                    }
                }

                if (phrictionDocumentsContainingText.Any() == false)
                {
                    jsonData = JsonConvert.SerializeObject(new
                    {
                        Status = "Finished",
                        MessageBox = new
                        {
                            Title = Locale.TranslateText("PluginName.PhrictionSearch", browser.Session.Locale),
                            Message = Locale.TranslateText("No documents were found", browser.Session.Locale)
                        }
                    });

                    return new JsonMessage(jsonData);
                }
                else
                {
                    // return result
                    jsonData = JsonConvert.SerializeObject(new
                    {
                        Status = "Finished",
                        TableData = new
                        {
                            Header = new string[] {
                                "Search result"
                            },
                            Data = phrictionDocumentsContainingText.OrderBy(data => data.Path.Split('/').Length)
                                                                   .ThenBy(data => data.Name)
                                                                   .Select(data => new string[] {
                                                                                        "<a href=\"" + Http.Server.RootPath + "w/" + data.Path + "\">" + HttpUtility.HtmlEncode(data.Name) + "</a>"
                                                                                   }
                                                                          )
                                                                   .ToArray()
                        }
                    });

                    return new JsonMessage(jsonData);
                }
            }
        }

        private Phabricator.Data.Phriction FindTextInDocument(Database database, string phrictionToken, string searchText)
        {
            Storage.Phriction phrictionStorage = new Storage.Phriction();

            Phabricator.Data.Phriction phrictionDocument = phrictionStorage.Get(database, phrictionToken, browser.Session.Locale);
            if (phrictionDocument == null) return null;
            if (string.IsNullOrWhiteSpace(phrictionDocument.Content)) return null;
            if (phrictionDocument.Content.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) == -1) return null;

            string[] words = phrictionDocument.Content.Split(' ', ',', '.', '\'', '"', '(', ')', ':', ';', '{', '}', '[', ']', '\\', '|', '?', '!', '<', '>');
            foreach (string searchWord in searchText.Split(' '))
            {
                if (words.Contains(searchWord, StringComparer.OrdinalIgnoreCase) == false) return null;
            }

            return phrictionDocument;
        }

        /// <summary>
        /// Downloads recursively all underlying Phriction documents for a given Phriction document
        /// </summary>
        /// <param name="database">Phabrico database</param>
        /// <param name="phrictionToken">Token of Phriction document to be searched</param>
        /// <param name="underlyingPhrictionTokens">Collection of tokens of all underlying Phriction documents</param>
        private void RetrieveUnderlyingPhrictionDocuments(Database database, string phrictionToken, ref List<string> underlyingPhrictionTokens)
        {
            Storage.Stage stageStorage = new Storage.Stage();
            Storage.Phriction phrictionStorage = new Storage.Phriction();
            Phabricator.Data.Phriction phrictionDocument = phrictionStorage.Get(database, phrictionToken, browser.Session.Locale);
            List<Phabricator.Data.Phriction> underlyingDocuments = phrictionStorage.Get(database, browser.Session.Locale)
                                                                                   .Where(wiki => wiki.Path.StartsWith(phrictionDocument.Path.TrimStart('/')))
                                                                                   .ToList();
            underlyingDocuments.AddRange( stageStorage.Get<Phabricator.Data.Phriction>(database, browser.Session.Locale)
                                                      .Where(stagedWiki => stagedWiki.Token.StartsWith("PHID-NEWTOKEN-")
                                                                        && stagedWiki.Path.StartsWith(phrictionDocument.Path.TrimStart('/'))
                                                            )
                                        );

            underlyingDocuments.RemoveAll(wiki => wiki.Path.Equals(phrictionDocument.Path));

            underlyingPhrictionTokens = underlyingDocuments.Select(wiki => wiki.Token).ToList();
        }
    }
}
