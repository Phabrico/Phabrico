namespace Phabrico.Phabricator.Data
{
    /// <summary>
    /// Represents an ManiphestStatusInfo record from the SQLite Phabrico database
    /// </summary>
    public class ManiphestStatus : PhabricatorObject
    {
        /// <summary>
        /// True if maniphest is closed in case it has this status
        /// </summary>
        public bool Closed { get; set; }

        /// <summary>
        /// FontAwesome representation of the ManiphestStatus
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// Descriptive name of the ManiphestStatus
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Internal value of the ManiphestStatus
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Initializes a new ManiphestStatus instance
        /// </summary>
        public ManiphestStatus()
        {
        }
    }
}
