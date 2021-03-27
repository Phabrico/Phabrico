using System;

namespace Phabrico.Phabricator.Data
{
    /// <summary>
    /// Represents an FavoriteObject record from the SQLite Phabrico database
    /// </summary>
    public class FavoriteObject : PhabricatorObject
    {
        /// <summary>
        /// The name of the account to which this favorite object is linked to
        /// </summary>
        public string AccountUserName { get; set; }

        /// <summary>
        /// The number of the order in which the object is to be displayed
        /// </summary>
        public Int64 DisplayOrder { get; set; } = 0;
    }
}