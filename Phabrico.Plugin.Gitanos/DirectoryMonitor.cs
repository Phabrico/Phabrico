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
        /// CancellationToken which allows to stop the FileSystemWatcher immediately
        /// </summary>
        private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Array of FileSystemWatchers which listen to file system change notifications in all local git repositories
        /// </summary>
        private static FileSystemWatcher[] _fileSystemWatchers = new FileSystemWatcher[0];

        /// <summary>
        /// Asynchronous task which will create a FileSystemWatcher for each GitanosConfigurationRootPath
        /// </summary>
        private static Task _taskDirectoryMonitor = null;

        /// <summary>
        /// ManualResetEvent for detecting when the cancellation action has been finished
        /// </summary>
        private static ManualResetEvent _manualResetEvent = new ManualResetEvent(false);

        /// <summary>
        /// Array of all available GitanosConfigurationRepositoryPath
        /// </summary>
        public static Model.GitanosConfigurationRepositoryPath[] Repositories { get; private set; } = new Model.GitanosConfigurationRepositoryPath[0];

        /// <summary>
        /// Collection which contains directories which don't have to be tracked.
        /// E.g. directories mentioned in .gitignore
        /// </summary>
        private static HashSet<string> _invalidDirectories = new HashSet<string>();

        /// <summary>
        /// UTC timestamp per directory when the tracked modifications should be processed.
        /// The FileSystemWatcher may send out multiple tracked changes for the same directory in the same second.
        /// To limit the number of "processing-calls", each event is postponed with 2 seconds.
        /// If a second event is received in this 2 seconds, the event for the 1st event is thrown away.
        /// </summary>
        private static Dictionary<string,DateTime> _directoryModificationTimes = new Dictionary<string, DateTime>();

        /// <summary>
        /// Collection of all available GitanosConfigurationRootPaths
        /// </summary>
        private static List<Model.GitanosConfigurationRootPath> _rootPaths = new List<Model.GitanosConfigurationRootPath>();

        /// <summary>
        /// Starts monitorring all available GitanosConfigurationRepositoryPaths
        /// </summary>
        /// <param name="rootPaths"></param>
        public static void Start(IEnumerable<Model.GitanosConfigurationRootPath> rootPaths)
        {
            if (_taskDirectoryMonitor != null)
            {
                // cancel previous task
                Cancel();
            }

            Logging.WriteInfo("Gitanos", "DirectoryMonitor started");

            _rootPaths = rootPaths.ToList();

            // start new task searching all .git directories
            CancellationToken cancellationToken = cancellationTokenSource.Token;
            _taskDirectoryMonitor = Task.Factory.StartNew(() =>
            {
                try
                {
                    List<string> gitRepositories = new List<string>();

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
                        foreach (string subdirectory in Directory.EnumerateDirectories(rootPath.Directory, "*.*", SearchOption.TopDirectoryOnly))
                        {
                            if (Directory.Exists(subdirectory + "\\.git"))
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                gitRepositories.Add(subdirectory);
                            }
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
                        gitanosConfigurationRepositoryPaths.Add(new Model.GitanosConfigurationRepositoryPath(gitRepository));
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
                }
                catch
                {
                }
                finally
                {
                    _taskDirectoryMonitor = null;
                    _manualResetEvent.Reset();

                    Logging.WriteInfo("Gitanos", "DirectoryMonitor finished");
                }
            });
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
            Logging.WriteInfo("Gitanos", "FileSystemWatcher_Changed: " + e.FullPath);

            if (_invalidDirectories.Contains(e.FullPath))
            {
                // was previously decided it was an invalid directory
                return;
            }

            Model.GitanosConfigurationRepositoryPath repository = Repositories.FirstOrDefault(repo => e.FullPath.StartsWith(repo.Directory + "\\", StringComparison.OrdinalIgnoreCase)
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

            // postpone action to be executed
            const int delay = 2000;
            lock (_directoryModificationTimes)
            {
                _directoryModificationTimes[repository.Directory] = DateTime.UtcNow.AddMilliseconds(delay);
            }

            Task.Delay(delay)
                .ContinueWith((task,obj) =>
                {
                    lock (_directoryModificationTimes)
                    {
                        // check if postpone duration is exceeded
                        FileSystemWatcherParameter parameter = obj as FileSystemWatcherParameter;
                        FileSystemEventArgs fileSystemEventArgs = (FileSystemEventArgs)parameter.Event;
                        if (_directoryModificationTimes.ContainsKey(parameter.Repository.Directory) == false ||
                            _directoryModificationTimes[parameter.Repository.Directory] > DateTime.UtcNow
                           )
                        {
                            return;
                        }

                        _directoryModificationTimes[repository.Directory] = DateTime.UtcNow.AddMilliseconds(delay);

                        if (Directory.Exists(repository.Directory) == false)
                        {
                            // repository directory was deleted
                            DirectoryMonitor.Stop();
                            DirectoryMonitor.Start(_rootPaths);
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

        /// <summary>
        /// This method is fired when a new file is created in a GitanosConfigurationRepositoryPath directory
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void FileSystemWatcher_Created(object sender, FileSystemEventArgs e)
        {
            if (System.IO.Directory.Exists(e.FullPath))
            {
                DirectoryMonitor.Stop();
                DirectoryMonitor.Start(_rootPaths);
            }
        }

        /// <summary>
        /// This method is fired when a file is removed from a GitanosConfigurationRepositoryPath directory
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void FileSystemWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            if (Repositories.Any(repo => repo.Directory.Equals(e.FullPath, StringComparison.OrdinalIgnoreCase)))
            {
                DirectoryMonitor.Stop();
                DirectoryMonitor.Start(_rootPaths);
            }
        }

        /// <summary>
        /// Cancels collecting all files from all available GitanosConfigurationRepositoryPaths
        /// </summary>
        public static void Cancel()
        {
            cancellationTokenSource.Cancel();

            // wait until previous task is completely canceled
            while (_manualResetEvent.WaitOne(0))
            {
                Thread.Sleep(100);
            }

            cancellationTokenSource = new CancellationTokenSource();
        }
    }
}
