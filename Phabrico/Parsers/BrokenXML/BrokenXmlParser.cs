using Phabrico.Miscellaneous;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Phabrico.Parsers.BrokenXML
{
    public class BrokenXmlParser
    {
        /// <summary>
        /// Converts a BrokenXml string into a bunch of BrokenXmlTokens
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        internal IEnumerable<BrokenXmlToken> Parse(string xml)
        {
            string unprocessedXML = xml;
            int index = 0;

            while (unprocessedXML.Any())
            {
                Match match = RegexSafe.Match(unprocessedXML, "^[^<]+", RegexOptions.Singleline);
                if (match.Success)
                {
                    BrokenXmlText newToken = new BrokenXmlText();
                    newToken.Value = match.Value;
                    newToken.Index = match.Index + index;
                    newToken.Length = match.Length;

                    unprocessedXML = unprocessedXML.Substring(match.Length);
                    index += match.Length;

                    yield return newToken;
                }

                match = RegexSafe.Match(unprocessedXML, "^<(([^ >]+)([^>]*))>", RegexOptions.Singleline);
                if (match.Success)
                {
                    if (match.Value[match.Length - 2] == '/')
                    {
                        BrokenXmlAutoCloseTag newToken = new BrokenXmlAutoCloseTag();
                        newToken.Name = match.Groups[2].Value;
                        newToken.Attributes = new List<BrokenXmlAttribute>();
                        newToken.Index = match.Index + index;
                        newToken.Length = match.Length;
                        newToken.Value = match.Value;

                        Match[] attributes = RegexSafe.Matches(match.Groups[3].Value, " *([^=]+)=\"([^\"]*)\"").OfType<Match>().ToArray();
                        foreach (Match attribute in attributes)
                        {
                            newToken.Attributes.Add(new BrokenXmlAttribute()
                            {
                                Name = attribute.Groups[1].Value,
                                Value = attribute.Groups[2].Value,
                                Index = attribute.Index + index,
                                Length = attribute.Length
                            });
                        }

                        unprocessedXML = unprocessedXML.Substring(match.Length);
                        index += match.Length;

                        yield return newToken;
                    }
                    else
                    {
                        string tagName = match.Groups[2].Value;

                        if (tagName.StartsWith("/"))
                        {
                            BrokenXmlClosingTag newClosingTag = new BrokenXmlClosingTag();
                            newClosingTag.Name = tagName.Substring(1);
                            newClosingTag.Index = match.Index + index;
                            newClosingTag.Length = match.Length;
                            newClosingTag.Value = match.Value;

                            unprocessedXML = unprocessedXML.Substring(match.Length);
                            index += match.Length;

                            yield return newClosingTag;
                        }
                        else
                        {
                            BrokenXmlOpeningTag newOpeningTag = new BrokenXmlOpeningTag();
                            newOpeningTag.Name = tagName;
                            newOpeningTag.Attributes = new List<BrokenXmlAttribute>();
                            newOpeningTag.Index = match.Index + index;
                            newOpeningTag.Length = match.Length;
                            newOpeningTag.Value = match.Value;


                            Match[] attributes = RegexSafe.Matches(match.Groups[3].Value, " *([^=]+)=\"([^\"]*)\"").OfType<Match>().ToArray();
                            foreach (Match attribute in attributes)
                            {
                                newOpeningTag.Attributes.Add(new BrokenXmlAttribute()
                                {
                                    Name = attribute.Groups[1].Value,
                                    Value = attribute.Groups[2].Value,
                                    Index = match.Groups[3].Index + attribute.Index + index,
                                    Length = attribute.Length
                                });
                            }

                            unprocessedXML = unprocessedXML.Substring(match.Length);
                            index += match.Length;

                            yield return newOpeningTag;
                        }
                    }
                }
            }
        }
    }
}
