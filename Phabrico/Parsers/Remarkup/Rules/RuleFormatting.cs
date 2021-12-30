using Phabrico.Http;
using Phabrico.Miscellaneous;
using System.Text.RegularExpressions;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Abstract Remarkup parser for formatting rules
    /// </summary>
    public abstract class RuleFormatting : RemarkupRule
    {
        public string UnformattedText { get; protected set; }
    }
}
