using System.Security.Cryptography;
using System.Text;

namespace Phabrico.Plugin.Model
{
    /// <summary>
    /// Represents a Modifications table record in the Gitanos repository details screen
    /// </summary>
    public class GitanosModificationsJsonRecordData
    {
        /// <summary>
        /// Full path to the new, modified, renamed or deleted file
        /// </summary>
        public string File { get; set; }

        /// <summary>
        /// Unique identifier which is used in javascript for linking.
        /// It does not contain special characters like ':' or '\'
        /// </summary>
        public string ID
        {
            get
            {
                if (File != null)
                {
                    using (MD5 md5 = MD5.Create())
                    {
                        byte[] hash = md5.ComputeHash(UTF8Encoding.UTF8.GetBytes(File));

                        StringBuilder sb = new StringBuilder();
                        for (int i = 0; i < hash.Length; i++)
                        {
                            sb.Append(hash[i].ToString("x2"));
                        }
                        return "g" + sb;
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Type of modification (e.g. new, modified, renamed, deleted, ...)
        /// </summary>
        public string ModificationType { get; set; }

        /// <summary>
        /// Type of modification as displayed and translated (e.g. new, modified, renamed, deleted, ...)
        /// </summary>
        public string ModificationTypeText { get; set; }

        /// <summary>
        /// True if the modification is selected for being part of a new commit
        /// </summary>
        public bool Selected { get; set; }

        /// <summary>
        /// Timestamp when file was last modified
        /// </summary>
        public string Timestamp { get; set; }
    }
}
