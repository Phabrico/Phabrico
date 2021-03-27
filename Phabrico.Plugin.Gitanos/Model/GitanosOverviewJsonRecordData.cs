namespace Phabrico.Plugin.Model
{
    /// <summary>
    /// Represents a table record in the Gitanos repositories overview screen
    /// </summary>
    public class GitanosOverviewJsonRecordData
    {
        /// <summary>
        /// Full path of the git repository
        /// </summary>
        public string Directory { get; set; }

        /// <summary>
        /// Current branch of the git repository
        /// </summary>
        public string Branch { get; set; }

        /// <summary>
        /// Number of files added to the index, which are not in the current commit
        /// </summary>
        public int Added { get; set; }

        /// <summary>
        /// Number of unstaged modifications
        /// </summary>
        public int Modified { get; set; }

        /// <summary>
        /// Number of files removed from the index but are existent in the current commit
        /// </summary>
        public int Removed { get; set; }

        /// <summary>
        /// Number of files that were renamed.
        /// </summary>
        public int Renamed { get; set; }

        /// <summary>
        /// Name of the git repository
        /// </summary>
        public string Repository { get; set; }

        /// <summary>
        /// Number of files existing in the working directory but are neither tracked
        /// in the index nor in the current commit.
        /// </summary>
        public int Untracked { get; set; }

        /// <summary>
        /// 1 if any branch has commits which haven't been pushed yet
        /// </summary>
        public int HasUnpushedCommits { get; set; }
    }
}
