namespace Phabrico.Phabricator.Data
{
    /// <summary>
    /// Represents an ManiphestPriorityInfo record from the SQLite Phabrico database
    /// </summary>
    public class ManiphestPriority : PhabricatorObject
    {
        /// <summary>
        /// Descriptive name of the ManiphestPriority
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Internal name of the ManiphestPriority
        /// </summary>
        public string Identifier { get; set; }

        /// <summary>
        /// Priority value
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Color of the ManiphestPriority
        /// </summary>
        public string Color { get; set; }

        /// <summary>
        /// Initializes a new ManiphestPriority instance
        /// </summary>
        public ManiphestPriority()
        {
        }
    }
}
