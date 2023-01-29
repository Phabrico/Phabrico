using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Abstract class for Remarkup rules
    /// </summary>
    public abstract class RemarkupRule
    {
        public const string BGN = "\x02";   // During XML Export the < and > characters in the XML tags will be replaced by BGN and END
        public const string END = "\x03";   // During XML Export the < and > characters in the XML tags will be replaced by BGN and END

        /// <summary>
        /// Decoded HTML
        /// </summary>
        public string Html { get; set; }

        /// <summary>
        /// Length of current RemarkupRule
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Start position where current RemarkupRule is positioned
        /// </summary>
        public int Start { get; set; }

        /// <summary>
        /// Content of RemarkupRule
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Determines the process order of the rule.
        /// A low priority number means a earlier rule processing
        /// </summary>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
        public class RulePriority : Attribute
        {
            /// <summary>
            /// Default priority for all RemarkupRules
            /// </summary>
            public const int DefaultPriority = 50;

            /// <summary>
            /// Priority value
            /// </summary>
            public int Priority { get; private set; } = 0;

            /// <summary>
            /// Initializes a newRulePriority instance
            /// </summary>
            /// <param name="priority"></param>
            public RulePriority(int priority)
            {
                Priority = priority;
            }
        }

        /// <summary>
        /// This attribute disallows the innertext to be decoded as Remarkup for the given RuleType
        /// </summary>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
        public class RuleNotInnerRuleFor : Attribute
        {
            /// <summary>
            /// Defines the outer Rule type where the current Rule is located in
            /// </summary>
            public Type ParentRuleType { get; private set; }

            /// <summary>
            /// Initializes a new instance of RuleNotInnerRuleFor
            /// </summary>
            /// <param name="parentRuleType"></param>
            public RuleNotInnerRuleFor(Type parentRuleType)
            {
                ParentRuleType = parentRuleType;
            }
        }

        /// <summary>
        /// This attribute defines the XML tag for the Remarkup to XML export functionality
        /// </summary>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
        public class RuleXmlTag : Attribute
        {
            /// <summary>
            /// Defines the XML tag for the Remarkup to XML export functionality
            /// </summary>
            public string XmlTag { get; private set; }

            /// <summary>
            /// Initializes a new instance of RuleXmlTag
            /// </summary>
            /// <param name="parentRuleType"></param>
            public RuleXmlTag(string xmlTag)
            {
                XmlTag = xmlTag;
            }

            /// <summary>
            /// Returns the XML tag name of a specified RemarkupRule class
            /// </summary>
            /// <param name="RemarkupRuleClass"></param>
            /// <returns></returns>
            public static string GetXmlTag(Type RemarkupRuleClass)
            {
                RuleXmlTag ruleXmlTag = RemarkupRuleClass.GetCustomAttributes(typeof(RuleXmlTag), false).OfType<RuleXmlTag>().FirstOrDefault();
                if (ruleXmlTag == null)
                    return null;
                else
                    return ruleXmlTag.XmlTag;
            }
        }

        /// <summary>
        /// string which represents the Remarkup-rule-specific XML attributes
        /// </summary>
        public virtual string Attributes { get; } = null;

        /// <summary>
        /// Link to browser (used in XML generation)
        /// </summary>
        public Browser Browser;

        /// <summary>
        /// List of underlying tokens
        /// </summary>
        public RemarkupTokenList ChildTokenList { get; set; } = new RemarkupTokenList();

        /// <summary>
        /// Link to database (used in XML generation)
        /// </summary>
        public Database Database;

        /// <summary>
        /// Reference to the Remarkup engine
        /// </summary>
        public RemarkupEngine Engine { get; set; } = null;

        /// <summary>
        /// Collection of all phabricator objects (e.g phriction, maniphest, ...) that are referenced in the current remarkup rule
        /// </summary>
        public List<Phabricator.Data.PhabricatorObject> LinkedPhabricatorObjects = new List<Phabricator.Data.PhabricatorObject>();

        /// <summary>
        /// Some Remarkup rules can be executed in another Remarkup rule (e.g. bold text in a table cell)
        /// This property represents the outer Remarkup rule
        /// </summary>
        public RemarkupRule ParentRemarkupRule { get; set; }

        /// <summary>
        /// If true, the current rule will only be valid if it starts on a new line
        /// </summary>
        public bool RuleStartOnNewLine { get; set; } = true;

        /// <summary>
        /// If true, the current rule will only be valid if a whitespace precedes it
        /// </summary>
        public bool RuleStartAfterWhiteSpace { get; set; } = true;

        /// <summary>
        /// If true, the current rule will only be valid if a punctuation character precedes it
        /// </summary>
        public bool RuleStartAfterPunctuation { get; set; } = true;

        /// <summary>
        /// List of processed tokens
        /// </summary>
        public RemarkupTokenList TokenList { get; set; }

        /// <summary>
        /// Link to Phriction URL from where the token is parsed (used in XML generation)
        /// </summary>
        public string DocumentURL;

        /// <summary>
        /// Creates a copy of the current RemarkupRule
        /// </summary>
        /// <returns></returns>
        public virtual RemarkupRule Clone()
        {
            RemarkupRule clonedRemarkupRule = GetType().GetConstructor(Type.EmptyTypes).Invoke(null) as RemarkupRule;
            if (clonedRemarkupRule != null)
            {
                clonedRemarkupRule.Html = Html;
                clonedRemarkupRule.Length = Length;
                clonedRemarkupRule.Start = Start;
                clonedRemarkupRule.Text = Text;
                clonedRemarkupRule.Clone(this);
                clonedRemarkupRule.LinkedPhabricatorObjects.AddRange(LinkedPhabricatorObjects);
                clonedRemarkupRule.ChildTokenList.AddRange(ChildTokenList);
            }

            return clonedRemarkupRule;
        }

        /// <summary>
        /// Virtual method which allows to do a deeper clone of RemarkupRule
        /// </summary>
        /// <param name="originalRemarkupRule"></param>
        public virtual void Clone(RemarkupRule originalRemarkupRule)
        {
        }

        /// <summary>
        /// Generates remarkup content
        /// </summary>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="browser">Reference to browser</param>
        /// <param name="innerText">Text between XML opening and closing tags</param>
        /// <param name="attributes">XML attributes</param>
        /// <returns>Remarkup content, translated from the XML</returns>
        internal abstract string ConvertXmlToRemarkup(Database database, Browser browser, string innerText, Dictionary<string, string> attributes);

        /// <summary>
        /// This method is executed before the ToHTML() is executed.
        /// It is meant for initializing some local variables
        /// </summary>
        public virtual void Initialize()
        {
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
        public abstract bool ToHTML(Storage.Database database, Browser browser, string url, ref string remarkup, out string html);

        /// <summary>
        /// Decodes a url into a Phabricator-like format
        /// </summary>
        /// <param name="url">Decoded URL</param>
        /// <returns>Encoded URL</returns>
        public static string UrlEncode(string url)
        {
            string stringResult;

            // if external URL -> do not encode
            if (url.StartsWith("http://")) return url;
            if (url.StartsWith("https://")) return url;
            if (url.StartsWith("mailto://")) return url;
            if (url.StartsWith("tel://")) return url;
            if (url.StartsWith("ftp://")) return url;

            // check if URL is valid -> if so, do not encode
            if (RegexSafe.IsMatch(url, "[\r\n\\\\]", RegexOptions.Singleline)) return url;

            // determine anchor in the URL (if existant)
            string anchor = url.Split('#').Skip(1).FirstOrDefault() ?? "";

            // if any stop characters are found in the URL, take only the part before the 1st stop character
            url = url.Split('?', '#')[0];

            // lowercase everything
            url = url.ToLower();


            byte[] bytes = UTF8Encoding.UTF8.GetBytes(url);

            string allowedCharacters = "abcdefghijklmnopqrstuvwxyz0123456789-_./~()";
            string charactersToBeUnderscored = " %&+={}\\<>\"'";
            int disallowedCharacters = bytes.Count(ch => allowedCharacters.Contains((char)ch) == false);
            if (disallowedCharacters > 0)
            {
                byte[] result = new byte[bytes.Length + disallowedCharacters * 2];
                int index = 0;
                foreach (byte ch in bytes)
                {
                    if (allowedCharacters.Contains((char)ch))
                    {
                        // copy character
                        result[index++] = ch;
                    }
                    else
                    if (charactersToBeUnderscored.Contains((char)ch))
                    {
                        // replace character by underscore
                        result[index++] = (byte)'_';
                    }
                    else
                    {
                        // replace character by URL encoded character (i.e. %XX)
                        result[index++] = (byte)'%';
                        result[index++] = (byte)(((ch / 0x10) < 10) ? '0' + (ch / 0x10) : 'A' + ((ch / 0x10) - 10));
                        result[index++] = (byte)(((ch % 0x10) < 10) ? '0' + (ch % 0x10) : 'A' + ((ch % 0x10) - 10));
                    }
                }

                // convert result to trimmed string
                stringResult = UTF8Encoding.UTF8
                                           .GetString(result)
                                           .TrimEnd('_', '\0');

                // remove underscore duplicates
                stringResult = RegexSafe.Replace(stringResult, "__+", "_");
            }
            else
            {
                stringResult = url;
            }

            if (string.IsNullOrWhiteSpace(anchor) == false)
            {
                anchor = "#" + anchor;
            }

            return stringResult + anchor;
        }

        /// <summary>
        /// Returns a readable description
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("{{{0}}}: \"{1}\"", GetType().Name, Text);
        }
    }
}
