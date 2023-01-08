using Phabrico.Miscellaneous;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Phabrico.ContentTranslation.Engines
{
    /// <summary>
    /// Translation engine used for unit testing
    /// </summary>
    [Translation(Name = "dummy")]
    public class DummyTranslationEngine : TranslationEngine
    {
        public override bool IsRemoteTranslationService { get; } = false;

        public static Dictionary<string,string> Translations;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="apiKey">API key to be used for online translation</param>
        public DummyTranslationEngine(string apiKey)
            : base(apiKey)
        {
        }

        /// <summary>
        /// Translates some content from one language to another language
        /// </summary>
        /// <param name="sourceLanguage">Language of content</param>
        /// <param name="destinationLanguage">Language of translated content</param>
        /// <param name="content">Content to be translated</param>
        /// <param name="previouslyTranslatedContent">Translated content. Can be empty if this is the first translation time or it can contain a translation from a previous call</param>
        /// <param name="origin">Location where the content can be found (i.e. a token)</param>
        /// <returns>Translated content</returns>
        protected override string Translate(string sourceLanguage, string destinationLanguage, string content, string previouslyTranslatedContent, string origin)
        {
            Match xmlTag = RegexSafe.Match(content, "^<[^>]+>", RegexOptions.None);
            string unprocessedContent = RegexSafe.Replace(content, "^<[^>]+>", "");
            string result = xmlTag.Value;
            while (unprocessedContent.Any())
            {
                string translationKey = Translations.Keys.OrderByDescending(key => key.Length)
                                                         .FirstOrDefault(key => unprocessedContent.StartsWith(key));
                if (string.IsNullOrEmpty(translationKey))
                {
                    result += unprocessedContent[0];
                    unprocessedContent = unprocessedContent.Substring(1);
                }
                else
                {
                    result += Translations[translationKey];
                    unprocessedContent = unprocessedContent.Substring(translationKey.Length);
                }

                while (true)
                {
                    xmlTag = RegexSafe.Match(unprocessedContent, "^<[^>]+>", RegexOptions.None);
                    if (xmlTag.Success == false) break;

                    result += xmlTag.Value;
                    unprocessedContent = unprocessedContent.Substring(xmlTag.Length);
                }
            }

            return result;
        }
    }
}
