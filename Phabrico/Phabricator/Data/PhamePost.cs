using System;

namespace Phabrico.Phabricator.Data
{
    /// <summary>
    /// Represents an PhamePostInfo record (=Blog post) from the SQLite Phabrico database
    /// </summary>
    public class PhamePost : PhabricatorObject
    {
        /// <summary>
        /// Token prefix to identify Phame objects in the Phabrico database
        /// </summary>
        public const string Prefix = "PHID-POST-";

        /// <summary>
        /// Author of the document
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Blog to where the post belongs to
        /// </summary>
        public string Blog { get; set; }

        /// <summary>
        /// Remarkup content of the document
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Timestamp when the document was last modified in Phabricator
        /// </summary>
        public DateTimeOffset DateModified { get; set; }

        /// <summary>
        /// ID of the Phame post (e.g. J123)
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// Title of the document
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Initializes a new instance of a PhamePost record
        /// </summary>
        public PhamePost()
        {
            Title = "";
            TokenPrefix = Prefix;
        }

        /// <summary>
        /// Clones a new instance of a PhamePost record
        /// </summary>
        /// <param name="original"></param>
        public PhamePost(PhamePost original)
            : base(original)
        {
            this.TokenPrefix = Prefix;

            this.Author = Author;
            this.Blog = Blog;
            this.Content = Content;
            this.DateModified = DateModified;
            this.ID = ID;
            this.Title = Title;
        }
    }
}
