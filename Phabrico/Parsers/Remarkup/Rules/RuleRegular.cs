using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// This rule will check for sequential alphanumeric characters.
    /// This rule is executed first to improve the performance of the Remarkup decoding
    /// </summary>
    [RulePriority(-100)]
    [RuleXmlTag("T")]
    public class RuleRegular : RemarkupRule
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

            if (RegexSafe.IsMatch(remarkup, "^(https?://|mailto:|tel:)", RegexOptions.Singleline)) return false;  // hyperlink syntax
            if (RegexSafe.IsMatch(remarkup, @"^((?=.{0,64}@.{0,255}$)(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*|""(?:[\x01-\x08\x0b\x0c\x0e-\x1f\x21\x23-\x5b\x5d-\x7f]|\\[\x01-\x09\x0b\x0c\x0e-\x7f])*""))@(?:(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?|\[(?:(?:(2(5[0-5]|[0-4][0-9])|1[0-9][0-9]|[1-9]?[0-9]))\.){3}(?:(2(5[0-5]|[0-4][0-9])|1[0-9][0-9]|[1-9]?[0-9])|[a-z0-9-]*[a-z0-9]:(?:[\x01-\x08\x0b\x0c\x0e-\x1f\x21-\x5a\x53-\x7f]|\\[\x01-\x09\x0b\x0c\x0e-\x7f])+)\])", RegexOptions.Singleline)) return false;  // email-address syntax

            if (RuleStartOnNewLine)
            {
                if (RegexSafe.IsMatch(remarkup, @"^(IMPORTANT|NOTE|WARNING):(\r?\n)?(.+?(?=\n\r?\n|$))", RegexOptions.Singleline)) return false;  // notification syntax
                if (RegexSafe.IsMatch(remarkup, @"^\((IMPORTANT|NOTE|WARNING)\)(\r?\n)?(.+?(?=\n\r?\n|$))", RegexOptions.Singleline)) return false;  // notification syntax
            }

            // check first line
            Match match = RegexSafe.Match(remarkup, "^[^\n]*", RegexOptions.Singleline);
            if (match.Success)
            {
                if (match.Value.Contains("{{{")) return false;  // contains interpreter syntax
            }

            // check for numeric characters, but make sure it's no numeric list item (which has a ') ' or a '. ' after it)
            match = RegexSafe.Match(remarkup, @"^[1-9][0-9]*[).] ", System.Text.RegularExpressions.RegexOptions.Singleline);
            if (match.Success) return false;

            // check for alphanumeric characters, but make sure there's no underlining (by '=' or '-' characters) on the next line (=> header syntax)
            match = RegexSafe.Match(remarkup, @"^[A-Za-z0-9]+(?![^\n]*\n[=-])", System.Text.RegularExpressions.RegexOptions.Singleline);
            if (match.Success == false) return false;

            // check if text is a macro
            string text = remarkup;
            Phabricator.Data.File macroFile = Http.Server.FilesPerMacroName.FirstOrDefault(macro => text.StartsWith(macro.Key)).Value;
            if (macroFile != null)
            {
                return false;
            }

            // search for metadata in the text and put them between SPAN elements (with class=metadata)
            string decodedHTML;
            Length = ProcessMetaData(remarkup, out decodedHTML);
            if (Length > 0)
            {
                remarkup = remarkup.Substring(Length);
                html = decodedHTML;
            }
            else
            {
                remarkup = remarkup.Substring(match.Length);
                html = match.Value;

                Length = match.Length;
            }

            return true;
        }

        /// <summary>
        /// Searches for any metadata in the remarkup content (e.g. IP addresses)
        /// These matadata will be incapsulated in SPAN elements
        /// </summary>
        /// <param name="remarkup">Remarkup content to be investigated</param>
        /// <param name="decodedHTML">Decoded HTML for metadata</param>
        /// <returns>Non-zero if metadata was found (contains the Length of the data found in the Remarkup content)</returns>
        private int ProcessMetaData(string remarkup, out string decodedHTML)
        {
            const string metadataType_IPV4 = "ipv4";
            const string metadataType_IPV6 = "ipv6";

            Dictionary<Match, string> metadatas = new Dictionary<Match, string>();
            decodedHTML = null;

            // search for IPv4 addresses
            MatchCollection ipv4s = RegexSafe.Matches(remarkup, @"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b");
            foreach (Match ipv4 in ipv4s)
            {
                metadatas[ipv4] = metadataType_IPV4;
            }


            // search for IPv6 addresses
            MatchCollection ipv6s = RegexSafe.Matches(remarkup, @"^
                (
                    (([0-9A-Fa-f]{1,4}:){7}([0-9A-Fa-f]{1,4}|:))|
                    (([0-9A-Fa-f]{1,4}:){6}(:[0-9A-Fa-f]{1,4}|((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3})|:))|
                    (([0-9A-Fa-f]{1,4}:){5}(((:[0-9A-Fa-f]{1,4}){1,2})|:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3})|:))|
                    (([0-9A-Fa-f]{1,4}:){4}(((:[0-9A-Fa-f]{1,4}){1,3})|((:[0-9A-Fa-f]{1,4})?:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}))|:))|
                    (([0-9A-Fa-f]{1,4}:){3}(((:[0-9A-Fa-f]{1,4}){1,4})|((:[0-9A-Fa-f]{1,4}){0,2}:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}))|:))|
                    (([0-9A-Fa-f]{1,4}:){2}(((:[0-9A-Fa-f]{1,4}){1,5})|((:[0-9A-Fa-f]{1,4}){0,3}:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}))|:))|
                    (([0-9A-Fa-f]{1,4}:){1}(((:[0-9A-Fa-f]{1,4}){1,6})|((:[0-9A-Fa-f]{1,4}){0,4}:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}))|:))|
                    (:(((:[0-9A-Fa-f]{1,4}){1,7})|((:[0-9A-Fa-f]{1,4}){0,5}:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}))|:))
                )
                \b",
                RegexOptions.IgnorePatternWhitespace);
            foreach (Match ipv6 in ipv6s)
            {
                metadatas[ipv6] = metadataType_IPV6;
            }

            if (metadatas.Any() == false)
            {
                return 0;
            }

            decodedHTML = remarkup;
            foreach (KeyValuePair<Match, string> metadata in metadatas.OrderByDescending(kvp => kvp.Key.Index))
            {
                decodedHTML = decodedHTML.Substring(0, metadata.Key.Index)
                            + "<span class='metadata " + metadata.Value + "'>" + metadata.Key.Value + "</span>";
            }

            int length = metadatas.Select(kvp => kvp.Key.Index + kvp.Key.Length).Max();
            return length;
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
