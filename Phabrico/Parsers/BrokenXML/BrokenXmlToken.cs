namespace Phabrico.Parsers.BrokenXML
{
    public abstract class BrokenXmlToken
    {
        public int Index;
        public int Length;
        public string Value;

        /// <summary>
        /// Returns a string representation of the current BrokenXmlToken
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Value;
        }
    }
}
