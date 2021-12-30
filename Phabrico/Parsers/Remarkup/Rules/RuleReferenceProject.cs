using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Phabricator.Data;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Rmarkup parser for links to Projects
    /// it will convert "#Project" to a CSS-styled Project tag
    /// </summary>
    [RuleXmlTag("P")]
    public class RuleReferenceProject : RemarkupRule
    {
        public string ProjectName { get; private set; }
        public string ProjectToken { get; private set; }

        /// <summary>
        /// Deep copy clone
        /// </summary>
        /// <param name="originalRemarkupRule"></param>
        public override void Clone(RemarkupRule originalRemarkupRule)
        {
            RuleReferenceProject originalRuleReferenceProject = originalRemarkupRule as RuleReferenceProject;
            ProjectName = originalRuleReferenceProject.ProjectName;
            ProjectToken = originalRuleReferenceProject.ProjectToken;
        }

        /// <summary>
        /// Converts Remarkup encoded text into HTML
        /// </summary>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="browser">Reference to web browser</param>
        /// <param name="url">URL from where this method was executed</param>
        /// <param name="remarkup">Remarkup content to be converted</param>
        /// <param name="html">Translated HTML</param>
        /// <returns>True if the content could successfully be converted</returns>
        public override bool ToHTML(Storage.Database database, Browser browser, string url, ref string remarkup, out string html)
        {
            html = "";
            if (RuleStartAfterWhiteSpace || RuleStartOnNewLine)
            {
                Match match = RegexSafe.Match(remarkup, @"^#([^\r\n\t ]+[ \t]*)", RegexOptions.Singleline);
                if (match.Success == false) return false;

                Match matchProjectName = RegexSafe.Match(match.Groups[1].Value, "[a-z0-9._-]*[a-z0-9_-]", RegexOptions.IgnoreCase);
                if (matchProjectName.Success == false) return false;

                string encryptionKey = browser.Token?.EncryptionKey;

                if (string.IsNullOrEmpty(encryptionKey) == false)
                {
                    Storage.Account accountStorage = new Storage.Account();
                    Storage.Project projectStorage = new Storage.Project();
                    Project project = projectStorage.Get(database, matchProjectName.Value, browser.Session.Locale);
                    if (project == null) return false;

                    LinkedPhabricatorObjects.Add(project);

                    string rgbColor = "rgb(0, 128, 255)";
                    if (project != null && string.IsNullOrWhiteSpace(project.Color) == false)
                    {
                        rgbColor = project.Color;
                    }

                    string style = string.Format("background: {0}; color: {1}; border-color: {1}",
                                        rgbColor,
                                        ColorFunctionality.WhiteOrBlackTextOnBackground(rgbColor));

                    if (browser.HttpServer.Customization.HideManiphest || browser.HttpServer.Customization.HideUsers)
                    {
                        html = string.Format("<span class='project-reference'>{0}</span>",
                                        System.Web.HttpUtility.HtmlEncode(project.Name));
                    }
                    else
                    {
                        html = string.Format("<a class='project-reference phui-icon-view phui-font-fa fa-briefcase' href='project/info/{0}/' style='{1}'>{2}</a>",
                                        project.InternalName,
                                        style,
                                        System.Web.HttpUtility.HtmlEncode(project.Name));
                    }

                    Length = match.Length;
                    if (match.Value.EndsWith(" ") || match.Value.EndsWith("\t"))
                    {
                        Length--;
                    }

                    remarkup = remarkup.Substring(Length);

                    ProjectName = project.Name;
                    ProjectToken = project.Token;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Generates remarkup content
        /// </summary>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="browser">Reference to browser</param>
        /// <param name="innerText">Text between XML opening and closing tags</param>
        /// <param name="attributes">XML attributes</param>
        /// <returns>Remarkup content, translated from the XML</returns>
        internal override string ConvertXmlToRemarkup(Storage.Database database, Browser browser, string innerText, Dictionary<string, string> attributes)
        {
            return innerText;
        }
    }
}
