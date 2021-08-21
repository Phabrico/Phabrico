using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Phabrico.Miscellaneous
{
    /// <summary>
    /// Class for translation functionality
    /// </summary>
    public class Locale
    {
        /// <summary>
        /// Dictionaries which recently have been used to translate Phabrico
        /// key = locale
        /// value = translation dictionaries (key = english text;  value = translation)
        /// </summary>
        private static Dictionary<string,DictionarySafe<string,string>> dictionariesInUse = new Dictionary<string, DictionarySafe<string, string>>();

        /// <summary>
        /// Locale.css stylesheet is a special CSS stylesheet: it will be merged into the HTML page for speed improvement during the loading of the webpage
        /// It's a static variable, so it will only be loaded once
        /// </summary>
        private static Http.Response.StyleSheet localeCss = new Http.Response.StyleSheet(null, null, "locale.css");

        /// <summary>
        /// Merges the locale css from the Stylesheets/locale.css into the HTML.
        /// The css-calculated layout will be immediately shown correctly instead of loading the locale.css by means of a link tag
        /// </summary>
        /// <param name="content">HTML content</param>
        /// <param name="locale">Language code</param>
        /// <returns></returns>
        public static string MergeLocaleCss(string content, string locale)
        {
            string noLocaleStyleProperties = localeCss.GetCssProperties("*");
            string currentLocaleStyleProperties = localeCss.GetCssProperties("[data-locale=\"" + locale + "\"]");
            
            string localeStyledHtml = content.Replace("</head>", 
                                              "<style>\n"
                                              + noLocaleStyleProperties
                                              + "\n"
                                              + currentLocaleStyleProperties
                                              + "\n</style></head>");

            return localeStyledHtml;
        }

        /// <summary>
        /// Returns a dictionary based on a PO file for a given locale
        /// </summary>
        /// <param name="locale">Language to be used for translation</param>
        /// <returns>Dictionary where key contains the English key translations and value contains the visible translations</returns>
        public static DictionarySafe<string, string> ReadLocaleFile(string locale)
        {
            if (locale == null)
            {
                return new DictionarySafe<string, string>();
            }

            if (dictionariesInUse.ContainsKey(locale) == false)
            {
                dictionariesInUse[locale] = new DictionarySafe<string, string>();

                // collect all assemblies (Phabrico + plugin dll's)
                List<Assembly> assemblies = new List<Assembly>();
                assemblies.Add(Assembly.GetExecutingAssembly());  // phabrico executable
                assemblies.AddRange(Http.Server.Plugins.Select(plugin => plugin.Assembly));

                // loop through all assemblies
                foreach (Assembly assembly in assemblies)
                {
                    string[] dictionaryNames = assembly.GetManifestResourceNames()
                                                       .Where(resourceName => resourceName.StartsWith("Phabrico.Locale.") 
                                                                           || resourceName.StartsWith("Phabrico.Plugin.Locale.")
                                                             )
                                                       .ToArray();

                    string dictionaryName = dictionaryNames.FirstOrDefault(resourceName => resourceName.EndsWith(locale + ".po", System.StringComparison.InvariantCultureIgnoreCase));
                    if (dictionaryName == null)
                    {
                        dictionaryName = dictionaryNames.FirstOrDefault(resourceName => resourceName.EndsWith("en.po", System.StringComparison.InvariantCultureIgnoreCase));
                    }

                    if (dictionaryName == null)
                    {
                        continue;
                    }

                    using (Stream stream = assembly.GetManifestResourceStream(dictionaryName))
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            string content = reader.ReadToEnd();

                            DictionarySafe<string, string> currentDictionary = RegexSafe.Matches(content, "^msgid +\"([^\"]*)\"\r?\nmsgstr +\"([^\"]*)", 
                                                                                                          RegexOptions.Multiline)
                                                                                        .OfType<Match>()
                                                                                        .GroupBy(g => g.Groups[1].Value)
                                                                                        .Select(g => g.FirstOrDefault())
                                                                                        .ToDictionary( key => key.Groups[1].Value, 
                                                                                                       value => value.Groups[2].Value
                                                                                                     );

                            foreach (KeyValuePair<string, string> translation in currentDictionary)
                            {
                                dictionariesInUse[locale][translation.Key] = translation.Value;
                            }
                        }
                    }
                }
            }

            return dictionariesInUse[locale];
        }

        /// <summary>
        /// Translates HTML content into a given locale
        /// </summary>
        /// <param name="htmlContent">HTML content to be translated</param>
        /// <param name="locale">Language to be used for translation</param>
        /// <returns>Translated HTML file</returns>
        public static string TranslateHTML(string htmlContent, string locale)
        {
            List<string> missingMsgIds;

            return TranslateHTML(htmlContent, locale, out missingMsgIds);
        }

        /// <summary>
        /// Translates HTML content into a given locale
        /// </summary>
        /// <param name="htmlContent">HTML content to be translated</param>
        /// <param name="locale">Language to be used for translation</param>
        /// <param name="missingMsgIds">Contains the msgids that need to be translated but couldn't be found in the po locale files</param>
        /// <returns>Translated HTML file</returns>
        public static string TranslateHTML(string htmlContent, string locale, out List<string> missingMsgIds)
        {
            DictionarySafe<string,string> currentDictionary = ReadLocaleFile(locale);
            missingMsgIds = new List<string>();

            // ignore script and style blocks in HTML for translation of HTML content
            string htmlWithoutScriptContent = htmlContent;
            MatchCollection scriptBlocks = RegexSafe.Matches(htmlContent, @"(?<=\<script[^>]*\>).*?(?=\<\/script\>)", RegexOptions.Singleline);
            foreach (Match scriptBlock in scriptBlocks.OfType<Match>().OrderByDescending(m => m.Index))
            {
                htmlWithoutScriptContent = htmlWithoutScriptContent.Substring(0, scriptBlock.Index)
                                         + new string(' ', scriptBlock.Length)
                                         + htmlWithoutScriptContent.Substring(scriptBlock.Index + scriptBlock.Length);
            }

            MatchCollection styleBlocks = RegexSafe.Matches(htmlContent, @"(?<=\<style[^>]*\>).*?(?=\<\/style\>)", RegexOptions.Singleline);
            foreach (Match styleBlock in styleBlocks.OfType<Match>().OrderByDescending(m => m.Index))
            {
                htmlWithoutScriptContent = htmlWithoutScriptContent.Substring(0, styleBlock.Index)
                                         + new string(' ', styleBlock.Length)
                                         + htmlWithoutScriptContent.Substring(styleBlock.Index + styleBlock.Length);
            }

            // return all content between HTML tags that should be translated
            var itemsToBeTranslated = RegexSafe.Matches(htmlWithoutScriptContent, "[>]([^<)][^<]+)[<]")
                     .OfType<Match>()
                     .Select(m => new {
                         Match = m.Groups[1],
                         Content = m.Groups[1]
                                    .Value
                                    .Trim(' ', '\r', '\n', '\t')
                     })
                     .Where(m => m.Content.Any()
                              && m.Content.Equals("&#x200B;", System.StringComparison.OrdinalIgnoreCase) == false
                              && m.Content.StartsWith("@{") == false
                              && m.Content.StartsWith("}@") == false
                              && m.Content.EndsWith("}@") == false
                              && (m.Content.StartsWith("@@") == false || m.Content.EndsWith("@@") == false)
                              && m.Content.Equals("T@@TASK-ID@@") == false
                              && m.Content.StartsWith("&nbsp") == false
                              && m.Content.StartsWith("&gt") == false
                              && m.Content.StartsWith("&lt") == false
                              && m.Content.StartsWith("-->") == false
                           )
                     .ToArray();

            // translate HTML
            foreach (var itemToBeTranslated in itemsToBeTranslated.OrderByDescending(item => item.Match.Index))
            {
                string untranslated = itemToBeTranslated.Content;
                int ifStatementPosition = untranslated.IndexOf("@{IF");
                string ifStatement = "";
                if (ifStatementPosition >= 0)
                {
                    ifStatement = untranslated.Substring(ifStatementPosition);
                    untranslated = untranslated.Substring(0, ifStatementPosition).Trim(' ', '\t', '\r', '\n');
                }

                string translation = currentDictionary[untranslated];
                if (translation != null)
                {
                    htmlContent = htmlContent.Substring(0, itemToBeTranslated.Match.Index)
                            + translation
                            + ifStatement
                            + htmlContent.Substring(itemToBeTranslated.Match.Index + itemToBeTranslated.Match.Length);
                }
                else
                {
                    missingMsgIds.Add(untranslated);

                    Logging.WriteError(null, "### No translation found for \"{0}\"", untranslated);
                }
            }

            // translate access keys in buttons
            MatchCollection buttonTags = RegexSafe.Matches(htmlContent, "[<]button[^>]*data-accesskey=[\"']([^\"'>]*).");
            foreach (Match buttonTag in buttonTags.OfType<Match>().OrderByDescending(m => m.Index))
            {
                bool noTranslationFound;
                string translation = TranslateText(buttonTag.Groups[1].Value, locale, out noTranslationFound);

                htmlContent = htmlContent.Substring(0, buttonTag.Groups[1].Index)
                            + translation
                            + htmlContent.Substring(buttonTag.Groups[1].Index + buttonTag.Groups[1].Length);

                if (noTranslationFound)
                {
                    missingMsgIds.Add(buttonTag.Groups[1].Value);
                }
            }

            // translate placeholders in input tags
            MatchCollection inputTags = RegexSafe.Matches(htmlContent, "[<]input[^>]*placeholder=\"([^\">]*).");
            foreach (Match inputTag in inputTags.OfType<Match>().OrderByDescending(m => m.Index))
            {
                bool noTranslationFound;
                string translation = TranslateText(inputTag.Groups[1].Value, locale, out noTranslationFound);

                htmlContent = htmlContent.Substring(0, inputTag.Groups[1].Index)
                            + TranslateText(inputTag.Groups[1].Value, locale)
                            + htmlContent.Substring(inputTag.Groups[1].Index + inputTag.Groups[1].Length);

                if (noTranslationFound)
                {
                    missingMsgIds.Add(inputTag.Groups[1].Value);
                }
            }

            // translate strings in javascript
            scriptBlocks = RegexSafe.Matches(htmlContent, @"(?<=\<script[^>]*\>).*?(?=\<\/script\>)", RegexOptions.Singleline);
            foreach (Match scriptBlock in scriptBlocks.OfType<Match>().OrderByDescending(m => m.Index))
            {
                htmlContent = htmlContent.Substring(0, scriptBlock.Index)
                            + TranslateJavascript(scriptBlock.Value, currentDictionary, ref missingMsgIds)
                            + htmlContent.Substring(scriptBlock.Index + scriptBlock.Length);
            }

            // return translated content
            return htmlContent;
        }

        /// <summary>
        /// Translates the Locale.Translate calls according to the given locale
        /// </summary>
        /// <param name="javascript">Javascript code to be translated</param>
        /// <param name="locale">Language code</param>
        /// <returns>Javascript with the Locale.Translate calls replaced by translated strings</returns>
        public static string TranslateJavascript(string javascript, string locale)
        {
            DictionarySafe<string,string> currentDictionary = ReadLocaleFile(locale);
            List<string> missingMsgIds = new List<string>();
            return TranslateJavascript(javascript, currentDictionary, ref missingMsgIds);
        }

        private static string TranslateJavascript(string javascript, DictionarySafe<string,string> currentDictionary, ref List<string> missingMsgIds)
        {
            string translatedJavascript = javascript;

            Match[] localeTranslateCalls = RegexSafe.Matches(javascript, 
                                                            @"Locale[.]Translate[(]    # start
                                                                (                      # begin of content
                                                                  (?>                  # now match...
                                                                      [^()]            # any characters except braces
                                                                  |                    # or
                                                                      \((?<DEPTH>)     # a (, increasing the depth counter
                                                                  |                    # or
                                                                      \)(?<-DEPTH>)    # a ), decreasing the depth counter
                                                                  )*                   # any number of times
                                                                  (?(DEPTH)(?!))       # until the depth counter is zero again
                                                                )                      # end of content
                                                              [)]                      # end
                                                              ", RegexOptions.IgnorePatternWhitespace)
                                                   .OfType<Match>()
                                                   .ToArray();

            foreach (Match localeTranslateCall in localeTranslateCalls.OrderByDescending(m => m.Index))
            {
                string textToTranslate = localeTranslateCall.Groups[1].Value.Trim(' ', '\t');
                if (RegexSafe.IsMatch(textToTranslate, "^[\"'].*[\"']$", RegexOptions.Singleline))
                {
                    textToTranslate = textToTranslate.Substring(1, textToTranslate.Length - 2);

                    string translation = currentDictionary[textToTranslate];
                    if (translation == null)
                    {
                        translation = textToTranslate;

                        missingMsgIds.Add(textToTranslate);


                        Logging.WriteError(null, "### No translation found for \"{0}\"", textToTranslate);
                    }

                    translatedJavascript = translatedJavascript.Substring(0, localeTranslateCall.Index)
                            + "\"" + translation + "\""
                            + translatedJavascript.Substring(localeTranslateCall.Index + localeTranslateCall.Length);
                }
            }

            return translatedJavascript;
        }

        /// <summary>
        /// Translates a string into a given locale
        /// </summary>
        /// <param name="text">String to be translated</param>
        /// <param name="locale">Language to be used for translation</param>
        /// <param name="noTranslationFound">True if no tranlsation was found for the given text</param>
        /// <returns>Translated string</returns>
        public static string TranslateText(string text, string locale, out bool noTranslationFound)
        {
            DictionarySafe<string, string> currentDictionary = ReadLocaleFile(locale);

            noTranslationFound = false;
            string translation = currentDictionary[text];
            if (translation == null)
            {
                translation = text;
                noTranslationFound = true;
                Logging.WriteError(null, "### No translation found for \"{0}\"", text);
            }

            return translation;
        }

        /// <summary>
        /// Translates a string into a given locale
        /// </summary>
        /// <param name="text">String to be translated</param>
        /// <param name="locale">Language to be used for translation</param>
        /// <returns>Translated string</returns>
        public static string TranslateText(string text, string locale)
        {
            bool noTranslationFound;
            return TranslateText(text, locale, out noTranslationFound);
        }
    }
}
