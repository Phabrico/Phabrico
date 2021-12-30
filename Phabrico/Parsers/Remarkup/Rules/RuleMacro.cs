using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Rmarkup parser for Macro objects
    /// </summary>
    [RulePriority(-110)]
    [RuleXmlTag("MA")]
    public class RuleMacro : RemarkupRule
    {
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

            if (RuleStartOnNewLine)
            {
                Match match = RegexSafe.Match(remarkup, @"^ *([a-z0-9:_-][a-z0-9:_-][a-z0-9:_-]+) *([\r\n][\r\n]?|$)", RegexOptions.Singleline);
                if (match.Success == false) return false;

                string macroName = match.Groups[1].Value;
                Phabricator.Data.File macroFile = Http.Server.FilesPerMacroName.FirstOrDefault(macro => macroName.Equals(macro.Key)).Value;
                if (macroFile == null) return false;

                LinkedPhabricatorObjects.Add(macroFile);

                html = string.Format(@"<img alt='{0}' src='file/data/{1}/'>", macroFile.FileName.Replace("'", ""), macroFile.ID);
                remarkup = "\r\n" + remarkup.Substring(match.Length);

                Length = match.Length;

                return true;
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
        internal override string ConvertXmlToRemarkup(Database database, Browser browser, string innerText, Dictionary<string, string> attributes)
        {
            return innerText;
        }
    }
}
