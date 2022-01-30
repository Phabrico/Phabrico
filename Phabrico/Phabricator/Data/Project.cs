using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;

namespace Phabrico.Phabricator.Data
{
    /// <summary>
    /// Represents an ProjectInfo record from the SQLite Phabrico database
    /// </summary>
    public class Project : PhabricatorObject
    {
        /// <summary>
        /// Token prefix to identify project objects in the Phabrico database
        /// </summary>
        public const string Prefix = "PHID-PROJ-";

        /// <summary>
        /// In case a maniphest task is not linked to a specific Phabricator project, the task will be linked to this virtual project token
        /// </summary>
        public const string None = "PHID-PROJ-NONE";

        /// <summary>
        /// In case there's a reference to an inexistant project, this tag will be used instead
        /// </summary>
        public const string Unknown = "PHID-PROJ-UNKNOWN";

        /// <summary>
        /// Synchronization selection mode
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public enum Selection
        {
            /// <summary>
            /// In case a project is part of the object to be synchronized, the object may not be uploaded
            /// </summary>
            Disallowed = 0,

            /// <summary>
            /// In case a project is part of the object to be synchronized, the object may not be uploaded
            /// However, if another project is also part of this object and this project is allowed, the object
            /// may be uploaded
            /// </summary>
            Unselected = 1,

            /// <summary>
            /// In case a project is part of the object to be synchronized, the object may be uploaded
            /// </summary>
            Selected = 2
        }

        /// <summary>
        /// Color of the project tag
        /// </summary>
        public string Color { get; set; }

        /// <summary>
        /// Name of the project
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Slug name of the project
        /// </summary>
        public string InternalName { get; set; }

        /// <summary>
        /// Description of the project
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Latest Phabricator-synchronization timestamp for all records linked to this project
        /// This timestamp will contain the timestamp of the latest modified Phriction document or Maniphest task in Phabricator plus 1 second, 
        /// if that project was part of the synchronization selection
        /// When the next synchronization action starts, it will start downloading from Phabricator from that given timestamp
        /// </summary>
        public DateTimeOffset DateSynchronized { get; set; }

        /// <summary>
        /// Synchronization selection mode
        /// </summary>
        public Selection Selected { get; set; }

        /// <summary>
        /// Initializes a new instance of a ProjectInfo record
        /// </summary>
        public Project()
        {
            Selected = Selection.Unselected;
        }

        /// <summary>
        /// Compares the current Project with another Project
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override int CompareTo(object obj)
        {
            Project other = obj as Project;
            if (other == null) return -1;

            return Name.CompareTo(other.Name);
        }

        /// <summary>
        /// Compares a Project with another Project
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public bool Equals(Project otherProject)
        {
            if (otherProject == null) return false;

            return base.Equals(otherProject);
        }
    }
}
