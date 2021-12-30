using System.Collections.Generic;
using Phabrico.Http;
using Phabrico.Storage;

namespace Phabrico.Parsers.Remarkup.Rules
{
    [RuleXmlTag("")]
    public class RuleUnknownToken : RemarkupRule
    {
        public string Content { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public RuleUnknownToken()
        {
            // needed for RemarkupEngine
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="content"></param>
        public RuleUnknownToken(string content)
        {
            Content = content;
            Text = content;
        }

        /// <summary>
        /// Abstract method is not implemented
        /// </summary>
        /// <param name="database"></param>
        /// <param name="browser"></param>
        /// <param name="url"></param>
        /// <param name="remarkup"></param>
        /// <param name="html"></param>
        /// <returns></returns>
        public override bool ToHTML(Database database, Browser browser, string url, ref string remarkup, out string html)
        {
            html = "";
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
            if (database == null)
            {
                throw new System.ArgumentNullException(nameof(database));
            }

            return innerText;
        }
    }
}