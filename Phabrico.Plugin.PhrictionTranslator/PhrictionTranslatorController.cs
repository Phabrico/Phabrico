using Newtonsoft.Json;
using Phabrico.ContentTranslation.Engines;
using Phabrico.Controllers;
using Phabrico.Http;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;
using Phabrico.Parsers.Remarkup;
using Phabrico.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Phabrico.Plugin
{
    /// <summary>
    /// Represents the controller for all the PhrictionTranslator functionalities
    /// </summary>
    public class PhrictionTranslatorController : PluginController
    {
        public class TranslationEngineRecord
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public bool Selected { get; set; }

            public bool APIKeyVisible { get; set; }
        }

        /// <summary>
        /// This method is fired when the user clicks on the 'Translate document' in the Phriction action pane.
        /// 
        /// This method will also check if there are any underlying Phriction documents under the current Phriction document.
        /// If so, a 'Confirm' JSON request will be sent instead representing a question to the user if the underlying documents
        /// should also be validated.
        /// The response for this 'Confirm' JSON request is handled by the HttpPostValidationConfirmation method
        /// After this HttpPostValidationConfirmation reponse is handled in the browser, this method is executed again
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/PhrictionTranslator")]
        public JsonMessage HttpPostTranslateDocument(Http.Server httpServer, Browser browser, string[] parameters)
        {
            Phabrico.Storage.Account accountStorage = new Phabrico.Storage.Account();
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

                if (formVariables.ContainsKey("language-chosen") == false)
                {
                    string javascript = @"
                            function refreshTranslationEngine(eng) {
                                var hasAPIKey = translationEngine.querySelector('option[value=""' + eng +'""]').classList.contains('apiKey1');
                                if (hasAPIKey) {
                                    paramAPIKey.style.display = null;
                                } else {
                                    paramAPIKey.style.display = 'none';
                                }
                            }

                            refreshTranslationEngine(translationEngine.value);
                            translationEngine.addEventListener('change', function(e) { refreshTranslationEngine(translationEngine.value); });

                            function refreshTargetLanguages(src) {
                                var targetLanguages = Array.prototype.slice.call( targetLanguage.options, 0 );
                                var hiddenLanguage = targetLanguages.filter(function(f) { return f.style.display == 'none' && f.value != ''; });
                                if (hiddenLanguage.length > 0) {
                                    hiddenLanguage = hiddenLanguage[0];
                                    hiddenLanguage.style.display = null;
                                }

                                hiddenLanguage = targetLanguages.filter(function(f) { return f.value == src; });
                                if (hiddenLanguage.length > 0) {
                                    if (targetLanguage.value == src) {
                                        targetLanguage.value = null;
                                    }
                                    hiddenLanguage = hiddenLanguage[0];
                                    hiddenLanguage.style.display = 'none';
                                }
                            }

                            refreshTargetLanguages(sourceLanguage.value);
                    ";

                    string TranslationEngineName = Locale.TranslateText("Translation engine", browser.Session.Locale);
                    string APIKey = Locale.TranslateText("API Key", browser.Session.Locale);
                    string SourceLanguage = Locale.TranslateText("Source language", browser.Session.Locale);
                    string TargetLanguage = Locale.TranslateText("Target language", browser.Session.Locale);
                    string LanguageBulgarian = Locale.TranslateText("Bulgarian", browser.Session.Locale);
                    string LanguageChinese = Locale.TranslateText("Chinese", browser.Session.Locale);
                    string LanguageCzech = Locale.TranslateText("Czech", browser.Session.Locale);
                    string LanguageDanish = Locale.TranslateText("Danish", browser.Session.Locale);
                    string LanguageDutch = Locale.TranslateText("Dutch", browser.Session.Locale);
                    string LanguageEnglish = Locale.TranslateText("English", browser.Session.Locale);
                    string LanguageEstonian = Locale.TranslateText("Estonian", browser.Session.Locale);
                    string LanguageFinnish = Locale.TranslateText("Finnish", browser.Session.Locale);
                    string LanguageFrench = Locale.TranslateText("French", browser.Session.Locale);
                    string LanguageGerman = Locale.TranslateText("German", browser.Session.Locale);
                    string LanguageGreek = Locale.TranslateText("Greek", browser.Session.Locale);
                    string LanguageHungarian = Locale.TranslateText("Hungarian", browser.Session.Locale);
                    string LanguageItalian = Locale.TranslateText("Italian", browser.Session.Locale);
                    string LanguageJapanese = Locale.TranslateText("Japanese", browser.Session.Locale);
                    string LanguageLatvian = Locale.TranslateText("Latvian", browser.Session.Locale);
                    string LanguageLithuanian = Locale.TranslateText("Lithuanian", browser.Session.Locale);
                    string LanguagePolish = Locale.TranslateText("Polish", browser.Session.Locale);
                    string LanguagePortuguese = Locale.TranslateText("Portuguese", browser.Session.Locale);
                    string LanguageRomanian = Locale.TranslateText("Romanian", browser.Session.Locale);
                    string LanguageRussian = Locale.TranslateText("Russian", browser.Session.Locale);
                    string LanguageSlovak = Locale.TranslateText("Slovak", browser.Session.Locale);
                    string LanguageSlovenian = Locale.TranslateText("Slovenian", browser.Session.Locale);
                    string LanguageSpanish = Locale.TranslateText("Spanish", browser.Session.Locale);
                    string LanguageSwedish = Locale.TranslateText("Swedish", browser.Session.Locale);

                    TranslationEngineRecord[] translationEngines = new TranslationEngineRecord[] {
                        new TranslationEngineRecord { Name = "deepl", Description = "DeepL", Selected = true  , APIKeyVisible = true},
                        new TranslationEngineRecord { Name = "dummy", Description = "Dummy", Selected = false , APIKeyVisible = false }
                    };

                    string translationEngineOptions = string.Join("",
                                                                  translationEngines.Where(engine => engine.Name.Equals("dummy") == false
                                                                                                  || Http.Server.UnitTesting == true
                                                                                          )
                                                                        .Select(engine => string.Format("<option value='{0}' class='apiKey{3}' {2}>{1}</option>",
                                                                                                           engine.Name,
                                                                                                           engine.Description,
                                                                                                           engine.Selected ? "selected" : "",
                                                                                                           engine.APIKeyVisible ? 1 : 0
                                                                                                       )
                                                                               )
                                                                 );

                    string dlgChooseLanguage = string.Format($@"
                        <input type='hidden' name='language-chosen' value='yes' />
                        <table>
                            <tr id='paramTranslationEngine'>
                                <td style='text-align:right; vertical-align: middle;'>{TranslationEngineName}:</td>
                                <td>
                                    <select name='translationEngine' id='translationEngine' class='required'>
                                        {translationEngineOptions}
                                    </select>
                                </td>
                            </tr>
                            <tr id='paramAPIKey'>
                                <td style='text-align:right; vertical-align: middle;'>{APIKey}:</td>
                                <td>
                                    <input name='apiKey' type='password' class='required' />
                                </td>
                            </tr>
                            <tr id='paramSourceLanguage'>
                                <td style='text-align:right; vertical-align: middle;'>{SourceLanguage}:</td>
                                <td>
                                    <select id='sourceLanguage' name='sourceLanguage' class='required' onchange=""refreshTargetLanguages(this.value)"">
                                        <!-- <option value='bg'>{LanguageBulgarian}</option>              -->
                                             <option value='zh'>{LanguageChinese}</option>
                                        <!-- <option value='cs'>{LanguageCzech}</option>                  -->
                                        <!-- <option value='da'>{LanguageDanish}</option>                 -->
                                             <option value='nl'>{LanguageDutch}</option>
                                             <option value='en' selected>{LanguageEnglish}</option>
                                        <!-- <option value='et'>{LanguageEstonian}</option>               -->
                                        <!-- <option value='fi'>{LanguageFinnish}</option>                -->
                                        <!-- <option value='fr'>{LanguageFrench}</option>                 -->
                                             <option value='de'>{LanguageGerman}</option>
                                        <!-- <option value='el'>{LanguageGreek}</option>                  -->
                                        <!-- <option value='hu'>{LanguageHungarian}</option>              -->
                                        <!-- <option value='it'>{LanguageItalian}</option>                -->
                                        <!-- <option value='ja'>{LanguageJapanese}</option>               -->
                                        <!-- <option value='lv'>{LanguageLatvian}</option>                -->
                                        <!-- <option value='lt'>{LanguageLithuanian}</option>             -->
                                        <!-- <option value='pl'>{LanguagePolish}</option>                 -->
                                        <!-- <option value='pt'>{LanguagePortuguese}</option>             -->
                                        <!-- <option value='ro'>{LanguageRomanian}</option>               -->
                                        <!-- <option value='ru'>{LanguageRussian}</option>                -->
                                        <!-- <option value='sk'>{LanguageSlovak}</option>                 -->
                                        <!-- <option value='sl'>{LanguageSlovenian}</option>              -->
                                             <option value='es'>{LanguageSpanish}</option>
                                        <!-- <option value='sv'>{LanguageSwedish}</option>                -->
                                    </select>
                                </td>
                            </tr>
                            <tr id='paramTargetLanguage'>
                                <td style='text-align:right; vertical-align: middle;'>{TargetLanguage}:</td>
                                <td>
                                    <select id='targetLanguage' name='targetLanguage' class='required'>
                                             <option value='' selected style='display:none'></option>
                                        <!-- <option value='bg'>{LanguageBulgarian}</option>              -->
                                             <option value='zh'>{LanguageChinese}</option>
                                        <!-- <option value='cs'>{LanguageCzech}</option>                  -->
                                        <!-- <option value='da'>{LanguageDanish}</option>                 -->
                                             <option value='nl'>{LanguageDutch}</option>
                                             <option value='en'>{LanguageEnglish}</option>
                                        <!-- <option value='et'>{LanguageEstonian}</option>               -->
                                        <!-- <option value='fi'>{LanguageFinnish}</option>                -->
                                        <!-- <option value='fr'>{LanguageFrench}</option>                 -->
                                             <option value='de'>{LanguageGerman}</option>
                                        <!-- <option value='el'>{LanguageGreek}</option>                  -->
                                        <!-- <option value='hu'>{LanguageHungarian}</option>              -->
                                        <!-- <option value='it'>{LanguageItalian}</option>                -->
                                        <!-- <option value='ja'>{LanguageJapanese}</option>               -->
                                        <!-- <option value='lv'>{LanguageLatvian}</option>                -->
                                        <!-- <option value='lt'>{LanguageLithuanian}</option>             -->
                                        <!-- <option value='pl'>{LanguagePolish}</option>                 -->
                                        <!-- <option value='pt'>{LanguagePortuguese}</option>             -->
                                        <!-- <option value='ro'>{LanguageRomanian}</option>               -->
                                        <!-- <option value='ru'>{LanguageRussian}</option>                -->
                                        <!-- <option value='sk'>{LanguageSlovak}</option>                 -->
                                        <!-- <option value='sl'>{LanguageSlovenian}</option>              -->
                                             <option value='es'>{LanguageSpanish}</option>
                                        <!-- <option value='sv'>{LanguageSwedish}</option>                -->
                                    </select>
                                </td>
                            </tr>
                        </table>
                    ");

                    jsonData = JsonConvert.SerializeObject(new
                    {
                        Status = "Prepare",
                        DialogTitle = Locale.TranslateText("Translation parameters", browser.Session.Locale),
                        DialogHTML = dlgChooseLanguage,
                        Javascript = javascript
                    });

                    return new JsonMessage(jsonData);
                }


                int nbrUnderlyingPhrictionTokens = underlyingPhrictionTokens.Count;
                if (nbrUnderlyingPhrictionTokens > 0 && PhrictionData.ConfirmState == ConfirmResponse.None)
                {
                    string message;

                    if (nbrUnderlyingPhrictionTokens == 1)
                    {
                        message = Locale.TranslateText("There is 1 underlying document. Would you like to translate this as well ?", browser.Session.Locale);
                    }
                    else
                    {
                        message = Locale.TranslateText("There are @@NBR-CHILD-DOCUMENTS@@ underlying documents. Would you like to translate these as well ?", browser.Session.Locale)
                                        .Replace("@@NBR-CHILD-DOCUMENTS@@", nbrUnderlyingPhrictionTokens.ToString());
                    }

                    jsonData = JsonConvert.SerializeObject(new
                    {
                        Status = "Confirm",
                        Message = message,
                        ResponseTarget = "includeUnderlyingDocuments"
                    });

                    return new JsonMessage(jsonData);
                }
                else
                {
                    string translationEngine = formVariables["translationEngine"];
                    string apiKey = formVariables["apiKey"];
                    TranslationEngine translator = TranslationEngine.GetTranslationEngine(translationEngine, apiKey);

                    bool isRemoteTranslationService = false;
                    if (translationEngine != null)
                    {
                        isRemoteTranslationService = translator.IsRemoteTranslationService;
                    }

                    if (isRemoteTranslationService && formVariables.ContainsKey("confirm-translation") == false)
                    {
                        string message = Locale.TranslateText("The translation will be performed by a machine translation service on the Internet. Be sure not to send any sensitive data over the Internet! Do you still want to continue ?", browser.Session.Locale)
                                               .Replace("\\n", "<BR>");

                        jsonData = JsonConvert.SerializeObject(new
                        {
                            Status = "Confirm",
                            AbortWhenDeclined = true,
                            Message = message,

                            SetFormVariables = new Dictionary<string, string>()
                            {
                                { "confirm-translation", "1" }
                            }
                        });

                        return new JsonMessage(jsonData);
                    }
                    else
                    {
                        Dictionary<int, List<string>> invalidFiles = new Dictionary<int, List<string>>();
                        Dictionary<string, List<string>> invalidHyperlinks = new Dictionary<string, List<string>>();
                        List<string> errorMessages = new List<string>();

                        string sourceLanguage = formVariables["sourceLanguage"];
                        string targetLanguage = formVariables["targetLanguage"];
                        string errorMessage = TranslateDocument(database, phrictionDocument.Token, translationEngine, apiKey, sourceLanguage, targetLanguage);
                        if (errorMessage != null)
                        {
                            errorMessages.Add(errorMessage);
                        }
                        else
                        {
                            // uncache document
                            httpServer.InvalidateNonStaticCache(phrictionDocument.Path);

                            // do we also need to validate the underlying documents ?
                            if (formVariables["includeUnderlyingDocuments"] == "Yes")
                            {
                                foreach (string underlyingPhrictionToken in underlyingPhrictionTokens)
                                {
                                    errorMessage = TranslateDocument(database, underlyingPhrictionToken, translationEngine, apiKey, sourceLanguage, targetLanguage);
                                    if (errorMessage != null)
                                    {
                                        errorMessages.Add(errorMessage);
                                    }
                                    else
                                    {
                                        // uncache document
                                        Phabricator.Data.Phriction underlyingPhrictionDocument = phrictionStorage.Get(database, underlyingPhrictionToken, browser.Session.Locale);
                                        httpServer.InvalidateNonStaticCache(underlyingPhrictionDocument.Path);
                                    }
                                }
                            }
                        }

                        // return result
                        if (errorMessages.Any())
                        {
                            jsonData = JsonConvert.SerializeObject(new
                            {
                                Status = "Finished",
                                MessageBox = new
                                {
                                    Title = Locale.TranslateText("PluginName.PhrictionTranslator", browser.Session.Locale),
                                    Message = errorMessages.FirstOrDefault()
                                }
                            });
                        }
                        else
                        {
                            jsonData = JsonConvert.SerializeObject(new
                            {
                                Status = "Finished",
                                MessageBox = new
                                {
                                    Title = Locale.TranslateText("PluginName.PhrictionTranslator", browser.Session.Locale),
                                    Message = (nbrUnderlyingPhrictionTokens == 0 || formVariables["includeUnderlyingDocuments"] == "No")
                                            ? Locale.TranslateText("Document is translated", browser.Session.Locale)
                                            : Locale.TranslateText("@@NBR-DOCUMENTS@@ documents have been translated", browser.Session.Locale)
                                                    .Replace("@@NBR-DOCUMENTS@@", (1 + nbrUnderlyingPhrictionTokens).ToString())
                                }
                            });
                        }

                        return new JsonMessage(jsonData);
                    }
                }
            }
        }

        /// <summary>
        /// Execute some validation checks (and corrections if needed) on the XML received from the translated
        /// </summary>
        /// <param name="unverfiedXML">Unverfied XML</param>
        /// <returns>Validated (and corrected) XML</returns>
        private string CorrectTranslatedXmlContent(string unverfiedXML)
        {
            // Newlines are normally BrokenXML foratted as follows: <N>[123]</N>
            // The translator might return newlines as follows <N>[123</N>]
            Match[] invalidNewLines = RegexSafe.Matches(unverfiedXML, "<N>\\[([0-9]+)</N>\\]", RegexOptions.Singleline)
                                               .OfType<Match>()
                                               .OrderByDescending(match => match.Index)
                                               .ToArray();
            foreach (Match invalidNewLine in invalidNewLines)
            {
                unverfiedXML = unverfiedXML.Substring(0, invalidNewLine.Index)
                             + "<N>[" + invalidNewLine.Groups[1].Value + "]</N>"
                             + unverfiedXML.Substring(invalidNewLine.Index + invalidNewLine.Length);
            }

            return unverfiedXML;
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

        /// <summary>
        /// Connects to a translation engine to get a translation for a specified Phriction document
        /// </summary>
        /// <param name="database">Phabrico database</param>
        /// <param name="phrictionToken">Token of Phriction document that needs to be translated</param>
        /// <param name="translationEngine">Name of the translation engine</param>
        /// <param name="apiKey">Authntication key for translation engine</param>
        /// <param name="sourceLanguage">Language in which the document is currently written</param>
        /// <param name="targetLanguage">Language in which the document should be translated to</param>
        /// <returns></returns>
        private string TranslateDocument(Database database, string phrictionToken, string translationEngine, string apiKey, string sourceLanguage, string targetLanguage)
        {
            TranslationEngine translator = TranslationEngine.GetTranslationEngine(translationEngine, apiKey);
            if (translator == null) return "Invalid translation engine";

            Storage.Phriction phrictionStorage = new Storage.Phriction();
            Storage.Stage stageStorage = new Storage.Stage();
            RemarkupEngine remarkup = new RemarkupEngine();
            RemarkupParserOutput remarkupParserOutput;
            Phabricator.Data.Phriction phrictionDocument = phrictionStorage.Get(database, phrictionToken, Language.NotApplicable);

            if (string.IsNullOrWhiteSpace(phrictionDocument.Content)) return null;

            Language originalLanguage = browser.Session.Locale;
            browser.Session.Locale = targetLanguage;

            try
            {
                string html = remarkup.ToHTML(null, database, browser, "/", phrictionDocument.Content, out remarkupParserOutput, false);
                string xmlData = remarkupParserOutput.TokenList.ToXML(database, browser, "/");

                string translatedTitle = translator.TranslateText(sourceLanguage, targetLanguage, phrictionDocument.Name);
                string translatedXmlContent = translator.TranslateXML(sourceLanguage, targetLanguage, xmlData);
                string correctedTranslatedXmlContent = CorrectTranslatedXmlContent(translatedXmlContent);
                string translatedContent = remarkupParserOutput.TokenList.FromXML(database, browser, "/", correctedTranslatedXmlContent, true);

                Storage.Content content = new Content(database);
                content.AddTranslation(phrictionDocument.Token, (Language)targetLanguage, translatedTitle, translatedContent);

                // retrieve new referenced fileobjects and relink them to the translated phrictionDocument
                remarkup.ToHTML(null, database, browser, "/", translatedContent, out remarkupParserOutput, false);
                database.ClearAssignedTokens(phrictionToken, targetLanguage);
                foreach (Phabricator.Data.PhabricatorObject linkedPhabricatorObject in remarkupParserOutput.LinkedPhabricatorObjects)
                {
                    database.AssignToken(phrictionDocument.Token, linkedPhabricatorObject.Token, targetLanguage);
                }

                // clean up old translations
                content.DeleteUnreferencedTranslatedObjects();

                // remove staged translation (if any)
                Phabricator.Data.Phriction stagedPhrictionDocument = stageStorage.Get<Phabricator.Data.Phriction>(database, phrictionToken, (Language)targetLanguage);
                if (stagedPhrictionDocument != null && stagedPhrictionDocument.Language.Equals(targetLanguage))
                {
                    stageStorage.Remove(database, browser, stagedPhrictionDocument, (Language)targetLanguage);
                }
                return null;
            }
            catch (System.Exception e)
            {
                return e.Message;
            }
            finally
            {
                browser.Session.Locale = originalLanguage;
            }
        }
    }
}
