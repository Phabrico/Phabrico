using System;

namespace Phabrico.Phabricator.Data
{
    /// <summary>
    /// Model class for synchronization logging
    /// All new downloaded data from Phabricator will be logged in this datatype.
    /// It is used to view the changes from the last synchronization action
    /// </summary>
    public class SynchronizationLogging : PhabricatorObject
    {
        /// <summary>
        /// Timestamp when the task or document was last modified
        /// </summary>
        public DateTimeOffset DateModified { get; set; }

        /// <summary>
        /// Token of the user who last modified task or document
        /// </summary>
        public string LastModifiedBy { get; set; }

        /// <summary>
        /// The content of the task or document before the synchronization action
        /// </summary>
        public string PreviousContent { get; set; }

        /// <summary>
        /// True if comments were added or status/owner/priority were modified (Maniphest Task only)
        /// </summary>
        public bool MetadataIsModified { get; set; } = false;

        /// <summary>
        /// The title of the task or document
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// The URL pointing to the task or document
        /// </summary>
        public string URL { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public SynchronizationLogging()
        {
        }
    }
}