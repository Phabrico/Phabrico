using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Phabricator.Data;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Rmarkup parser for links to Phame blog posts
    /// it will convert "{J1}" to a CSS-styled blog post link
    /// </summary>
    [RuleXmlTag("BL")]
    public class RuleReferencePhameBlogPost : RemarkupRule
    {
        public int BlogPostID { get; private set; }

        /// <summary>
        /// Creates a copy of the current RuleReferencePhameBlogPost
        /// </summary>
        /// <returns></returns>
        public override RemarkupRule Clone()
        {
            RuleReferencePhameBlogPost copy = base.Clone() as RuleReferencePhameBlogPost;
            copy.BlogPostID = BlogPostID;
            return copy;
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
            Match match = RegexSafe.Match(remarkup, @"^{J([0-9]+)([^}]*)}", RegexOptions.Singleline);
            if (match.Success == false) return false;

            Storage.PhamePost phamePostStorage = new Storage.PhamePost();

            BlogPostID = Int32.Parse(match.Groups[1].Value);
            Phabricator.Data.PhamePost phamePost = phamePostStorage.Get(database, BlogPostID.ToString(), browser.Session.Locale);
            if (phamePost == null) return false;


            html = string.Format("<a class='phame-reference phui-icon-view phui-font-fa fa-feed' href='phame/post/{0}/'>&nbsp;{1}</a>",
                            BlogPostID,
                            System.Web.HttpUtility.HtmlEncode(phamePost.Title));

            remarkup = remarkup.Substring(match.Length);

            Length = match.Length;

            return true;
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
