using System;

namespace Phabrico.Phabricator.Data
{
    /// <summary>
    /// Represents an UserInfo record from the SQLite Phabrico database
    /// </summary>
    public class User : PhabricatorObject
    {
        /// <summary>
        /// Token prefix to identify user objects in the Phabrico database
        /// </summary>
        public const string Prefix = "PHID-USER-";

        /// <summary>
        /// This token is used for maniphest tasks which are not assigned to a user.
        /// (They are assigned to this token instead)
        /// </summary>
        public const string None = "PHID-USER-NONE";

        /// <summary>
        /// The real name of the user
        /// </summary>
        public string RealName { get; set; }

        /// <summary>
        /// The shortened name of the user
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// True if the user was selected in Phabrico's Users screen.
        /// This property is used for limiting the import Maniphest data from Phabricator
        /// See also Phabrico's configuration screen
        /// </summary>
        public bool Selected { get; set; }

        /// <summary>
        /// Latest Phabricator-synchronization timestamp for all records linked to this user
        /// This timestamp will contain the timestamp of the latest modified Phriction document or Maniphest task in Phabricator plus 1 second, 
        /// if that user was part of the synchronization selection
        /// When the next synchronization action starts, it will start downloading from Phabricator from that given timestamp
        /// </summary>
        public DateTimeOffset DateSynchronized { get; set; }

        /// <summary>
        /// Compares the current User with another User
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override int CompareTo(object obj)
        {
            User other = obj as User;
            return RealName.CompareTo(other.RealName);
        }
    }
}
