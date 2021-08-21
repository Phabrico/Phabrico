﻿using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Phabrico.Http.Response
{
    /// <summary>
    /// Represents the HTTP response to form a HTML page
    /// </summary>
    public class HtmlPage : HttpFound
    {
        /// <summary>
        /// Represents the application's theme in which the HTML page should be shown (i.e. dark, light)
        /// </summary>
        public string Theme { get; set; }

        /// <summary>
        /// Initializes a new HTTP object which identifies a HTML page
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="url"></param>
        public HtmlPage(Http.Server httpServer, Browser browser, string url)
            : base(httpServer, browser, url)
        {
        }

        /// <summary>
        /// Return the OPTION HTML codes for the language dropdown in the 'Change Language' dialog
        /// </summary>
        /// <param name="currentLanguageCode"></param>
        /// <returns>A string of OPTION tags containing all available languages</returns>
        public string GetLanguageOptions(string currentLanguageCode)
        {
            // dictionary holding native language name (key) and its language code (value)
            Dictionary<string,string> availableLanguages =  Assembly.GetExecutingAssembly()
                                                                    .GetManifestResourceNames()
                                                                    .Where(resourceName => resourceName.StartsWith("Phabrico.Locale.Phabrico_", System.StringComparison.OrdinalIgnoreCase))
                                                                    .Select(resourceName => resourceName.Substring("Phabrico.Locale.Phabrico_".Length))
                                                                    .Select(resourceName => resourceName.Substring(0, resourceName.Length - ".po".Length))
                                                                    .ToDictionary(key => char.ToUpper((new CultureInfo(key)).NativeName[0]) + (new CultureInfo(key)).NativeName.Substring(1),
                                                                                  value => value
                                                                                 );

            string htmlResult = "";

            // currentLanguage is unknown language -> take English instead as currentLanguage
            if (availableLanguages.Values.Contains(currentLanguageCode) == false)
            {
                currentLanguageCode = "en";
            }

            foreach (string language in availableLanguages.Keys.OrderBy(lang => lang))
            {
                if (availableLanguages[language].Equals(currentLanguageCode))
                {
                    htmlResult += string.Format("<option selected='selected' value='{0}'>&nbsp;{1}</option>\n", availableLanguages[language], language);
                }
                else
                {
                    htmlResult += string.Format("<option value='{0}'>&nbsp;{1}</option>\n", availableLanguages[language], language);
                }
            }

            return htmlResult;
        }
    }
}
