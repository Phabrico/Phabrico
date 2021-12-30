namespace Phabrico.Parsers.BrokenXML
{
    class BrokenXmlClosingTag : BrokenXmlToken 
    {
        public string Name { get; set; }

        /// <summary>
        /// Returns a string representation of the current BrokenXmlClosingTag
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "</" + Name + ">";
        }
    }
}
