using System.Text.RegularExpressions;

using Phabrico.Http;
using Phabrico.Miscellaneous;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Remarkup parser for Phabricator interpreters.
    /// Only Cowsay and Figlet are implemented
    /// </summary>
    public class RuleInterpreter : RemarkupRule
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

            Match match = RegexSafe.Match(remarkup, "^(([^\r\n]+?)(?={{{)){{{(.+?(?=}}}))}}}", RegexOptions.Singleline);
            if (match.Success == false) return false;

            string interpreterBlockParameterList = match.Groups[2].Value.Trim();
            string interpreterBlockContent = match.Groups[3].Value.Trim('\r', '\n');

            string interpreterName = interpreterBlockParameterList.Split(' ', '\r', '\n', '(', '{')[0].ToLower();
            Interpreters.Interpreter interpreter;
            if (Interpreters.Interpreter.KnownInterpreters.TryGetValue(interpreterName, out interpreter))
            {
                interpreterBlockParameterList = interpreterBlockParameterList.Substring(interpreterName.Length).Trim(' ', '\r', '\n', '(', '{', ')');
                html = interpreter.Parse(interpreterBlockParameterList, interpreterBlockContent);
            }
            else
            {
                html = "<div class='remarkup-warning'>No interpreter found: " + interpreterName + "</div>";
            }

            remarkup = remarkup.Substring(match.Length);

            Length = match.Length;

            return true;
        }
    }
}
