using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Phabrico.Parsers.Remarkup.Rules
{
    [RulePriority(-110)]
    [RuleXmlTag("CU")]
    public class RuleCustomRule : RemarkupRule
    {
        public override bool ToHTML(Database database, Browser browser, string url, ref string remarkup, out string html)
        {
            lock (ApplicationCustomization.lockCustomRemarkupRules)
            {
                foreach (KeyValuePair<string, string> customRemarkupRule in browser.HttpServer.Customization.CustomRemarkupRules)
                {
                    try
                    {
                        string regex = "^" + customRemarkupRule.Key.TrimStart('^');

                        Match match = RegexSafe.Match(remarkup, regex, RegexOptions.Singleline);
                        if (match.Success)
                        {
                            html = RegexSafe.Replace(remarkup.Substring(0, match.Length), regex, customRemarkupRule.Value);
                            remarkup = remarkup.Substring(match.Length);
                            return true;
                        }
                    }
                    catch
                    {
                    }
                }

                html = "";
                return false;
            }
        }

        internal override string ConvertXmlToRemarkup(Database database, Browser browser, string innerText, Dictionary<string, string> attributes)
        {
            return innerText;
        }
    }
}
