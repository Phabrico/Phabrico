using System.Collections.Generic;
using System.Linq;

namespace Phabrico.Parsers.BrokenXML
{
    class BrokenXmlOpeningTag : BrokenXmlClosingTag
    {
        public List<BrokenXmlAttribute> Attributes { get; internal set; }

        /// <summary>
        /// Returns a string representation of the current BrokenXmlOpeningTag
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("<{0}{1}>",
                Name, 
                Attributes.Any() ? " " + string.Join(" ", Attributes.Select(a => a.Name + "=\"" + a.Value + "\""))
                                 : ""
            );
        }
    }
}
