using Newtonsoft.Json;
using Phabrico.Controllers;
using Phabrico.Http;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Phabrico.Plugin
{
    /// <summary>
    /// Represents the controller for all the PhrictionValidator functionalities
    /// </summary>
    public class PhrictionValidatorController : PluginController
    {
        /// <summary>
        /// This method is fired when the user clicks on the 'Validate document' in the Phriction action pane.
        /// It will validate hyperlinks and linked files
        /// 
        /// This method will also check if there are any underlying Phriction documents under the current Phriction document.
        /// If so, a 'Confirm' JSON request will be sent instead representing a question to the user if the underlying documents
        /// should also be validated.
        /// The response for this 'Confirm' JSON request is handled by the HttpPostValidationConfirmation method
        /// After this HttpPostValidationConfirmation reponse is handled in the browser, this method is executed again
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/PhrictionValidator")]
        public JsonMessage HttpPostValidateDocument(Http.Server httpServer, string[] parameters)
        {
            Phabrico.Storage.Phriction phrictionStorage = new Phabrico.Storage.Phriction();

            List<string> underlyingPhrictionTokens = new List<string>();
            using (Phabrico.Storage.Database database = new Phabrico.Storage.Database(EncryptionKey))
            {
                string jsonData;
                Phabricator.Data.Phriction phrictionDocument = phrictionStorage.Get(database, PhrictionData.Path, browser.Session.Locale);

                if (phrictionDocument != null)
                {
                    RetrieveUnderlyingPhrictionDocuments(database, phrictionDocument.Token, ref underlyingPhrictionTokens);
                }

                int nbrUnderlyingPhrictionTokens = underlyingPhrictionTokens.Count;
                if (nbrUnderlyingPhrictionTokens > 0 && PhrictionData.ConfirmState == ConfirmResponse.None)
                {
                    string message;

                    if (nbrUnderlyingPhrictionTokens == 1)
                    {
                        message = Locale.TranslateText("There is 1 underlying document. Would you like to validate this as well ?", browser.Session.Locale);
                    }
                    else
                    {
                        message = Locale.TranslateText("There are @@NBR-CHILD-DOCUMENTS@@ underlying documents. Would you like to validate these as well ?", browser.Session.Locale)
                                        .Replace("@@NBR-CHILD-DOCUMENTS@@", nbrUnderlyingPhrictionTokens.ToString());
                    }

                    jsonData = JsonConvert.SerializeObject(new
                    {
                        Status = "Confirm",
                        Message = message
                    });

                    return new JsonMessage(jsonData);
                }
                else
                {
                    Dictionary<int, List<string>> invalidFiles = new Dictionary<int, List<string>>();
                    Dictionary<string, List<string>> invalidHyperlinks = new Dictionary<string, List<string>>();

                    ValidateDocument(database, phrictionDocument?.Token, invalidFiles, invalidHyperlinks);

                    // do we also need to validate the underlying documents ?
                    if (PhrictionData.ConfirmState == ConfirmResponse.Yes)
                    {
                        foreach (string underlyingPhrictionToken in underlyingPhrictionTokens)
                        {
                            ValidateDocument(database, underlyingPhrictionToken, invalidFiles, invalidHyperlinks);
                        }
                    }

                    if (invalidFiles.Any() == false && invalidHyperlinks.Any() == false)
                    {
                        jsonData = JsonConvert.SerializeObject(new
                        {
                            Status = "Finished",
                            MessageBox = new
                            {
                                Title = Locale.TranslateText("PluginName.PhrictionValidator", browser.Session.Locale),
                                Message = Locale.TranslateText("No errors were found", browser.Session.Locale)
                            }
                        });

                        return new JsonMessage(jsonData);
                    }
                    else
                    {
                        // generate row data
                        object[] tableData = invalidFiles.Select(kvp => new object[] {
                                                                    Locale.TranslateJavascript("Invalid file reference @@FILE-REFERENCE-ID@@", browser.Session.Locale)
                                                                          .Replace("@@FILE-REFERENCE-ID@@", "<span style='color:var(--a-color)'>" + kvp.Key + "</span>"),
                                                                    kvp.Value
                                                                })
                                                                .Distinct()
                                                                .Concat(
                                                invalidHyperlinks.Select(kvp => new object[] {
                                                                    Locale.TranslateJavascript("Invalid hyperlink @@URL@@", browser.Session.Locale)
                                                                          .Replace("@@URL@@", "<span style='color:var(--a-color)'>" + kvp.Key + "</span>"),
                                                                    kvp.Value
                                                                 })

                                                            )
                                                .ToArray();

                        List<string[]> result = new List<string[]>();
                        foreach (object[] record in tableData)
                        {
                            List<string> origins = (List<string>)record[1];
                            foreach (string origin in origins.OrderBy(o => o.ToUpperInvariant()))
                            {
                                phrictionDocument = phrictionStorage.Get(database, origin, browser.Session.Locale);
                                string title = HttpUtility.HtmlEncode(phrictionDocument.Name);
                                string phrictionDocumentPath = origin.TrimEnd('/');
                                while (PhrictionData.Path.TrimEnd('/').Equals(phrictionDocumentPath.TrimEnd('/')) == false)
                                {
                                    string[] pathParts = phrictionDocumentPath.Split('/');
                                    if (pathParts.Length <= 1) break;
                                    phrictionDocumentPath = string.Join("/", pathParts.Take(pathParts.Length - 1));

                                    phrictionDocument = phrictionStorage.Get(database, phrictionDocumentPath, browser.Session.Locale);
                                    if (phrictionDocument == null)
                                    {
                                        title = phrictionDocumentPath.Split('/').LastOrDefault() + " > " + title;
                                    }
                                    else
                                    {
                                        title = HttpUtility.HtmlEncode(phrictionDocument.Name) + " > " + title;
                                    }
                                }

                                string url = Http.Server.RootPath + "w/" + origin;
                                string[] row = new string[record.Length];
                                row[0] = "<a href=\"" + url + "\">" + title + "</a>";
                                row[1] = record[0].ToString();
                                result.Add(row);
                            }
                        }

                        // return result
                        jsonData = JsonConvert.SerializeObject(new
                        {
                            Status = "Finished",
                            TableData = new
                            {
                                Header = new string[] {
                                "Source",
                                "Error"
                            },
                                Data = result.OrderBy(data => data.FirstOrDefault())
                            }
                        });

                        return new JsonMessage(jsonData);
                    }
                }
            }
        }

        private IEnumerable<Parsers.Remarkup.Rules.RemarkupRule> GetAllRemarkupTokens(IEnumerable<Parsers.Remarkup.Rules.RemarkupRule> me)
        {
            foreach (Parsers.Remarkup.Rules.RemarkupRule child in me)
            {
                yield return child;

                foreach (Parsers.Remarkup.Rules.RemarkupRule grandchild in GetAllRemarkupTokens(child.ChildTokenList))
                {
                    yield return grandchild;
                }
            }
        }

        private void ValidateDocument(Database database, string phrictionToken, Dictionary<int, List<string>> invalidFiles, Dictionary<string, List<string>> invalidHyperlinks)
        {
            Storage.File fileStorage = new Storage.File();
            Storage.Phriction phrictionStorage = new Storage.Phriction();
            Storage.Stage stageStorage = new Storage.Stage();
            List<string> invalidFileReferences;
            List<string> invalidHyperlinkReferences;

            Phabricator.Data.Phriction phrictionDocument = phrictionStorage.Get(database, phrictionToken, browser.Session.Locale);
            if (phrictionDocument != null)
            {
                if (string.IsNullOrWhiteSpace(phrictionDocument.Content)) return;

                Parsers.Remarkup.RemarkupParserOutput remarkupPerserOutput;
                ConvertRemarkupToHTML(database, phrictionDocument.Path, phrictionDocument.Content, out remarkupPerserOutput, false);

                foreach (Parsers.Remarkup.Rules.RemarkupRule remarkupToken in GetAllRemarkupTokens(remarkupPerserOutput.TokenList))
                {
                    Parsers.Remarkup.Rules.RuleReferenceFile referencedFile = remarkupToken as Parsers.Remarkup.Rules.RuleReferenceFile;
                    Parsers.Remarkup.Rules.RuleHyperLink hyperlink = remarkupToken as Parsers.Remarkup.Rules.RuleHyperLink;

                    // check if referenced file exists
                    if (referencedFile != null)
                    {
                        bool fileExists;

                        if (referencedFile.FileID < 0)
                        {
                            fileExists = stageStorage.Get(database, browser.Session.Locale).Any(stageData => stageData.ObjectID == referencedFile.FileID);
                        }
                        else
                        {
                            fileExists = fileStorage.GetByID(database, referencedFile.FileID, true) != null;
                        }

                        if (fileExists == false)
                        {
                            if (invalidFiles.TryGetValue(referencedFile.FileID, out invalidFileReferences) == false)
                            {
                                invalidFileReferences = new List<string>();
                                invalidFiles[referencedFile.FileID] = invalidFileReferences;
                            }

                            if (invalidFileReferences.Contains(phrictionDocument.Path) == false)
                            {
                                invalidFileReferences.Add(phrictionDocument.Path);
                            }
                        }
                    }
                    else
                    // check if hyperlink is valid
                    if (hyperlink != null && hyperlink.InvalidHyperlink)
                    {
                        if (invalidHyperlinks.TryGetValue(hyperlink.Text, out invalidHyperlinkReferences) == false)
                        {
                            invalidHyperlinkReferences = new List<string>();
                            invalidHyperlinks[hyperlink.Text] = invalidHyperlinkReferences;
                        }

                        if (invalidHyperlinkReferences.Contains(phrictionDocument.Path) == false)
                        {
                            invalidHyperlinkReferences.Add(phrictionDocument.Path);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Downloads recursively all underlying Phriction documents for a given Phriction document
        /// </summary>
        /// <param name="database">Phabrico database</param>
        /// <param name="phrictionToken">Token of Phriction document to be analyzed</param>
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
