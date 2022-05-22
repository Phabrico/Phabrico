using Phabrico.Miscellaneous;
using System.Collections.Generic;
using System.Linq;

namespace Phabrico.Plugin.Model
{
    /// <summary>
    /// This class represents a directory which contains a git repository
    /// </summary>
    public class GitanosConfigurationRepositoryPath
    {
        /// <summary>
        /// Full path of the local git repository
        /// </summary>
        private string _directory;

        /// <summary>
        /// Initializes a new instance of GitanosConfigurationRepositoryPath
        /// </summary>
        /// <param name="directory">Full path of local git repository</param>
        public GitanosConfigurationRepositoryPath(string directory = null)
        {
            if (directory != null)
            {
                try
                {
                    Directory = directory;
                }
                catch (LibGit2Sharp.RepositoryNotFoundException)
                {
                    _directory = null;
                }
            }
        }

        /// <summary>
        /// Number of files added to the index, which are not in the current commit
        /// </summary>
        public int NumberOfAddedFiles { get; set; }

        /// <summary>
        /// Current git branch
        /// </summary>
        public string Branch { get; private set; }

        /// <summary>
        /// List of git submodules
        /// </summary>
        public List<GitanosConfigurationRepositoryPath> SubModules { get; private set; } = new List<GitanosConfigurationRepositoryPath>();

        /// <summary>
        /// Full path of the local git repository
        /// </summary>
        public string Directory
        {
            get
            {
                return _directory;
            }

            set
            {
                _directory = value;
                try
                {
                    // set other git information
                    using (var repo = new LibGit2Sharp.Repository(_directory))
                    {
                        Branch = repo.Head.FriendlyName;

                        LibGit2Sharp.StatusOptions statusOptions = new LibGit2Sharp.StatusOptions();
                        statusOptions.IncludeUnaltered = true;
                        statusOptions.ExcludeSubmodules = true;
                        Status = repo.RetrieveStatus(statusOptions);
                        NumberOfAddedFiles = Status.Added.Count();
                        NumberModified = Status.Modified.Count();
                        NumberOfRemovedFiles = Status.Removed.Count() + Status.Missing.Count();
                        NumberOfRenamedFiles = Status.RenamedInIndex.Count() + Status.RenamedInWorkDir.Count();
                        NumberOfUntrackedFiles = Status.Untracked.Count();

                        HasUnpushedCommits = repo.Branches.Any(branch => branch.TrackingDetails.AheadBy.HasValue && branch.TrackingDetails.AheadBy.Value > 0);

                        NumberOfLocalChanges = (UseAdded ? NumberOfAddedFiles : 0)
                                             + (UseModified ? NumberModified : 0)
                                             + (UseRemoved ? NumberOfRemovedFiles : 0)
                                             + (UseRenamed ? NumberOfRenamedFiles : 0)
                                             + (UseUntracked ? NumberOfUntrackedFiles : 0);

                        LibGit2Sharp.Submodule[] submodules = repo.Submodules.ToArray();
                        foreach (LibGit2Sharp.Submodule submodule in submodules)
                        {
                            if (SubModules.Any(subrepo => submodule.Path.Equals(subrepo.Directory)))
                            {
                                continue;
                            }

                            GitanosConfigurationRepositoryPath subRepo = SubModules.FirstOrDefault(r => r.Directory.Equals(_directory + "\\" + submodule.Path));
                            if (subRepo != null)
                            {
                                subRepo.Refresh();
                            }
                            else
                            {
                                // add new submodule to current repo
                                GitanosConfigurationRepositoryPath newRepo = new GitanosConfigurationRepositoryPath(_directory + "\\" + submodule.Path);
                                if (newRepo.Directory != null && SubModules.Any(module => module.Directory.Equals(newRepo.Directory)) == false)
                                {
                                    SubModules.Add(newRepo);
                                }
                            }
                        }

                        foreach (GitanosConfigurationRepositoryPath submodule in SubModules.ToArray())
                        {
                            if (submodules.Any(subrepo => submodule.Directory.Equals(_directory + "\\" + subrepo.Path)))
                            {
                                continue;
                            }

                            // remove old submodule from current repo
                            SubModules.Remove(submodule);
                        }
                    }
                }
                catch (LibGit2Sharp.RepositoryNotFoundException)
                {
                    throw;
                }
                catch (System.Exception ex)
                {
                    Logging.WriteInfo("Gitanos-Directory", ex.Message);
                    if (ex.InnerException != null)
                    {
                        Logging.WriteInfo("Gitanos-Directory", ex.InnerException.Message);
                    }
                    foreach (string line in ex.StackTrace.Split('\n'))
                    {
                        Logging.WriteInfo("Gitanos-Directory", line.Trim('\r'));
                    }
                }
            }
        }

        /// <summary>
        /// True if any branch has commits which haven't been pushed yet
        /// </summary>
        public bool HasUnpushedCommits { get; private set; }

        /// <summary>
        /// Represents the name of the git directory.
        /// In case the repository is a submodule, the the name of the parent git directory is also included (e.g. parent\submodule1\submodule2)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Number of unstaged modifications
        /// </summary>
        public int NumberModified { get; set; }

        /// <summary>
        /// Number of local commits which haven't been pushed
        /// </summary>
        public int NumberOfLocalChanges { get; private set; }

        /// <summary>
        /// Number of files removed from the index but are existent in the current commit
        /// </summary>
        public int NumberOfRemovedFiles { get; set; }

        /// <summary>
        /// Number of files that were renamed.
        /// </summary>
        public int NumberOfRenamedFiles { get; set; }

        /// <summary>
        /// Result of git status
        /// </summary>
        public LibGit2Sharp.RepositoryStatus Status { get; private set; }

        /// <summary>
        /// Number of files existing in the working directory but are neither tracked
        /// in the index nor in the current commit.
        /// </summary>
        public int NumberOfUntrackedFiles { get; set; }

        /// <summary>
        /// If true, the number of Added items of the git-status will be added to the
        /// NumberOfLocalChanges result which is shown as a notification
        /// </summary>
        public static bool UseAdded { get; set; } = true;

        /// <summary>
        /// If true, the number of Modified items of the git-status will be added to the
        /// NumberOfLocalChanges result which is shown as a notification
        /// </summary>
        public static bool UseModified { get; set; } = true;

        /// <summary>
        /// If true, the number of Removed items of the git-status will be added to the
        /// NumberOfLocalChanges result which is shown as a notification
        /// </summary>
        public static bool UseRemoved { get; set; } = true;

        /// <summary>
        /// If true, the number of Renamed items of the git-status will be added to the
        /// NumberOfLocalChanges result which is shown as a notification
        /// </summary>
        public static bool UseRenamed { get; set; } = true;

        /// <summary>
        /// If true, the number of Untracked items of the git-status will be added to the
        /// NumberOfLocalChanges result which is shown as a notification
        /// </summary>
        public static bool UseUntracked { get; set; } = true;

        /// <summary>
        /// True if directory is excluded by means of .gitignore
        /// </summary>
        /// <param name="fullPath">directory to be validated</param>
        /// <returns>True if path is excluded</returns>
        public bool PathIsIgnored(string fullPath)
        {
            try
            {
                using (var repo = new LibGit2Sharp.Repository(Directory))
                {
                    string relativePath = "/" + fullPath.Substring(Directory.Length)
                                                        .TrimStart('/', '\\')
                                                        .Replace('\\', '/');

                    return repo.Ignore.IsPathIgnored(relativePath);
                }
            }
            catch
            {
                // if repository directory is removed, an exception will be thrown
                return false;
            }
        }

        /// <summary>
        /// Reloads the git information for current repo
        /// </summary>
        public void Refresh()
        {
            Directory = Directory;
        }
    }
}
