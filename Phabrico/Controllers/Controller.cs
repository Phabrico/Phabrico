using Phabrico.Miscellaneous;
using Phabrico.Parsers.Remarkup;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace Phabrico.Controllers
{
    /// <summary>
    /// Abstract class for Phabrico controllers
    /// </summary>
    public abstract class Controller
    {
        /// <summary>
        /// Sub-class for correcting invalid header-tree in the generated HTML, converted from Remarkup.
        /// For example: a H4 tag should appear after a H3 or another H4. If it appears after a H2 tag, the H4 will be corrected to H3
        /// </summary>
        private class HeaderTagMatch
        {
            /// <summary>
            /// The regex match which contains the whole HTML representing the header
            /// </summary>
            public Match Match { get; set; }

            /// <summary>
            /// The header level to be used in the resulting HTML (e.g. H2 -> 2)
            /// </summary>
            public int NewLevel { get; set; }

            /// <summary>
            /// The header level generated into the HTML from the Remarkup code (e.g. H2 -> 2)
            /// </summary>
            public int OriginalLevel { get; set; }
        }

        private static RemarkupEngine remarkupEngine = new RemarkupEngine();

        /// <summary>
        /// Dictionary containing user tokens (=key) and their corresponding user data (=value)
        /// </summary>
        protected static Dictionary<string, Phabricator.Data.User> AccountByToken = new Dictionary<string, Phabricator.Data.User>();

        /// <summary>
        /// Dictionary containing project tokens (=key) and their corresponding project data (=value)
        /// </summary>
        protected static Dictionary<string, Phabricator.Data.Project> ProjectByToken = new Dictionary<string, Phabricator.Data.Project>();

        /// <summary>
        /// This synchronization lock object prevents some methods from being run simultaneously.
        /// The next invocation of a method will only be executed when the previous one has left the lock(ReentrancyLock) statement
        /// </summary>
        protected static object ReentrancyLock = new object();

        /// <summary>
        /// Link to the Http.Browser
        /// This property is set by means of reflection
        /// </summary>
        public Http.Browser browser { get; set; }

        /// <summary>
        /// The session token id
        /// This property is set by means of reflection
        /// </summary>
        public string TokenId { get; set; }

        /// <summary>
        /// The key to encrypt the database
        /// This property is set by means of reflection
        /// </summary>
        public string EncryptionKey { get; set; }
        
        /// <summary>
        /// Convert a Phabricator slug (URL) to a readable description.
        /// This method will only be executed for referenced Phriction documents which haven't been downloaded
        /// </summary>
        /// <param name="slug"></param>
        /// <returns></returns>
        protected string ConvertPhabricatorUrlPartToDescription(string slug)
        {
            string[] words = slug.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries).ToArray();
            string result = string.Join(" ", 
                                        words.Select(
                                                        word => char.ToUpper(word[0]) + word.Substring(1)
                                                    )
                                       );
            
            return HttpUtility.UrlDecode(result);
        }

        /// <summary>
        /// Converts a given Remarkup text to HTML
        /// </summary>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="url">URL from where request originated. This can be usefull to parse relative URLS.</param>
        /// <param name="remarkupText">The remarkup content to be parsed</param>
        /// <param name="remarkupParserOutput">References found in the Remarkup content</param>
        /// <param name="includeLineNumbers">If true, empty SPAN elements with a line data-attribute will be added to generated HTML code. This line data-attribute contains the (first) line number of the original Remarkup code for all the generated elements</param>
        /// <returns>HTML</returns>
        public string ConvertRemarkupToHTML(Storage.Database database, string url, string remarkupText, out RemarkupParserOutput remarkupParserOutput, bool includeLineNumbers)
        {
            if (remarkupText == null) remarkupText = "";

            // make sure we don't have a line at the end containing some spaces (which would might a code block)
            remarkupText = RegexSafe.Replace(remarkupText, "\n +$", "\n", System.Text.RegularExpressions.RegexOptions.None);

            browser.Token.EncryptionKey = database.EncryptionKey;
            browser.Token.PrivateEncryptionKey = database.PrivateEncryptionKey;
            string result = remarkupEngine.ToHTML(null, database, browser, url, "\n" + remarkupText + "\n", out remarkupParserOutput, includeLineNumbers);

            result = CorrectInvalidHTML(result);

            if (result.StartsWith("<br>\n")) result = result.Substring("<br>\n".Length);  // remove begin newline if existant
            if (result.EndsWith("\n<br>")) result = result.Substring(0, result.Length - "\n<br>".Length); // remove end newline if existant
            if (result.EndsWith("<br>")) result = result.Substring(0, result.Length - "<br>".Length); // remove end newline if existant

            string text = HttpUtility.HtmlDecode(result);
            text = RegexSafe.Replace(text, "<[^>]*>", "");
            text = RegexSafe.Replace(text, "[\r\n\t]+", " ");
            remarkupParserOutput.Text = text;

            return result;
        }

        private string CorrectInvalidHTML(string html)
        {
            // == fix non-sequential header tags ==============
            // For example: H4 can only appear after a H3 tag. If an H4 appears after an H2, the H4 will be renamed to H3.
            var headerTags = RegexSafe.Matches(html, @"(<h1[^>]*>(.*?)</h1>) |
                                                                   (<h2[^>]*>(.*?)</h2>) |
                                                                   (<h3[^>]*>(.*?)</h3>) |
                                                                   (<h4[^>]*>(.*?)</h4>) |
                                                                   (<h5[^>]*>(.*?)</h5>) |
                                                                   (<h6[^>]*>(.*?)</h6>)
                                                                 ",
                                                           RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline)
                                      .OfType<Match>()
                                      .Select(match => new HeaderTagMatch()
                                      {
                                          OriginalLevel = Int32.Parse(match.Value.Substring(2, 1)),
                                          NewLevel = Int32.Parse(match.Value.Substring(2, 1)),
                                          Match = match
                                      })
                                      .ToArray();

            int currentHeaderLevel = 0;
            for (int index=0; index < headerTags.Length; index++)
            {
                if (headerTags[index].NewLevel > currentHeaderLevel + 1)
                {
                    int subIndex = index;
                    while (subIndex < headerTags.Length)
                    {
                        if (headerTags[subIndex].NewLevel > currentHeaderLevel + 1)
                        {
                            headerTags[subIndex].NewLevel--;
                        }
                        else
                        if (headerTags[subIndex].NewLevel <= currentHeaderLevel)
                        {
                            break;
                        }

                        subIndex++;
                    }

                    index--;
                }
                else
                {
                    currentHeaderLevel = headerTags[index].NewLevel;
                }
            }

            foreach (var modifiedHeaderTag in headerTags.Where(headerTag => headerTag.OriginalLevel != headerTag.NewLevel))
            {
                html = html.Substring(0, modifiedHeaderTag.Match.Index + 2)
                     + modifiedHeaderTag.NewLevel.ToString()
                     + html.Substring(modifiedHeaderTag.Match.Index + 3, modifiedHeaderTag.Match.Length - 5)
                     + modifiedHeaderTag.NewLevel.ToString()
                     + ">"
                     + html.Substring(modifiedHeaderTag.Match.Index + modifiedHeaderTag.Match.Length);
            }

            return html;
        }

        /// <summary>
        /// Format a timestamp in a human readable timestamp
        /// </summary>
        /// <param name="timeStamp"></param>
        /// <param name="locale"></param>
        /// <param name="addDateTimeHTMLSeparator"></param>
        /// <returns>Human readable timestamp</returns>
        public static string FormatDateTimeOffset(DateTimeOffset timeStamp, string locale, bool addDateTimeHTMLSeparator = true)
        {
            if (locale == null)
            {
                throw new Phabrico.Exception.AuthorizationException();
            }

            DateTimeOffset localTimeStamp = timeStamp.ToLocalTime();
            CultureInfo cultureInfo = new CultureInfo(locale);
            string formatTimeStamp = cultureInfo.DateTimeFormat.LongDatePattern;
            string longFormatTimeStamp = localTimeStamp.ToString(formatTimeStamp, cultureInfo);

            if (addDateTimeHTMLSeparator)
            {
                return string.Format("{0} &bull; {1:D2}:{2:D2}", longFormatTimeStamp, localTimeStamp.Hour, localTimeStamp.Minute);
            }
            else
            {
                return string.Format("{0} {1:D2}:{2:D2}", longFormatTimeStamp, localTimeStamp.Hour, localTimeStamp.Minute);
            }
        }

        /// <summary>
        /// Translates a user token into a username
        /// </summary>
        /// <param name="accountToken"></param>
        /// <returns></returns>
        protected string getAccountName(string accountToken)
        {
            if (string.IsNullOrWhiteSpace(accountToken))
            {
                return Locale.TranslateText("No users assigned", browser.Session.Locale);
            }
            else
            {
                Phabricator.Data.User user;
                if (AccountByToken.TryGetValue(accountToken, out user) == false)
                {
                    // load all users again in case there's a task with an unknown author user token
                    using (Storage.Database database = new Storage.Database(EncryptionKey))
                    {
                        Storage.User phabricatorUsers = new Storage.User();
                        AccountByToken = phabricatorUsers.Get(database)
                                                         .ToDictionary(key => key.Token, value => value);
                    }
                }

                if (AccountByToken.TryGetValue(accountToken, out user) == false)
                {
                        return "(unknown)";
                }
                else
                {
                    return user.RealName;
                }
            }
        }

        /// <summary>
        /// Translates a project token into project name
        /// </summary>
        /// <param name="projectToken"></param>
        /// <returns></returns>
        protected string getProjectName(string projectToken)
        {
            if (string.IsNullOrWhiteSpace(projectToken))
            {
                return Locale.TranslateText("No projects assigned", browser.Session.Locale);
            }
            else
            {
                Phabricator.Data.Project project;
                if (ProjectByToken.TryGetValue(projectToken, out project) == false)
                {
                    // load all projects again in case there's a task with an unknown project token
                    using (Storage.Database database = new Storage.Database(EncryptionKey))
                    {
                        Storage.Project phabricatorProjects = new Storage.Project();
                        ProjectByToken = phabricatorProjects.Get(database)
                                                            .ToDictionary(key => key.Token, value => value);
                    }
                }

                if (ProjectByToken.TryGetValue(projectToken, out project) == false)
                {
                    return "(unknown)";
                }
                else
                {
                    return project.Name;
                }
            }
        }
    }
}
