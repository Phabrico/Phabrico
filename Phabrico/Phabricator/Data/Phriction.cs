using System;

namespace Phabrico.Phabricator.Data
{
    /// <summary>
    /// Represents an PhrictionInfo record from the SQLite Phabrico database
    /// </summary>
    public class Phriction : PhabricatorObject
    {
        /// <summary>
        /// Token prefix to identify Phriction objects in the Phabrico database
        /// </summary>
        public const string Prefix = "PHID-WIKI-";

        /// <summary>
        /// Token prefix to identify Phriction alias objects in the Phabrico database
        /// </summary>
        public const string PrefixAlias = "PHID-WIKIALIAS-";

        /// <summary>
        /// Token prefix to identify Phriction coverpages in the Phabrico database.
        /// A Phriction coverpage is page created in Phabrico in case the Phabricator Phriction Homepage
        /// can not be downloaded, but some of the underlying Phriction documents can.
        /// Phabrico will create a coverpage with links to the underlying downloaded Phriction documents
        /// When you click on Phriction in the navigator menu, you will see the cover page in this case.
        /// If the Phabricator Phriction homepage can be downloaded, you won't see this coverpage.
        /// </summary>
        public const string PrefixCoverPage = "PHID-WIKICOVER-";

        /// <summary>
        /// Author of the document
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Remarkup content of the document
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Timestamp when the document was last modified in Phabricator
        /// </summary>
        public DateTimeOffset DateModified { get; set; }

        /// <summary>
        /// If true, the document is visible in the list of Favorites
        /// </summary>
        public Int64 DisplayOrderInFavorites { get; set; } = 0;

        /// <summary>
        /// Token of user who last modified this document
        /// </summary>
        public string LastModifiedBy { get; set; }

        /// <summary>
        /// Title of the document
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// URL path to the document
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Projects linked to the task
        /// </summary>
        public string Projects { get; set; }

        /// <summary>
        /// Users who subscribed to this document
        /// </summary>
        public string Subscribers { get; set; }

        /// <summary>
        /// Initializes a new instance of a Phriction record
        /// </summary>
        public Phriction()
        {
            Name = "";
            TokenPrefix = Prefix;
            Projects = "";
            Subscribers = "";
        }

        /// <summary>
        /// Clones a new instance of a Phriction record
        /// </summary>
        /// <param name="original"></param>
        public Phriction(Phriction original)
            : base(original)
        {
            this.TokenPrefix = Prefix;

            this.Name = original.Name;
            this.Projects = original.Projects;
            this.DateModified = original.DateModified;
            this.Author = original.Author;
            this.Subscribers = original.Subscribers;
            this.Content = original.Content;
            this.Path = original.Path;
        }

        /// <summary>
        /// Compares the current Phriction object with another Phriction object
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override int CompareTo(object obj)
        {
            Phriction other = obj as Phriction;
            return Name.CompareTo(other.Name);
        }

        /// <summary>
        /// True if the given parameter is a token and belongs to the current Phriction object
        /// or if the given parameter is a URL and belongs to the current Phriction object
        /// </summary>
        /// <param name="parameter">URL or token</param>
        /// <returns></returns>
        public override bool Equals(string parameter)
        {
            return base.Equals(parameter) ||
                   Path.Trim('/').Equals(parameter.TrimEnd('/'));
        }
    }
}
