namespace Phabrico.Parsers.BrokenXML
{
    class BrokenXmlAttribute : BrokenXmlToken
    {
        public string Name { get; set; }

        /// <summary>
        /// Returns a string representation of the current BrokenXmlAttribute
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Name + "=\"" + Value + "\"";
        }
    }
}
