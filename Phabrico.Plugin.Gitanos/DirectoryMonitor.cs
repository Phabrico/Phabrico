using Phabrico.Miscellaneous;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Phabrico.Plugin
{
    /// <summary>
    /// Tracks all changes in all local git directories by means of FileSystemWatchers.
    /// </summary>
    public static class DirectoryMonitor
    {
        /// <summary>
        /// Subclass which represents a parameter-class for a FileSystemWatcher event
        /// This subclass is basically been used for checking the postpone timeout.
        /// The FileSystemWatcher may send out multiple tracked changes for the same directory in the same second.
        /// To limit the number of "processing-calls", each event is postponed with 2 seconds.
        /// If a second event is received in this 2 seconds, the event for the 1st event is thrown away.
        /// </summary>
        private class FileSystemWatcherParameter
        {
            /// <summary>
            /// The event spawn by the FileSystemWatcher
            /// </summary>
            public FileSystemEventArgs Event { get; set; }

            /// <summary>
            /// GitanosConfigurationRepositoryPath object for which the event was spawn
            /// </summary>
            public Model.GitanosConfigurationRepositoryPath Repository { get; set; }
        }

        /// <summary>
        /// Synchronization object for keeping calls from different threads separately
        /// </summary>
        public static object DatabaseAccess = new object();

        /// <summary>
        /// Synchronization object for FileSystemWatcher_Changed event
        /// </summary>
        public static object FileSystemWatcherAccess = new object();

        /// <summary>
        /// CancellationToken which allows to stop the FileSystemWatcher immediately
        /// </summary>
        private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Array of FileSystemWatchers which listen to file system change notifications in all local git repositories
        /// </summary>
        private static FileSystemWatcher[] _fileSystemWatchers = new FileSystemWatcher[0];

        /// <summary>
        /// Array of all available GitanosConfigurationRepositoryPath
        /// </summary>
        public static Model.GitanosConfigurationRepositoryPath[] Repositories { get; private set; } = new Model.GitanosConfigurationRepositoryPath[0];

        /// <summary>
        /// Collection which contains directories which don't have to be tracked.
        /// E.g. directories mentioned in .gitignore
        /// </summary>
        private readonly static HashSet<string> _invalidDirectories = new HashSet<string>();

        /// <summary>
        /// UTC timestamp per directory when the tracked modifications should be processed.
        /// The FileSystemWatcher may send out multiple tracked changes for the same directory in the same second.
        /// To limit the number of "processing-calls", each event is postponed with 2 seconds.
        /// If a second event is received in this 2 seconds, the event for the 1st event is thrown away.
        /// </summary>
        private readonly static Dictionary<string,DateTime> _directoryModificationTimes = new Dictionary<string, DateTime>();
        private static object _directoryModificationTimesSynchronization = new object();

        /// <summary>
        /// Collection of all available GitanosConfigurationRootPaths
        /// </summary>
        private static List<Model.GitanosConfigurationRootPath> _rootPaths = new List<Model.GitanosConfigurationRootPath>();

        const int executionDelay = 2000;

        /// <summary>
        /// Starts monitorring all available GitanosConfigurationRepositoryPaths
        /// </summary>
        /// <param name="rootPaths"></param>
        public static void Start(IEnumerable<Model.GitanosConfigurationRootPath> rootPaths)
        {
            Http.Server.StartUnimpersonatedThread(() =>
            {
                _rootPaths = rootPaths.ToList();
            },
            (cancellationToken) =>
            {
                List<string> gitRepositories = new List<string>();

                Http.Server.SendNotificationBusy("/gitanos/notification");

                List<string> processedRootPathNames = new List<string>();
                List<FileSystemWatcher> fileSystemWatchers = new List<FileSystemWatcher>();
                foreach (Model.GitanosConfigurationRootPath rootPath in _rootPaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (processedRootPathNames.Contains(rootPath.Directory, StringComparer.OrdinalIgnoreCase))
                    {
                        // duplicated rootpath: ignore
                        continue;
                    }

                    // go through all directories of rootpath and search for ".git" directories
                    foreach (string subdirectory in Directory.EnumerateDirectories(rootPath.Directory, "*.*", SearchOption.TopDirectoryOnly)
                                                                    .Where(directory => Directory.Exists(directory + "\\.git"))
                                )
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        gitRepositories.Add(subdirectory);
                    }

                    processedRootPathNames.Add(rootPath.Directory);

                    FileSystemWatcher fileSystemWatcher = new FileSystemWatcher(rootPath.Directory);
                    fileSystemWatcher.EnableRaisingEvents = true;
                    fileSystemWatcher.IncludeSubdirectories = true;
                    fileSystemWatcher.Changed += FileSystemWatcher_Changed;
                    fileSystemWatcher.Created += FileSystemWatcher_Created;
                    fileSystemWatcher.Deleted += FileSystemWatcher_Deleted;
                    fileSystemWatchers.Add(fileSystemWatcher);
                }

                List<Model.GitanosConfigurationRepositoryPath> gitanosConfigurationRepositoryPaths = new List<Model.GitanosConfigurationRepositoryPath>();
                foreach (string gitRepository in gitRepositories)
                {
                    Model.GitanosConfigurationRepositoryPath gitanosRepo = new Model.GitanosConfigurationRepositoryPath(gitRepository);
                    gitanosRepo.Name = System.IO.Path.GetFileName(gitanosRepo.Directory);
                    gitanosConfigurationRepositoryPaths.Add(gitanosRepo);

                    foreach (Model.GitanosConfigurationRepositoryPath childRepo in gitanosRepo.SubModules)
                    {
                        childRepo.Name = gitanosRepo.Name + "\t" + System.IO.Path.GetFileName(childRepo.Directory);
                        gitanosConfigurationRepositoryPaths.Add(childRepo);
                        foreach (Model.GitanosConfigurationRepositoryPath grandchildRepo in childRepo.SubModules)
                        {
                            grandchildRepo.Name = childRepo.Name + "\t" + System.IO.Path.GetFileName(grandchildRepo.Directory);
                            gitanosConfigurationRepositoryPaths.Add(grandchildRepo);
                            foreach (Model.GitanosConfigurationRepositoryPath greatgrandchildRepo in grandchildRepo.SubModules)
                            {
                                greatgrandchildRepo.Name = grandchildRepo.Name + "\t" + System.IO.Path.GetFileName(greatgrandchildRepo.Directory);
                                gitanosConfigurationRepositoryPaths.Add(greatgrandchildRepo);
                            }
                        }
                    }
                }

                lock (DirectoryMonitor.DatabaseAccess)
                {
                    // reset FileSystemWatchers
                    foreach (FileSystemWatcher previousFileSystemWatcher in _fileSystemWatchers)
                    {
                        previousFileSystemWatcher.EnableRaisingEvents = false;
                        previousFileSystemWatcher.Changed -= FileSystemWatcher_Changed;
                    }
                    _fileSystemWatchers = fileSystemWatchers.ToArray();

                    Repositories = gitanosConfigurationRepositoryPaths.ToArray();

                    if (Repositories.Any(repo => repo.HasUnpushedCommits))
                    {
                        Http.Server.SendNotificationError("/gitanos/notification", Repositories.Sum(repo => repo.NumberOfLocalChanges).ToString());
                    }
                    else
                    {
                        int numberOfLocalChanges = Repositories.Sum(repo => repo.NumberOfLocalChanges);
                        if (numberOfLocalChanges == 0)
                        {
                            Http.Server.SendNotificationInformation("/gitanos/notification", "");  // don't show a notification
                        }
                        else
                        {
                            Http.Server.SendNotificationInformation("/gitanos/notification", numberOfLocalChanges.ToString());
                        }
                    }
                }
            },
            "Gitanos",
            "DirectoryMonitor");
        }

        /// <summary>
        /// Stops monitorring all available GitanosConfigurationRepositoryPaths
        /// </summary>
        public static void Stop()
        {
            foreach (FileSystemWatcher fileSystemWatcher in _fileSystemWatchers)
            {
                fileSystemWatcher.EnableRaisingEvents = false;
                fileSystemWatcher.Changed -= FileSystemWatcher_Changed;
            }

            _fileSystemWatchers = new FileSystemWatcher[0];
            Repositories = new Model.GitanosConfigurationRepositoryPath[0];
        }

        /// <summary>
        /// This method is fired when a file is modified in a GitanosConfigurationRepositoryPath directory
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            Model.GitanosConfigurationRepositoryPath[] affectedRepositories;
            DateTime nextExecutiontimeStamp = DateTime.UtcNow.AddMilliseconds(executionDelay);

            lock (FileSystemWatcherAccess)
            {
                if (_invalidDirectories.Contains(e.FullPath))
                {
                    // was previously decided it was an invalid directory
                    return;
                }

                affectedRepositories = Repositories.OrderByDescending(repo => repo.Directory.Length)
                                                   .Where(repo => e.FullPath.StartsWith(repo.Directory + "\\", 
                                                                                        StringComparison.OrdinalIgnoreCase
                                                                                       )
                                                               || e.FullPath.Equals(repo.Directory, 
                                                                                    StringComparison.OrdinalIgnoreCase
                                                                                   )
                                                         )
                                                   .ToArray();

                if (affectedRepositories.Any() == false)
                {
                    // not a git repo -> skip it
                    _invalidDirectories.Add(e.FullPath);
                    return;
                }

                foreach (Model.GitanosConfigurationRepositoryPath repository in affectedRepositories)
                {
                    if (e.FullPath.Contains("\\.git\\logs\\HEAD") == false &&                                               //  .git\logs\HEAD is changed when a commit is created/deleted
                        (e.FullPath.Contains("\\.git\\modules\\") && e.FullPath.EndsWith("\\logs\\HEAD")) == false &&       //  .git\modules\xxx\logs\HEAD is changed when a commit is created/deleted in a submodule
                        RegexSafe.IsMatch(e.FullPath, @"\\.git\\(modules\\[^\\]+\\)*(logs\\)?refs\\remotes\\origin", System.Text.RegularExpressions.RegexOptions.None) == false &&    // .git\refs\remotes\origin is accessed by git-push command
                        repository.PathIsIgnored(e.FullPath)
                       )
                    {
                        // directory is excluded by means of .gitignore
                        _invalidDirectories.Add(e.FullPath);
                        return;
                    }

                    // postpone action to be executed
                    lock (_directoryModificationTimesSynchronization)
                    {
                        _directoryModificationTimes[repository.Directory] = nextExecutiontimeStamp;
                    }
                }
            }

            Thread.Sleep(500);

            lock (FileSystemWatcherAccess)
            {

                foreach (Model.GitanosConfigurationRepositoryPath repository in affectedRepositories)
                {
                    if (nextExecutiontimeStamp != _directoryModificationTimes[repository.Directory])
                    {
                        continue;
                    }

                    Task.Delay(executionDelay)
                        .ContinueWith((task, obj) =>
                        {
                            FileSystemWatcherParameter parameter = obj as FileSystemWatcherParameter;

                            lock (_directoryModificationTimesSynchronization)
                            {
                            // check if postpone duration is exceeded
                            if (parameter == null) return;

                                DateTime ealiestNewModificationTime;
                                if (_directoryModificationTimes.TryGetValue(parameter.Repository.Directory, out ealiestNewModificationTime) == false) return;
                                if (ealiestNewModificationTime > DateTime.UtcNow) return;
                                _directoryModificationTimes[repository.Directory] = DateTime.UtcNow.AddMilliseconds(executionDelay);
                            }

                            Thread.Sleep(100);

                            lock (_directoryModificationTimesSynchronization)
                            {

                                if (Directory.Exists(repository.Directory) == false)
                                {
                                    // repository directory was deleted
                                    DirectoryMonitor.Stop();
                                    DirectoryMonitor.Start(_rootPaths);

                                    Http.Server.SendNotificationBusy("/gitanos/notification");
                                }
                                else
                                {
                                    // reload git information for current repo
                                    parameter.Repository.Refresh();

                                    // update all browsers
                                    Logging.WriteInfo("Gitanos-notify", parameter.Repository.Directory);
                                    if (Repositories.Any(repo => repo.HasUnpushedCommits))
                                    {
                                        Http.Server.SendNotificationError("/gitanos/notification", Repositories.Sum(repo => repo.NumberOfLocalChanges).ToString());
                                    }
                                    else
                                    {
                                        Http.Server.SendNotificationInformation("/gitanos/notification", Repositories.Sum(repo => repo.NumberOfLocalChanges).ToString());
                                    }
                                }
                            }
                        },
                        new FileSystemWatcherParameter
                        {
                            Event = e,
                            Repository = repository
                        });
                }
            }
        }

        /// <summary>
        /// This method is fired when a new file is created in a GitanosConfigurationRepositoryPath directory
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void FileSystemWatcher_Created(object sender, FileSystemEventArgs e)
        {
            DateTime nextExecutiontimeStamp;
            Model.GitanosConfigurationRepositoryPath repository;

            if (System.IO.Directory.Exists(e.FullPath) == false)
            {
                return;
            }

            lock (FileSystemWatcherAccess)
            {
                repository = Repositories.FirstOrDefault(repo => e.FullPath.StartsWith(repo.Directory + "\\", StringComparison.OrdinalIgnoreCase)
                                                      || e.FullPath.Equals(repo.Directory, StringComparison.OrdinalIgnoreCase)
                                                );
                if (repository == null)
                {
                    // not a git repo -> skip it
                    _invalidDirectories.Add(e.FullPath);
                    return;
                }

                if (e.FullPath.Contains("\\.git\\logs\\HEAD") == false &&               //  .git\logs\HEAD is changed when a commit is created/deleted
                    e.FullPath.Contains("\\.git\\refs\\remotes\\origin") == false &&    // .git\refs\remotes\origin is accessed by git-push command
                    repository.PathIsIgnored(e.FullPath)
                   )
                {
                    // directory is excluded by means of .gitignore
                    _invalidDirectories.Add(e.FullPath);
                    return;
                }
            }

            // postpone action to be executed
            lock (_directoryModificationTimesSynchronization)
            {
                nextExecutiontimeStamp = DateTime.UtcNow.AddMilliseconds(executionDelay);
                _directoryModificationTimes[repository.Directory] = nextExecutiontimeStamp;
            }

            Thread.Sleep(500);

            lock (FileSystemWatcherAccess)
            {
                if (nextExecutiontimeStamp != _directoryModificationTimes[repository.Directory])
                {
                    return;
                }

                Task.Delay(executionDelay)
                    .ContinueWith((task) =>
                    {
                        DirectoryMonitor.Stop();
                        DirectoryMonitor.Start(_rootPaths);

                        Http.Server.SendNotificationBusy("/gitanos/notification");
                    });
            }
        }

        /// <summary>
        /// This method is fired when a file is removed from a GitanosConfigurationRepositoryPath directory
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void FileSystemWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            DateTime nextExecutiontimeStamp;
            Model.GitanosConfigurationRepositoryPath repository;

            if (Repositories.Any(repo => repo.Directory.Equals(e.FullPath, StringComparison.OrdinalIgnoreCase)) == false)
            {
                return;
            }

            lock (FileSystemWatcherAccess)
            {
                repository = Repositories.FirstOrDefault(repo => e.FullPath.StartsWith(repo.Directory + "\\", StringComparison.OrdinalIgnoreCase)
                                                      || e.FullPath.Equals(repo.Directory, StringComparison.OrdinalIgnoreCase)
                                                );
                if (repository == null)
                {
                    // not a git repo -> skip it
                    _invalidDirectories.Add(e.FullPath);
                    return;
                }

                if (e.FullPath.Contains("\\.git\\logs\\HEAD") == false &&               //  .git\logs\HEAD is changed when a commit is created/deleted
                    e.FullPath.Contains("\\.git\\refs\\remotes\\origin") == false &&    // .git\refs\remotes\origin is accessed by git-push command
                    repository.PathIsIgnored(e.FullPath)
                   )
                {
                    // directory is excluded by means of .gitignore
                    _invalidDirectories.Add(e.FullPath);
                    return;
                }
            }

            // postpone action to be executed
            lock (_directoryModificationTimesSynchronization)
            {
                nextExecutiontimeStamp = DateTime.UtcNow.AddMilliseconds(executionDelay);
                _directoryModificationTimes[repository.Directory] = nextExecutiontimeStamp;
            }

            Thread.Sleep(500);

            lock (FileSystemWatcherAccess)
            {
                if (nextExecutiontimeStamp != _directoryModificationTimes[repository.Directory])
                {
                    return;
                }

                Task.Delay(executionDelay)
                    .ContinueWith((task) =>
                    {
                        DirectoryMonitor.Stop();
                        DirectoryMonitor.Start(_rootPaths);

                        Http.Server.SendNotificationBusy("/gitanos/notification");
                    });
            }
        }
    }
}
