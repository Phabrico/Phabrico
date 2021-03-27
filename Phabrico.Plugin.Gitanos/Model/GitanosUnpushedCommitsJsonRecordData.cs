namespace Phabrico.Plugin.Model
{
    /// <summary>
    /// Represents a UnpushedCommits table record in the Gitanos repository details screen
    /// </summary>
    public class GitanosUnpushedCommitsJsonRecordData
    {
        /// <summary>
        /// The hash-identifier of the commit which hasn't been pushed yet
        /// </summary>
        public string CommitHash { get; set; }

        /// <summary>
        /// Description of the commit
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Timestamp of the commit
        /// </summary>
        public string Timestamp { get; set; }
    }
}
