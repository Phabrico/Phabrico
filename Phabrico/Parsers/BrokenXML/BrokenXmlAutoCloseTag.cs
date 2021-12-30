using System.Linq;

namespace Phabrico.Parsers.BrokenXML
{
    class BrokenXmlAutoCloseTag : BrokenXmlOpeningTag
    {
        /// <summary>
        /// Returns a string representation of the current BrokenXmlAutoCloseTag
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("<{0}{1} />",
                Name, 
                Attributes.Any() ? " " + string.Join(" ", Attributes.Select(a => a.Name + "=\"" + a.Value + "\""))
                                 : ""
            );
        }
    }
}
