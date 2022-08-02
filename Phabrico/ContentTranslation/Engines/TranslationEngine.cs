using Phabrico.Miscellaneous;
using Phabrico.Parsers.Base64;
using Phabrico.Parsers.BrokenXML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Phabrico.ContentTranslation.Engines
{
    public abstract class TranslationEngine
    {
        public class Translation
        {
            public string OriginalText { get; private set; }
            public string TranslatedText { get; set; }

            public Translation(string original, string translation)
            {
                OriginalText = original;
                TranslatedText = translation;
            }
        }

        /// <summary>
        /// API key for web-based translators
        /// </summary>
        protected string APIKey { get; private set;}

        /// <summary>
        /// Dictionary used in file-based translators.
        /// The key is a MD5-based key, which identifies the part to be translated
        /// The value contains a Translation class, which contains the text to be translated and the translation.
        /// If the translator is a non-file-based translator, this property should be null.
        /// </summary>
        protected virtual Dictionary<string, Translation> TranslationalDictionary { get; set; } = null;

        private readonly List<string> untranslatableTextParts = new List<string>();

        /// <summary>
        /// True if a file import/export functionality should be provided
        /// </summary>
        public bool IsFileBasedTranslationService
        { 
            get
            {
                return TranslationalDictionary != null;
            }
        }

        /// <summary>
        /// True if a connection is established over the internet.
        /// If this is set to true, a confirmation messagebox will be shown just before the translation starts.
        /// It will mention that you should not transfer sensitive information over the internet
        /// </summary>
        public abstract bool IsRemoteTranslationService { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="apiKey">API key to be used for online translation</param>
        public TranslationEngine(string apiKey)
        {
            APIKey = apiKey;
        }

        /// <summary>
        /// Translates some content from one language to another language
        /// </summary>
        /// <param name="sourceLanguage">Language of content</param>
        /// <param name="destinationLanguage">Language of translated content</param>
        /// <param name="content">Content to be translated</param>
        /// <param name="origin">Location where the content can be found (i.e. a token)</param>
        /// <returns>Translated content</returns>
        protected abstract string Translate(string sourceLanguage, string destinationLanguage, string content, string origin);

        /// <summary>
        /// If the translation enigne is a file-based translator, this method will return the file content in a base64 encoding
        /// so it can be readily downloaded from a web browser
        /// </summary>
        /// <returns>Stream containing base64 encoded file data</returns>
        public virtual Base64EIDOStream GetBase64EIDOStream()
        {
            return null;
        }

        /// <summary>
        /// If the translation enigne is a file-based translator, this method will return the content-type of the file
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public virtual string GetContentType()
        {
            if (IsFileBasedTranslationService == false) return null;

            throw new NotImplementedException();
        }

        /// <summary>
        /// If the translation enigne is a file-based translator, this method will return the name of the file
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public virtual string GetFileName()
        {
            if (IsFileBasedTranslationService == false) return null;

            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns a translation engine which is specificied by name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="apiKey"></param>
        /// <returns></returns>
        public static TranslationEngine GetTranslationEngine(string name, string apiKey)
        {
            // dummy translator is only used for unit testing
            if (Http.Server.UnitTesting == false && name.Equals("dummy")) return null;

            Type translatorType = Assembly.GetExecutingAssembly()
                                          .GetTypes()
                                          .Where(type => typeof(TranslationEngine).IsAssignableFrom(type))
                                          .FirstOrDefault(type => {
                                              TranslationAttribute translationAttribute = type.GetCustomAttribute(typeof(TranslationAttribute)) as TranslationAttribute;
                                              if (translationAttribute == null) return false;
                                              if (translationAttribute.Name.Equals(name)) return true;
                                              return false;
                                          });
            if (translatorType == null) return null;

            ConstructorInfo translatorConstructor = translatorType.GetConstructor(new Type[] { typeof(string) });
            return translatorConstructor.Invoke(new object[] { apiKey }) as TranslationEngine;
        }

        /// <summary>
        /// Imports a translation file
        /// </summary>
        /// <param name="base64FileData">Base64-encoded file content</param>
        /// <param name="sourceLanguage">Current language</param>
        /// <param name="targetLanguage">Language to translate to</param>
        public virtual void ImportFile(string base64FileData, string sourceLanguage, string targetLanguage)
        {
        }

        /// <summary>
        /// Imports a translation dictionary, which was cretaed by the ImportFile method
        /// </summary>
        /// <param name="targetLanguage"></param>
        /// <param name="database"></param>
        /// <param name="browser"></param>
        /// <param name="phrictionDocument"></param>
        /// <param name="translatedTitle"></param>
        /// <param name="translatedContent"></param>
        /// <returns>True if more underlyings documents need to be translated</returns>
        public virtual bool ImportTranslationDictionary(string targetLanguage, Storage.Database database, Http.Browser browser, Phabricator.Data.Phriction phrictionDocument, out string translatedTitle, out string translatedContent)
        {
            translatedTitle = "";
            translatedContent = "";
            return false;
        }

        /// <summary>
        /// Converts a BrokenXML string into an XML which can be used for translation
        /// </summary>
        /// <param name="brokenXml"></param>
        /// <returns></returns>
        private string PrepareTranslatableContent(string brokenXml)
        {
            BrokenXmlParser brokenXmlParser = new BrokenXmlParser();
            List<BrokenXmlToken> tokens = brokenXmlParser.Parse(brokenXml).ToList();

            string translatableContent = "";
            for (int t=0; t<tokens.Count; t++)
            {
                BrokenXmlText brokenXmlText = tokens[t] as BrokenXmlText;
                if (brokenXmlText != null)
                {
                    translatableContent += brokenXmlText.Value;
                    continue;
                }

                BrokenXmlOpeningTag brokenXmlOpeningTag = tokens[t] as BrokenXmlOpeningTag;
                if (brokenXmlOpeningTag != null)
                {
                    int depth = 1;
                    string innerContent = "";
                    for (int c = t+1; c < tokens.Count; c++)
                    {
                        if (tokens[c].GetType() == typeof(BrokenXmlClosingTag))
                        {
                            BrokenXmlClosingTag brokenXmlClosingTag = tokens[c] as BrokenXmlClosingTag;
                            if (brokenXmlClosingTag != null)
                            {
                                depth--;
                                if (depth == 0)
                                {
                                    if (XmlTagTranslatable(brokenXmlOpeningTag))
                                    {
                                        translatableContent += brokenXmlOpeningTag.Value
                                                             + PrepareTranslatableContent(innerContent)
                                                             + brokenXmlClosingTag.Value;

                                        t = c;
                                    }
                                    else
                                    {
                                        string untranslatable = brokenXmlOpeningTag.Value
                                                              + innerContent
                                                              + brokenXmlClosingTag.Value;

                                        untranslatableTextParts.Add(untranslatable);

                                        brokenXmlOpeningTag.Name = "UT";  // untranslatable text
                                        brokenXmlClosingTag.Name = brokenXmlOpeningTag.Name;
                                        brokenXmlOpeningTag.Attributes.Clear();
                                        brokenXmlOpeningTag.Attributes.Add(new BrokenXmlAttribute()
                                        {
                                            Name = "i",
                                            Value = untranslatableTextParts.Count.ToString()
                                        });

                                        while (t + 1 != c)
                                        {
                                            tokens.RemoveAt(t + 1);
                                            c--;
                                        }

                                        translatableContent += brokenXmlOpeningTag.ToString()
                                                             + brokenXmlClosingTag.ToString();
                                    }

                                    break;
                                }
                            }
                        }
                        else
                        if (tokens[c].GetType() == typeof(BrokenXmlOpeningTag))
                        {
                            depth++;
                        }

                        innerContent += tokens[c].Value;
                    }

                    continue;
                }

                BrokenXmlAutoCloseTag brokenXmlAutoCloseTag = tokens[t] as BrokenXmlAutoCloseTag;
                if (brokenXmlAutoCloseTag != null)
                {
                    translatableContent += brokenXmlAutoCloseTag.Name;
                    continue;
                }
            }

            return translatableContent;
        }

        /// <summary>
        /// Translates a BrokenXML string from one language into another language
        /// </summary>
        /// <param name="sourceLanguage"></param>
        /// <param name="destinationLanguage"></param>
        /// <param name="brokenXml"></param>
        /// <param name="origin"></param>
        /// <returns></returns>
        public string TranslateXML(string sourceLanguage, string destinationLanguage, string brokenXml, string origin)
        {
            untranslatableTextParts.Clear();

            string translatableContent = PrepareTranslatableContent(brokenXml);
            string translatedContent = Translate(sourceLanguage, destinationLanguage, translatableContent, origin);

            Match[] untranslatedMatches = RegexSafe.Matches(translatedContent, "<UT i=\"([0-9]+)\"></UT>", RegexOptions.Singleline)
                                                   .OfType<Match>()
                                                   .OrderByDescending(match => match.Index)
                                                   .ToArray();

            foreach (Match untranslatedMatch in untranslatedMatches)
            {
                int untranslatedMatchIndexer = Int32.Parse(untranslatedMatch.Groups[1].Value);

                translatedContent = translatedContent.Substring(0, untranslatedMatch.Index)
                                  + untranslatableTextParts[untranslatedMatchIndexer - 1]
                                  + translatedContent.Substring(untranslatedMatch.Index + untranslatedMatch.Length);
            }

            return translatedContent;
        }

        /// <summary>
        /// Translates a string from one language into another language
        /// </summary>
        /// <param name="sourceLanguage"></param>
        /// <param name="targetLanguage"></param>
        /// <param name="text"></param>
        /// <param name="origin"></param>
        /// <returns></returns>
        public virtual string TranslateText(string sourceLanguage, string targetLanguage, string text, string origin)
        {
            return TranslateXML(sourceLanguage, targetLanguage, text, origin);
        }

        /// <summary>
        /// Verifies if the content of an XML tag should be translated
        /// </summary>
        /// <param name="brokenXmlOpeningTag"></param>
        /// <returns></returns>
        private bool XmlTagTranslatable(BrokenXmlOpeningTag brokenXmlOpeningTag)
        {
            if (brokenXmlOpeningTag.Name.Equals("BT") ||
                brokenXmlOpeningTag.Name.Equals("IN") ||
                brokenXmlOpeningTag.Name.Equals("LT") ||
                brokenXmlOpeningTag.Name.Equals("P") ||
                brokenXmlOpeningTag.Name.Equals("US") ||
                brokenXmlOpeningTag.Name.Equals("WS"))
            {
                return false;
            }

            return true;
        }
    }
}
