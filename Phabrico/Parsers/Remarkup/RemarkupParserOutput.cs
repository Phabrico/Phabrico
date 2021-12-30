using System.Collections.Generic;

namespace Phabrico.Parsers.Remarkup
{
    /// <summary>
    /// Output class which is used by the Controller.ConvertRemarkupToHTML method.
    /// This Controller.ConvertRemarkupToHTML method will basically convert some remarkup content
    /// to HTML but generates also some other stuff which is stored in this output class.
    /// </summary>
    public class RemarkupParserOutput
    {
        /// <summary>
        /// Contains all the Phabricator objects (e.g. phriction documents, maniphest tasks) which
        /// are referenced in the given remarkup content
        /// </summary>
        public List<Phabricator.Data.PhabricatorObject> LinkedPhabricatorObjects { get; set; }

        /// <summary>
        /// Contains a plain-text version of the remarkup content (thus without all formatting)
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Contains all the token rules of the given remarkup content
        /// </summary>
        public RemarkupTokenList TokenList { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public RemarkupParserOutput()
        {
            LinkedPhabricatorObjects = new List<Phabricator.Data.PhabricatorObject>();
            Text = "";
            TokenList = new RemarkupTokenList();
        }
    }
}
