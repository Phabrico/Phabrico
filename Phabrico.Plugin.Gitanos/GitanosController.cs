using LibGit2Sharp;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Controllers;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;
using Phabrico.Plugin.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Phabrico.Plugin
{
    /// <summary>
    /// Represents the controller for all the Gitanos functionalities
    /// </summary>
    public class GitanosController : PluginController
    {
        /// <summary>
        /// Returns all the file modifications for a given git repository
        /// </summary>
        /// <param name="repositoryDirectory"></param>
        /// <returns></returns>
        private IEnumerable<GitanosModificationsJsonRecordData> GetRepositoryModifications(string repositoryDirectory)
        {
            List<GitanosModificationsJsonRecordData> modifications = new List<GitanosModificationsJsonRecordData>();

            try
            {
                using (var repo = new LibGit2Sharp.Repository(repositoryDirectory))
                {
                    GitanosModificationsJsonRecordData modification;

                    LibGit2Sharp.StatusOptions statusOptions = new LibGit2Sharp.StatusOptions();
                    statusOptions.DetectRenamesInIndex = true;
                    statusOptions.DetectRenamesInWorkDir = true;
                    statusOptions.ExcludeSubmodules = true;
                    statusOptions.RecurseIgnoredDirs = true;
                    statusOptions.RecurseUntrackedDirs = true;
                    statusOptions.IncludeUnaltered = false;
                    statusOptions.IncludeUntracked = true;
                    LibGit2Sharp.RepositoryStatus Status = repo.RetrieveStatus(statusOptions);

                    foreach (LibGit2Sharp.StatusEntry added in Status.Added)
                    {
                        modification = new GitanosModificationsJsonRecordData();
                        modification.File = repositoryDirectory + "\\" + added.FilePath.Replace("/", "\\");
                        modification.ModificationType = "Added";
                        modification.ModificationTypeText = Locale.TranslateText("Gitanos::Added", browser.Session.Locale);
                        modification.Timestamp = Controller.FormatDateTimeOffset(System.IO.File.GetLastWriteTime(modification.File), browser.Session.Locale);

                        modifications.Add(modification);
                    }

                    foreach (LibGit2Sharp.StatusEntry modified in Status.Modified)
                    {
                        modification = new GitanosModificationsJsonRecordData();
                        modification.File = repositoryDirectory + "\\" + modified.FilePath.Replace("/", "\\");

                        System.IO.FileInfo fileInfo = new System.IO.FileInfo(modification.File);
                        if (fileInfo.Length > 2100000)
                        {
                            modification.ModificationType = "ModifiedTooLarge";
                        }
                        else
                        {
                            modification.ModificationType = "Modified";
                        }
                        modification.ModificationTypeText = Locale.TranslateText("Gitanos::" + modification.ModificationType, browser.Session.Locale);
                        modification.Timestamp = Controller.FormatDateTimeOffset(System.IO.File.GetLastWriteTime(modification.File), browser.Session.Locale);

                        modifications.Add(modification);
                    }

                    foreach (LibGit2Sharp.StatusEntry removed in Status.Removed.Concat(Status.Missing))
                    {
                        modification = new GitanosModificationsJsonRecordData();
                        modification.File = repositoryDirectory + "\\" + removed.FilePath.Replace("/", "\\");
                        modification.ModificationType = "Removed";
                        modification.ModificationTypeText = Locale.TranslateText("Gitanos::Removed", browser.Session.Locale);
                        modification.Timestamp = "";

                        modifications.Add(modification);
                    }

                    foreach (LibGit2Sharp.StatusEntry renamed in Status.RenamedInIndex.Concat(Status.RenamedInWorkDir))
                    {
                        modification = new GitanosModificationsJsonRecordData();
                        modification.File = repositoryDirectory + "\\" + renamed.FilePath.Replace("/", "\\");
                        modification.ModificationType = "Renamed";
                        modification.ModificationTypeText = Locale.TranslateText("Gitanos::Renamed", browser.Session.Locale);
                        modification.Timestamp = "";

                        modifications.Add(modification);
                    }

                    foreach (LibGit2Sharp.StatusEntry untracked in Status.Untracked)
                    {
                        modification = new GitanosModificationsJsonRecordData();
                        modification.File = repositoryDirectory + "\\" + untracked.FilePath.Replace("/", "\\");
                        modification.ModificationType = "Untracked";
                        modification.ModificationTypeText = Locale.TranslateText("Gitanos::Untracked", browser.Session.Locale);
                        modification.Timestamp = Controller.FormatDateTimeOffset(System.IO.File.GetLastWriteTime(modification.File), browser.Session.Locale);

                        modifications.Add(modification);
                    }
                }
            }
            catch (System.Exception exception)
            {
                Logging.WriteError("Gitanos", "GetRepositoryModifications: " + exception.Message);
            }

            return modifications;
        }

        /// <summary>
        /// This method will show the DiffFile screen
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="viewPage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/gitanos/file", ServerCache = false)]
        public HttpMessage HttpGetDiffFileScreen(Http.Server httpServer, ref HtmlViewPage viewPage, string[] parameters, string parameterActions)
        {
            lock (DirectoryMonitor.DatabaseAccess)
            {
                if (parameters.Any() == false) throw new Exception.HttpNotFound("/gitanos/file");

                string filePath = string.Join("\\", parameters);
                filePath = System.Web.HttpUtility.UrlDecode(filePath);
                filePath = string.Format("{0}:{1}", filePath[0], filePath.Substring(1));
                GitanosConfigurationRepositoryPath currentRepository = DirectoryMonitor.Repositories.FirstOrDefault(repo => filePath.StartsWith(repo.Directory));

                if (currentRepository == null)
                {
                    // can happen when Phabrico is restarted while a Gitanos screen is still open in some browser window
                    return new Http.Response.HttpRedirect(httpServer, browser, "/");
                }

                using (var repo = new LibGit2Sharp.Repository(currentRepository.Directory))
                {
                    GitanosModificationsJsonRecordData gitanosModificationsJsonRecordData = new GitanosModificationsJsonRecordData();
                    gitanosModificationsJsonRecordData.File = filePath;
                    string fileID = gitanosModificationsJsonRecordData.ID;

                    string relativeFilePath = filePath.Substring(currentRepository.Directory.Length)
                                                      .Replace("\\", "/")
                                                      .TrimStart('/');
                    PatchEntryChanges binaryModification = null;
                    LibGit2Sharp.Patch diff = repo.Diff.Compare<LibGit2Sharp.Patch>(new string[] { relativeFilePath }, true);
                    if (diff.LinesAdded == 0 && diff.LinesDeleted == 0)
                    {
                        binaryModification = diff.FirstOrDefault(modification => modification.IsBinaryComparison);
                        if (binaryModification == null  ||  binaryModification.Status != LibGit2Sharp.ChangeKind.Modified)
                        {
                            // all modifications have been unedited for current file -> go back to modifications overview of current repository
                            string redirectURL = "/gitanos/data/" + currentRepository.Directory.Replace("\\", "/") + "/";
                            return new Http.Response.HttpRedirect(httpServer, browser, redirectURL);
                        }
                    }

                    viewPage = new HtmlViewPage(httpServer, browser, true, "GitanosDiffFile", parameters);

                    int repositoryIndex = Array.IndexOf(DirectoryMonitor.Repositories, currentRepository);
                    string repositoryUrl = Http.Server.RootPath + "gitanos/data/" + currentRepository.Directory.Replace("\\", "/").Replace(":", "") + "/";
                    viewPage.SetText("REPOSITORY-INDEX", repositoryIndex.ToString(), HtmlViewPage.ArgumentOptions.JavascriptEncoding);
                    viewPage.SetText("REPO-URL", repositoryUrl, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    viewPage.SetText("REPO-PATH", currentRepository.Directory, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    viewPage.SetText("FILE-PATH", filePath.Substring(currentRepository.Directory.Length + 1), HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    viewPage.SetText("FILE-ID", fileID, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                    if (binaryModification != null)
                    {
                        viewPage.SetText("BINARY-MODIFICATION", "True");
                    }
                    else
                    {
                        viewPage.SetText("BINARY-MODIFICATION", "False");
                    }

                    foreach (string line in diff.Content
                                                .Split('\n')
                                                .SkipWhile(line => line.Any() == false || line[0] != '@')
                                                .Select(line => line.Trim('\r')))
                    {
                        string lineState = "diff";
                        if (line.Any())
                        {
                            if (line[0] == '+') lineState += " added";
                            if (line[0] == '-') lineState += " removed";
                        }

                        string code = line.TrimEnd(' ', '\t');
                        HtmlViewPage viewLine = viewPage.GetPartialView("DIFF-LINES");
                        viewLine.SetText("DIFF-LINE-STATE", lineState, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        viewLine.SetText("DIFF-LINE-CONTENT", code, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        if (line.Length > 1 && line.Length != code.Length && line.StartsWith("@@") == false)
                        {
                            string redSpan = string.Format("<span style='background:red'>{0}</span>", line.Substring(code.Length));
                            viewLine.SetText("DIFF-LINE-SPACE-ENDINGS", redSpan, HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                        }
                        else
                        {
                            viewLine.SetText("DIFF-LINE-SPACE-ENDINGS", "", HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue);
                        }
                    }

                    viewPage.Merge();
                }
            }

            return null;
        }

        /// <summary>
        /// This method is fired when the user opens the Gitanos screen (from the Phabrico navigator)
        /// or from the Gitanos overview screen itself when browsing to the modifications of a given repository
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="viewPage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/gitanos", ServerCache = false)]
        public void HttpGetOverviewScreen(Http.Server httpServer, ref HtmlViewPage viewPage, string[] parameters, string parameterActions)
        {
            using (Phabrico.Storage.Database database = new Phabrico.Storage.Database(EncryptionKey))
            {
                string repositoryDirectory = string.Join("\\", parameters.Skip(1));  // skip "data"-part in url
                if (string.IsNullOrWhiteSpace(repositoryDirectory) == false)
                {
                    // add colon character
                    repositoryDirectory = string.Format("{0}:{1}", repositoryDirectory[0], repositoryDirectory.Substring(1));
                }

                if (string.IsNullOrWhiteSpace(repositoryDirectory) || Directory.Exists(repositoryDirectory) == false)  // if invalid directory -> show overview screen
                {
                    // show overview screen
                    viewPage = new HtmlViewPage(httpServer, browser, true, "GitanosOverview", parameters);

                    string cssNotificationClasses = ".no-unpushed-commits";
                    string[] gitStates = Storage.GitanosConfiguration.GetNotificationStates(database);

                    if (gitStates == null || gitStates.Contains("Added"))
                    {
                        cssNotificationClasses += ".added";
                    }

                    if (gitStates == null || gitStates.Contains("Modified"))
                    {
                        cssNotificationClasses += ".modified";
                    }

                    if (gitStates == null || gitStates.Contains("Removed"))
                    {
                        cssNotificationClasses += ".removed";
                    }

                    if (gitStates == null || gitStates.Contains("Renamed"))
                    {
                        cssNotificationClasses += ".renamed";
                    }

                    if (gitStates == null || gitStates.Contains("Untracked"))
                    {
                        cssNotificationClasses += ".untracked";
                    }

                    viewPage.SetText("CSS-NOTIFICATIONS", cssNotificationClasses, HtmlViewPage.ArgumentOptions.AllowEmptyParameterValue | HtmlViewPage.ArgumentOptions.NoHtmlEncoding);
                }
                else
                {
                    // show detail screen
                    using (var repo = new LibGit2Sharp.Repository(repositoryDirectory))
                    {
                        lock (DirectoryMonitor.DatabaseAccess)
                        {
                            GitanosConfigurationRepositoryPath currentRepository = DirectoryMonitor.Repositories.FirstOrDefault(repository => repository.Directory.Equals(repositoryDirectory));
                            int repositoryIndex = Array.IndexOf(DirectoryMonitor.Repositories, currentRepository);

                            viewPage = new HtmlViewPage(httpServer, browser, true, "GitanosRepositoryModifications", parameters);
                            viewPage.SetText("REPOSITORY-INDEX", repositoryIndex.ToString(), HtmlViewPage.ArgumentOptions.JavascriptEncoding);
                            viewPage.SetText("REPOSITORY-NAME", repositoryDirectory, HtmlViewPage.ArgumentOptions.JavascriptEncoding);
                            viewPage.SetText("BRANCH-NAME", repo.Head.FriendlyName, HtmlViewPage.ArgumentOptions.JavascriptEncoding);
                        }
                    }
                }
            }
        }


        /// <summary>
        /// This method is fired when the user clicks on the 'Show Phabricator repositories' button in the Gitanos screen
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="viewPage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/gitanos/repositories", ServerCache = false)]
        public void HttpGetOverviewRemoteRepositoriesScreen(Http.Server httpServer, ref HtmlViewPage viewPage, string[] parameters, string parameterActions)
        {
            Storage.GitanosConfigurationRootPath storageGitanosConfigurationRootPath = new Storage.GitanosConfigurationRootPath();

            using (Phabrico.Storage.Database database = new Phabrico.Storage.Database(EncryptionKey))
            {
                string firstRootDirectory = storageGitanosConfigurationRootPath.Get(database, Language.NotApplicable).FirstOrDefault().Directory.TrimEnd('\\');

                // show overview screen
                viewPage = new HtmlViewPage(httpServer, browser, true, "GitanosOverviewRemoteRepositories", parameters);
                viewPage.SetText("GITANOS-ROOTDIRECTORY", firstRootDirectory, HtmlViewPage.ArgumentOptions.JavascriptEncoding);
            }
        }

        /// <summary>
        /// This method is fired from the Gitanos screen to fill the table.
        /// It's also executed when the search filter is changed
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/gitanos/query", ServerCache = false)]
        public void HttpGetPopulateOverviewTableData(Http.Server httpServer, ref JsonMessage jsonMessage, string[] parameters, string parameterActions)
        {
            lock (DirectoryMonitor.DatabaseAccess)
            {
                List<GitanosOverviewJsonRecordData> tableRows = new List<GitanosOverviewJsonRecordData>();
                IEnumerable<GitanosConfigurationRepositoryPath> repositories = DirectoryMonitor.Repositories;

                if (parameters.Any())
                {
                    string filter = "";
                    string orderBy = System.Web.HttpUtility.UrlDecode(parameters[0]);
                    if (parameters.Length > 1)
                    {
                        filter = System.Web.HttpUtility.UrlDecode(parameters[1]);
                    }

                    repositories = repositories.Where(repository => repository.Directory.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

                    switch (orderBy.TrimEnd('-'))
                    {
                        case "Added":
                            if (orderBy.Last() == '-')
                                repositories = repositories.OrderByDescending(o => o.NumberOfAddedFiles);
                            else
                                repositories = repositories.OrderBy(o => o.NumberOfAddedFiles);
                            break;

                        case "Branch":
                            if (orderBy.Last() == '-')
                                repositories = repositories.OrderByDescending(o => o.Branch);
                            else
                                repositories = repositories.OrderBy(o => o.Branch);
                            break;

                        case "Modified":
                            if (orderBy.Last() == '-')
                                repositories = repositories.OrderByDescending(o => o.NumberModified);
                            else
                                repositories = repositories.OrderBy(o => o.NumberModified);
                            break;

                        case "Removed":
                            if (orderBy.Last() == '-')
                                repositories = repositories.OrderByDescending(o => o.NumberOfRemovedFiles);
                            else
                                repositories = repositories.OrderBy(o => o.NumberOfRemovedFiles);
                            break;

                        case "Renamed":
                            if (orderBy.Last() == '-')
                                repositories = repositories.OrderByDescending(o => o.NumberOfRenamedFiles);
                            else
                                repositories = repositories.OrderBy(o => o.NumberOfRenamedFiles);
                            break;

                        case "Repository":
                            if (orderBy.Last() == '-')
                                repositories = repositories.OrderByDescending(o => o.Directory);
                            else
                                repositories = repositories.OrderBy(o => o.Directory);
                            break;

                        case "Untracked":
                            if (orderBy.Last() == '-')
                                repositories = repositories.OrderByDescending(o => o.NumberOfUntrackedFiles);
                            else
                                repositories = repositories.OrderBy(o => o.NumberOfUntrackedFiles);
                            break;

                        default:
                            repositories = repositories.OrderBy(o => o.Directory);
                            break;
                    }
                }

                foreach (GitanosConfigurationRepositoryPath repository in repositories)
                {
                    GitanosOverviewJsonRecordData record = new GitanosOverviewJsonRecordData();

                    record.Added = repository.NumberOfAddedFiles;
                    record.Branch = repository.Branch;
                    record.Directory = repository.Directory.Replace(":", "");
                    record.Modified = repository.NumberModified;
                    record.Removed = repository.NumberOfRemovedFiles;
                    record.Renamed = repository.NumberOfRenamedFiles;
                    record.Repository = Path.GetFileName(repository.Directory);
                    record.Untracked = repository.NumberOfUntrackedFiles;

                    record.HasUnpushedCommits = repository.HasUnpushedCommits ? 1 : 0;

                    tableRows.Add(record);
                }

                string jsonData = JsonConvert.SerializeObject(tableRows);
                jsonMessage = new JsonMessage(jsonData);
            }
        }

        /// <summary>
        /// This method is fired from the Gitanos screen to fill the modifications table.
        /// It's also executed when the search filter is changed
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/gitanos/repository/unpushed", ServerCache = false)]
        public void HttpGetPopulateRepositoryUnpushedCommitsTableData(Http.Server httpServer, ref JsonMessage jsonMessage, string[] parameters, string parameterActions)
        {
            lock (DirectoryMonitor.DatabaseAccess)
            {
                try
                {
                    List<GitanosUnpushedCommitsJsonRecordData> unpushedCommits = new List<GitanosUnpushedCommitsJsonRecordData>();

                    if (parameters.Any())
                    {
                        int repoIndex = Int32.Parse(parameters[0]);
                        if (DirectoryMonitor.Repositories.Length > repoIndex)
                        {
                            GitanosConfigurationRepositoryPath currentRepository = DirectoryMonitor.Repositories[repoIndex];
                            using (var repo = new LibGit2Sharp.Repository(currentRepository.Directory))
                            {
                                GitanosUnpushedCommitsJsonRecordData unpushedCommit;

                                if (repo.Head.TrackedBranch != null)
                                {
                                    LibGit2Sharp.Commit[] pushedCommits = repo.Head.TrackedBranch.Commits.ToArray();
                                    foreach (LibGit2Sharp.Commit commit in repo.Commits)
                                    {
                                        if (pushedCommits.Contains(commit)) break;

                                        unpushedCommit = new GitanosUnpushedCommitsJsonRecordData();
                                        unpushedCommit.CommitHash = commit.Id.Sha;
                                        unpushedCommit.Description = commit.Message;
                                        unpushedCommit.Timestamp = Controller.FormatDateTimeOffset(commit.Author.When, browser.Session.Locale);
                                        unpushedCommits.Add(unpushedCommit);
                                    }
                                }
                            }
                        }
                    }

                    string jsonData = JsonConvert.SerializeObject(unpushedCommits);
                    jsonMessage = new JsonMessage(jsonData);
                }
                catch
                {
                    jsonMessage = new JsonMessage("[]");
                }
            }
        }

        /// <summary>
        /// This method is fired when the user clicks on the Clone button
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/gitanos/repositories/clone", IntegratedWindowsSecurity = true)]
        public JsonMessage HttpPostCloneRemoteRepository(Http.Server httpServer, string[] parameters)
        {
            using (Phabrico.Storage.Database database = new Phabrico.Storage.Database(EncryptionKey))
            {
                try
                {
                    DirectoryMonitor.Stop();

                    Storage.GitanosConfigurationRootPath storageGitanosConfigurationRootPath = new Storage.GitanosConfigurationRootPath();
                    Storage.GitanosPhabricatorRepository storageGitanosPhabricatorRepository = new Storage.GitanosPhabricatorRepository();

                    string firstRootDirectory = storageGitanosConfigurationRootPath.Get(database, Language.NotApplicable).FirstOrDefault().Directory.TrimEnd('\\');
                    string workingDirectory = firstRootDirectory + "\\" + browser.Session.FormVariables[browser.Request.RawUrl]["txtCloneDestination"];
                    string repositoryName = browser.Session.FormVariables[browser.Request.RawUrl]["uriRepository"];

                    Phabricator.Data.Diffusion repository = storageGitanosPhabricatorRepository.Get(database, Language.NotApplicable).FirstOrDefault(record => record.Name.Equals(repositoryName));

                    // get authentication
                    string password;
                    string urlGitRemote = string.Join("/", repository.URI.Split('/').Take(3));
                    LibGit2Sharp.Signature gitAuthor = GetGitAuthor(urlGitRemote, out password);

                    // create local work directory
                    Directory.CreateDirectory(workingDirectory);

                    // clone
                    LibGit2Sharp.Repository.Clone(repository.URI, workingDirectory, new CloneOptions
                    {
                        CredentialsProvider = (url, usernameFromUrl, types) => new LibGit2Sharp.UsernamePasswordCredentials
                        {
                            Username = gitAuthor.Name,
                            Password = password
                        }
                    });

                    string jsonData = JsonConvert.SerializeObject(new
                    {
                        Status = "OK"
                    });
                    return new JsonMessage(jsonData);
                }
                catch (System.Exception e)
                {
                    string jsonData = JsonConvert.SerializeObject(new
                    {
                        Status = "Error",
                        Description = e.Message
                    });
                    return new JsonMessage(jsonData);
                }
                finally
                {
                    // start monitoring the local git repositories
                    Storage.GitanosConfigurationRootPath gitanosConfigurationRootPathStorage = new Storage.GitanosConfigurationRootPath();
                    IEnumerable<Model.GitanosConfigurationRootPath> rootPaths = gitanosConfigurationRootPathStorage.Get(database, Language.NotApplicable);

                    DirectoryMonitor.Start(rootPaths);
                }
            }
        }

        /// <summary>
        /// This method is fired when the user clicks on the Confirm button of the commit-dialog.
        /// The commit-dialog is shown when user selects 1 or more files and clicks on the commit button.
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/gitanos/repository/commit", IntegratedWindowsSecurity = true)]
        public JsonMessage HttpPostCommitModifications(Http.Server httpServer, string[] parameters)
        {
            lock (DirectoryMonitor.DatabaseAccess)
            {
                try
                {
                    int repositoryIndex = Int32.Parse(browser.Session.FormVariables[browser.Request.RawUrl]["repositoryIndex"]);
                    string[] modificationsForNewCommit = browser.Session.FormVariables[browser.Request.RawUrl]["modificationsForNewCommit"].Split(',');
                    string txtCommitMessage = browser.Session.FormVariables[browser.Request.RawUrl]["txtCommitMessage"];

                    GitanosConfigurationRepositoryPath currentRepository = DirectoryMonitor.Repositories[repositoryIndex];

                    using (var repo = new LibGit2Sharp.Repository(currentRepository.Directory))
                    {
                        // start committing
                        string password;
                        LibGit2Sharp.Signature gitAuthor = GetGitAuthor(repo, out password);

                        // add to index
                        foreach (GitanosModificationsJsonRecordData modification in GetRepositoryModifications(currentRepository.Directory)
                                                                                        .Where(modif => modificationsForNewCommit.Contains(modif.ID))
                                )
                        {
                            string filePath = modification.File
                                                          .Substring(currentRepository.Directory.Length)
                                                          .TrimStart('\\')
                                                          .Replace('\\', '/');
                            LibGit2Sharp.Commands.Stage(repo, filePath);
                        }

                        // commit
                        repo.Commit(txtCommitMessage, gitAuthor, gitAuthor, new LibGit2Sharp.CommitOptions());

                        string jsonData = JsonConvert.SerializeObject(new
                        {
                            Status = "OK"
                        });
                        return new JsonMessage(jsonData);
                    }
                }
                catch (System.Exception e)
                {
                    string jsonData = JsonConvert.SerializeObject(new
                    {
                        Status = "Error",
                        Description = e.Message
                    });
                    return new JsonMessage(jsonData);
                }
            }
        }

        /// <summary>
        /// Retrieves the encoding of a byte array
        /// </summary>
        /// <param name="data">Byte array to be analyzed</param>
        /// <returns>Encoding of the byte array</returns>
        private System.Text.Encoding GetEncodingFromBOM(byte[] data)
        {
            if (data.Length < 2)
            {
                return System.Text.Encoding.GetEncoding(28591);
            }

            if (data[0] == 0xFE && data[1] == 0xFF)
            {
                return System.Text.Encoding.BigEndianUnicode;
            }
            else
            if (data[0] == 0xFF && data[1] == 0xFE)
            {
                return System.Text.Encoding.Unicode;
            }

            if (data.Length >= 3)
            {
                if (data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
                {
                    return System.Text.Encoding.UTF8;
                }
                else
                if (data[0] == 0x2B && data[1] == 0x2F && data[2] == 0x76)
                {
                    return System.Text.Encoding.UTF7;
                }
            }

            if (data.Length >= 4)
            {
                if (data[0] == 0xFF && data[1] == 0xFE && data[2] == 0x00 && data[3] == 0x00)
                {
                    return System.Text.Encoding.UTF32;
                }
                else
                if (data[0] == 0x00 && data[1] == 0x00 && data[2] == 0xFE && data[3] == 0xFF)
                {
                    return new System.Text.UTF32Encoding(true, true);
                }
            }

            // BOM not found: validate all bytes in file (or until an invalid UTF8 byte is found)
            bool isUTF8 = true;
            for (int b = 0; b < data.Length; b++)
            {
                if ((data[b] & 0x80) == 0x00)
                {
                    continue;
                }

                if (b < data.Length - 1 && (data[b] & 0xE0) == 0xC0)
                {
                    if ((data[b + 1] & 0xC0) == 0x80)
                    {
                        b++;
                        continue;
                    }
                }

                if (b < data.Length - 2 && (data[b] & 0xF0) == 0xE0)
                {
                    if ((data[b + 1] & 0xC0) == 0x80)
                    {
                        if ((data[b + 2] & 0xC0) == 0x80)
                        {
                            b+=2;
                            continue;
                        }
                    }
                }

                if (b < data.Length - 3 && (data[b] & 0xF8) == 0xF0)
                {
                    if ((data[b + 1] & 0xC0) == 0x80)
                    {
                        if ((data[b + 2] & 0xC0) == 0x80)
                        {
                            if ((data[b + 3] & 0xC0) == 0x80)
                            {
                                b+=3;
                                continue;
                            }
                        }
                    }
                }

                isUTF8 = false;
                break;
            }

            if (isUTF8)
            {
                return new System.Text.UTF8Encoding(false);
            }
            else
            {
                return System.Text.Encoding.GetEncoding(28591);
            }
        }

        /// <summary>
        /// This method is fired when the user clicks on the Edit button in the Diff screen.
        /// It will read the associated file and return its content
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/gitanos/repository/editfile", IntegratedWindowsSecurity = true)]
        public PlainTextMessage HttpPostGetFileContent(Http.Server httpServer, string[] parameters)
        {
            string fileName = browser.Session.FormVariables[browser.Request.RawUrl]["filepath"];
            try
            {
                using (StreamReader streamReader = new StreamReader(fileName, true))
                {
                    byte[] data;
                    using (var memoryStream = new MemoryStream())
                    {
                        streamReader.BaseStream.CopyTo(memoryStream);
                        data = memoryStream.ToArray();

                        System.Text.Encoding encoding = GetEncodingFromBOM(data);
                        string fileContent = encoding.GetString(data);

                        return new PlainTextMessage(fileContent);
                    }
                }
            }
            catch
            {
                return new PlainTextMessage("");
            }
        }

        /// <summary>
        /// This method is fired when the user clicks on the Push button
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/gitanos/repository/push", IntegratedWindowsSecurity = true)]
        public JsonMessage HttpPostPush(Http.Server httpServer, string[] parameters)
        {
            lock (DirectoryMonitor.DatabaseAccess)
            {
                try
                {
                    int repositoryIndex = Int32.Parse(browser.Session.FormVariables[browser.Request.RawUrl]["repositoryIndex"]);

                    GitanosConfigurationRepositoryPath currentRepository = DirectoryMonitor.Repositories[repositoryIndex];

                    using (var repo = new LibGit2Sharp.Repository(currentRepository.Directory))
                    {
                        // start committing
                        string password;
                        LibGit2Sharp.Signature gitAuthor = GetGitAuthor(repo, out password);

                        // push
                        LibGit2Sharp.PushOptions options = new LibGit2Sharp.PushOptions()
                        {
                            CredentialsProvider = (url, usernameFromUrl, types) => new LibGit2Sharp.UsernamePasswordCredentials
                            {
                                Username = gitAuthor.Name,
                                Password = password
                            }
                        };
                        repo.Network.Push(repo.Branches[repo.Head.FriendlyName], options);

                        string jsonData = JsonConvert.SerializeObject(new
                        {
                            Status = "OK"
                        });
                        return new JsonMessage(jsonData);
                    }
                }
                catch (System.Exception e)
                {
                    string jsonData = JsonConvert.SerializeObject(new
                    {
                        Status = "Error",
                        Description = e.Message
                    });
                    return new JsonMessage(jsonData);
                }
            }
        }

        /// <summary>
        /// This method is fired when the user clicks on the Save button in the "Edit-File" screen (in Diff screen)
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/gitanos/repository/savefile", IntegratedWindowsSecurity = true)]
        public JsonMessage HttpPostSaveFileContent(Http.Server httpServer, string[] parameters)
        {
            string fileName = browser.Session.FormVariables[browser.Request.RawUrl]["filepath"];
            string content = browser.Session.FormVariables[browser.Request.RawUrl]["content"];

            // remove stx and etx characters from content
            content = content.Substring(1, content.Length - 2);

            try
            {
                System.Text.Encoding encoding;

                using (StreamReader streamReader = new StreamReader(fileName, true))
                {
                    byte[] data;
                    using (var memoryStream = new MemoryStream())
                    {
                        streamReader.BaseStream.CopyTo(memoryStream);
                        data = memoryStream.ToArray();

                        encoding = GetEncodingFromBOM(data);
                    }
                }

                System.IO.File.WriteAllText(fileName, content, encoding);

                string jsonData = JsonConvert.SerializeObject(new
                {
                    Status = "OK"
                });
                return new JsonMessage(jsonData);
            }
            catch
            {
                string jsonData = JsonConvert.SerializeObject(new
                {
                    Status = "Error"
                });
                return new JsonMessage(jsonData);
            }
        }

        /// <summary>
        /// Returns the git credentials for a given git repository
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        private LibGit2Sharp.Signature GetGitAuthor(LibGit2Sharp.Repository repo, out string password)
        {
            string pushUrl = repo.Network.Remotes.FirstOrDefault().PushUrl;
            pushUrl = string.Join("/", pushUrl.Split('/').Take(3));

            return GetGitAuthor(pushUrl, out password, repo);
        }

        /// <summary>
        /// Returns the git credentials for a given git repository
        /// </summary>
        /// <param name="urlGitRemote">url pointing to the remote git repository</param>
        /// <param name="password"></param>
        /// <returns></returns>
        private LibGit2Sharp.Signature GetGitAuthor(string urlGitRemote, out string password, LibGit2Sharp.Repository repo = null)
        {
            LibGit2Sharp.Signature signature = null;
            using (CredentialManagement.Credential credential = new CredentialManagement.Credential { Target = "git:" + urlGitRemote })
            {
                bool credentialsLoaded = credential.Load();
                if (credentialsLoaded)
                {
                    string emailAddress;
                    int startHostName = urlGitRemote.IndexOf('@');
                    if (startHostName > 0)
                    {
                        emailAddress = string.Format("{0}@{1}", credential.Username, urlGitRemote.Substring(startHostName + 1));
                    }
                    else
                    {
                        emailAddress = string.Format("{0}@{1}", credential.Username, urlGitRemote.Substring("http:/".Length).TrimStart('/'));
                    }

                    signature = new LibGit2Sharp.Signature(credential.Username, emailAddress, DateTimeOffset.Now);

                    password = credential.Password;

                    return signature;
                }

                password = null;

                if (repo != null) signature = repo.Config.BuildSignature(DateTimeOffset.Now);
                if (signature != null) return signature;

                if (browser.WindowsIdentity != null)
                {
                    // load git-configuration:
                    //   repo.Config might point to the git configuration of the Phabrico service account
                    //   -> retrieve the user profile path of the impersonated user (this is were the .gitconfig file is located)
                    RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                    registryKey = registryKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\" + browser.WindowsIdentity.User.Value);
                    string userProfilePath = registryKey.GetValue("ProfileImagePath", null, RegistryValueOptions.None) as string;
                    if (userProfilePath == null)
                    {
                        registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                        registryKey = registryKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\" + browser.WindowsIdentity.User.Value);
                        userProfilePath = registryKey.GetValue("ProfileImagePath", null, RegistryValueOptions.None) as string;
                    }

                    LibGit2Sharp.Configuration gitConfig = null;
                    if (userProfilePath != null)
                    {
                        try
                        {
                            gitConfig = LibGit2Sharp.Configuration.BuildFrom(userProfilePath + "\\.gitconfig");
                        }
                        catch (System.Exception exception)
                        {
                            Logging.WriteError("Gitanos", "GetGitAuthor: " + exception.Message);
                        }
                    }

                    if (gitConfig == null && repo != null)
                    {
                        // unable to load .gitconfig file of impersonated user -> proceed with the one of service account
                        gitConfig = repo.Config;
                    }

                    if (gitConfig == null)
                    {
                        // unable to load .gitconfig -> stop
                        throw new InvalidOperationException(string.Format(Locale.TranslateText("Unable to access .gitconfig for {0}", browser.Session.Locale), browser.WindowsIdentity.Name));
                    }

                    signature = gitConfig.BuildSignature(DateTimeOffset.Now);
                }

                if (signature == null)
                {
                    signature = new Signature(Environment.UserName, Environment.UserName + "@" + Environment.UserDomainName, DateTimeOffset.Now);
                }

                return signature;
            }
        }

        /// <summary>
        /// This method is fired when the repositories table is loaded by means of AJAX call
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/gitanos/repositories", IntegratedWindowsSecurity = true)]
        public JsonMessage HttpPostPopulateRemoteRepositoriesTableData(Http.Server httpServer, string[] parameters)
        {
            using (Phabrico.Storage.Database database = new Phabrico.Storage.Database(EncryptionKey))
            {
                Storage.GitanosPhabricatorRepository storageGitanosPhabricatorRepository = new Storage.GitanosPhabricatorRepository();
                Storage.GitanosConfigurationRootPath storageGitanosConfigurationRootPath = new Storage.GitanosConfigurationRootPath();
                IEnumerable<Phabricator.Data.Diffusion> repositories = storageGitanosPhabricatorRepository.Get(database, Language.NotApplicable);

                if (parameters.Any())
                {
                    string filter = "";
                    string orderBy = System.Web.HttpUtility.UrlDecode(parameters[0]);
                    if (parameters.Length > 1)
                    {
                        filter = System.Web.HttpUtility.UrlDecode(parameters[1]);
                    }

                    repositories = repositories.Where(repository => repository.CallSign.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                                                                 || repository.Description.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                                                                 || repository.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                                                                 || repository.ShortName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                                                                 || repository.URI.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                                                     );

                    switch (orderBy.TrimEnd('-'))
                    {
                        case "Name":
                            if (orderBy.Last() == '-')
                                repositories = repositories.OrderByDescending(o => o.Name);
                            else
                                repositories = repositories.OrderBy(o => o.Name);
                            break;

                        case "ShortName":
                            if (orderBy.Last() == '-')
                                repositories = repositories.OrderByDescending(o => o.ShortName);
                            else
                                repositories = repositories.OrderBy(o => o.ShortName);
                            break;

                        case "CallSign":
                            if (orderBy.Last() == '-')
                                repositories = repositories.OrderByDescending(o => o.CallSign);
                            else
                                repositories = repositories.OrderBy(o => o.CallSign);
                            break;

                        case "URI":
                            if (orderBy.Last() == '-')
                                repositories = repositories.OrderByDescending(o => o.URI);
                            else
                                repositories = repositories.OrderBy(o => o.URI);
                            break;

                        default:
                            repositories = repositories.OrderBy(o => o.Name);
                            break;
                    }
                }

                repositories = repositories.Select(record => {
                    Match uriWithCredentials = RegexSafe.Match(record.URI, "^https?://([^/\\?@&]*@)", System.Text.RegularExpressions.RegexOptions.None);
                    if (uriWithCredentials.Success)
                    {
                        // hide credentials in URI
                        Phabricator.Data.Diffusion modifiedRecord = new Phabricator.Data.Diffusion()
                        {
                            CallSign = record.CallSign,
                            DateModified = record.DateModified,
                            Description = record.Description,
                            Name = record.Name,
                            ShortName = record.ShortName,
                            Status = record.Status,
                            URI = record.URI.Substring(0, uriWithCredentials.Groups[1].Index)
                                + record.URI.Substring(uriWithCredentials.Groups[1].Index + uriWithCredentials.Groups[1].Length),
                        };

                        return modifiedRecord;
                    }
                    else
                    {
                        return record;
                    }
                });

                repositories = repositories.Select(record =>
                {
                    string defaultCloneDestination = record.Name;
                    string firstRootDirectory = storageGitanosConfigurationRootPath.Get(database, Language.NotApplicable).FirstOrDefault().Directory.TrimEnd('\\');
                    int indexer = 2;

                    while (true)
                    {
                        if (System.IO.Directory.Exists(firstRootDirectory + "\\" + defaultCloneDestination) ||
                            System.IO.File.Exists(firstRootDirectory + "\\" + defaultCloneDestination))
                        {
                            defaultCloneDestination = record.Name + "_" + indexer;
                            indexer++;
                            continue;
                        }

                        break;
                    }

                    return new Phabricator.Data.Diffusion()
                    {
                        CallSign = record.CallSign,
                        DateModified = record.DateModified,
                        Description = record.Description,
                        Name = record.Name,
                        ShortName = record.ShortName,
                        Status = record.Status,
                        URI = record.URI,

                        DefaultCloneDestination = defaultCloneDestination
                    };
                });
                string jsonData = JsonConvert.SerializeObject(repositories);
                return new JsonMessage(jsonData);
            }
        }

        /// <summary>
        /// This method is fired from the Gitanos screen to fill the modifications table.
        /// It's also executed when the search filter is changed
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterActions"></param>
        [UrlController(URL = "/gitanos/repository/modifications")]
        public JsonMessage HttpPostPopulateRepositoryModificationsTableData(Http.Server httpServer, string[] parameters)
        {
            lock (DirectoryMonitor.DatabaseAccess)
            {
                IEnumerable<GitanosModificationsJsonRecordData> filteredModifications = new GitanosModificationsJsonRecordData[0];

                if (parameters.Any())
                {
                    string filter = "";
                    string orderBy = System.Web.HttpUtility.UrlDecode(parameters[1]);
                    if (parameters.Length > 2)
                    {
                        filter = System.Web.HttpUtility.UrlDecode(string.Join("\\", parameters.Skip(2)));
                    }

                    try
                    {
                        GitanosConfigurationRepositoryPath currentRepository = DirectoryMonitor.Repositories[Int32.Parse(parameters[0])];
                        List<GitanosModificationsJsonRecordData> modifications = GetRepositoryModifications(currentRepository.Directory).ToList();

                        filteredModifications = modifications.Where(m => m.File.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

                        switch (orderBy.TrimEnd('-'))
                        {
                            case "ModificationType":
                                if (orderBy.Last() == '-')
                                    filteredModifications = filteredModifications.OrderByDescending(o => o.ModificationTypeText);
                                else
                                    filteredModifications = filteredModifications.OrderBy(o => o.ModificationTypeText);
                                break;

                            case "FileName":
                                if (orderBy.Last() == '-')
                                    filteredModifications = filteredModifications.OrderByDescending(o => o.File);
                                else
                                    filteredModifications = filteredModifications.OrderBy(o => o.File);
                                break;

                            default:
                                filteredModifications = filteredModifications.OrderBy(o => o.ModificationTypeText);
                                break;
                        }
                    }
                    catch (System.Exception exception)
                    {
                        Logging.WriteError("Gitanos", "HttpPostPopulateRepositoryModificationsTableData: " + exception.Message);
                    }
                }

                string jsonData = JsonConvert.SerializeObject(filteredModifications);
                return new JsonMessage(jsonData);
            }
        }

        /// <summary>
        /// This method is fired when the user clicks on the 'Undo modification' button in the repository-modifications screen
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/gitanos/repository/resetHEAD1", IntegratedWindowsSecurity = true)]
        public JsonMessage HttpPostResetHEAD1(Http.Server httpServer, string[] parameters)
        {
            lock (DirectoryMonitor.DatabaseAccess)
            {
                try
                {
                    int repositoryIndex = Int32.Parse(browser.Session.FormVariables[browser.Request.RawUrl]["repositoryIndex"]);

                    GitanosConfigurationRepositoryPath currentRepository = DirectoryMonitor.Repositories[repositoryIndex];

                    using (var repo = new LibGit2Sharp.Repository(currentRepository.Directory))
                    {
                        // start reset
                        repo.Reset(LibGit2Sharp.ResetMode.Mixed, repo.Head.Commits.Skip(1).First());

                        string jsonData = JsonConvert.SerializeObject(new
                        {
                            Status = "OK"
                        });
                        return new JsonMessage(jsonData);
                    }
                }
                catch (System.Exception e)
                {
                    string jsonData = JsonConvert.SerializeObject(new
                    {
                        Status = "Error",
                        Description = e.Message
                    });
                    return new JsonMessage(jsonData);
                }
            }
        }

        /// <summary>
        /// This method is fired when the user clicks on the 'Undo modification' button in the Diff-screen
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/gitanos/repository/undo", IntegratedWindowsSecurity = true)]
        public JsonMessage HttpPostUndoRepositoryModifications(Http.Server httpServer, string[] parameters)
        {
            lock (DirectoryMonitor.DatabaseAccess)
            {
                try
                {
                    int repositoryIndex = Int32.Parse(browser.Session.FormVariables[browser.Request.RawUrl]["repositoryIndex"]);
                    string modificationID = browser.Session.FormVariables[browser.Request.RawUrl]["modificationID"];

                    GitanosConfigurationRepositoryPath currentRepository = DirectoryMonitor.Repositories[repositoryIndex];

                    using (var repo = new LibGit2Sharp.Repository(currentRepository.Directory))
                    {
                        // start restoring
                        string password;
                        LibGit2Sharp.Signature gitAuthor = GetGitAuthor(repo, out password);

                        // add to index
                        foreach (GitanosModificationsJsonRecordData modification in GetRepositoryModifications(currentRepository.Directory)
                                                                                        .Where(modif => modificationID.Equals(modif.ID))
                                )
                        {
                            string filePath = modification.File
                                                          .Substring(currentRepository.Directory.Length)
                                                          .TrimStart('\\')
                                                          .Replace('\\', '/');
                            LibGit2Sharp.Commands.Unstage(repo, filePath);

                            LibGit2Sharp.CheckoutOptions checkoutOptions = new LibGit2Sharp.CheckoutOptions() { CheckoutModifiers = LibGit2Sharp.CheckoutModifiers.Force };
                            repo.CheckoutPaths(repo.Head.FriendlyName, new string[] { filePath }, checkoutOptions);

                            if (modification.ModificationType.Equals("Untracked"))
                            {
                                System.IO.File.Delete(modification.File);
                            }
                        }

                        string jsonData = JsonConvert.SerializeObject(new
                        {
                            Status = "OK"
                        });
                        return new JsonMessage(jsonData);
                    }
                }
                catch (System.Exception e)
                {
                    string jsonData = JsonConvert.SerializeObject(new
                    {
                        Status = "Error",
                        Description = e.Message
                    });
                    return new JsonMessage(jsonData);
                }
            }
        }

        /// <summary>
        /// This method is fired when the user changes some parameters in the configuration screen
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="parameters"></param>
        [UrlController(URL = "/gitanos/configuration/save")]
        public void HttpPostSaveConfiguration(Http.Server httpServer, string[] parameters)
        {
            Storage.GitanosConfigurationRootPath gitanosConfigurationRootPathStorage = new Storage.GitanosConfigurationRootPath();

            using (Phabrico.Storage.Database database = new Phabrico.Storage.Database(EncryptionKey))
            {
                string result;
                JsonMessage jsonMessage;

                // == root-directories configuration ============================================================================================
                List<GitanosConfigurationRootPath> rootPaths = new List<GitanosConfigurationRootPath>();
                string jsonArrayRootDirectories = browser.Session.FormVariables[browser.Request.RawUrl]["rootDirectories"];
                JArray rootDirectories = JsonConvert.DeserializeObject(jsonArrayRootDirectories) as JArray;
                if (rootDirectories != null)
                {
                    foreach (var rootDirectory in rootDirectories)
                    {
                        GitanosConfigurationRootPath rootPath = new GitanosConfigurationRootPath();
                        rootPath.Directory = rootDirectory.ToString();

                        // check if directory exists
                        if (Directory.Exists(rootPath.Directory) == false)
                        {
                            string errorMessage = Locale.TranslateText("Gitanos::Directory @@DIRECTORY@@ does not exist", browser.Session.Locale)
                                                        .Replace("@@DIRECTORY@@", rootPath.Directory);

                            // send error state to browser
                            result = JsonConvert.SerializeObject(new
                            {
                                status = "ERROR",
                                message = errorMessage
                            });

                            jsonMessage = new JsonMessage(result);
                            jsonMessage.Send(browser);
                            return;
                        }

                        // directory exists -> put it on the list to process it later
                        rootPaths.Add(rootPath);
                    }
                }

                // == notifications configuration ============================================================================================
                string jsonArrayGitStates = browser.Session.FormVariables[browser.Request.RawUrl]["gitStates"];

                // == save data to database ==================================================================================================
                try
                {
                    // save all root-directories to the database
                    gitanosConfigurationRootPathStorage.Overwrite(database, rootPaths);

                    // save notification states
                    Storage.GitanosConfiguration.SetNotificationStates(database, jsonArrayGitStates.Split(','));

                    // send success state to browser
                    result = JsonConvert.SerializeObject(new
                    {
                        status = "OK"
                    });

                    jsonMessage = new JsonMessage(result);
                    jsonMessage.Send(browser);

                    DirectoryMonitor.Start(rootPaths);
                }
                catch (System.Exception exception)
                {
                    // send error state to browser
                    result = JsonConvert.SerializeObject(new
                    {
                        status = "ERROR",
                        message = exception.Message
                    });

                    jsonMessage = new JsonMessage(result);
                    jsonMessage.Send(browser);
                }
            }
        }

        /// <summary>
        /// Is executed after GetConfigurationViewPage and fills in all the data in the plugin tab in the configuration screen
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="configurationTabContent"></param>
        public override void LoadConfigurationParameters(PluginBase plugin, HtmlPartialViewPage configurationTabContent)
        {
            Storage.GitanosConfigurationRootPath storageGitanosConfigurationRootPath = new Storage.GitanosConfigurationRootPath();

            using (Phabrico.Storage.Database database = new Phabrico.Storage.Database(EncryptionKey))
            {
                // notification states
                string[] gitStates = Storage.GitanosConfiguration.GetNotificationStates(database);

                if (gitStates == null || gitStates.Contains("Added"))
                {
                    configurationTabContent.SetText("ADDED", "checked");
                }
                else
                {
                    configurationTabContent.SetText("ADDED", "");
                }

                if (gitStates == null || gitStates.Contains("Modified"))
                {
                    configurationTabContent.SetText("MODIFIED", "checked");
                }
                else
                {
                    configurationTabContent.SetText("MODIFIED", "");
                }

                if (gitStates == null || gitStates.Contains("Removed"))
                {
                    configurationTabContent.SetText("REMOVED", "checked");
                }
                else
                {
                    configurationTabContent.SetText("REMOVED", "");
                }

                if (gitStates == null || gitStates.Contains("Renamed"))
                {
                    configurationTabContent.SetText("RENAMED", "checked");
                }
                else
                {
                    configurationTabContent.SetText("RENAMED", "");
                }

                if (gitStates == null || gitStates.Contains("Untracked"))
                {
                    configurationTabContent.SetText("UNTRACKED", "checked");
                }
                else
                {
                    configurationTabContent.SetText("UNTRACKED", "");
                }

                // root directories
                foreach (GitanosConfigurationRootPath rootPath in storageGitanosConfigurationRootPath.Get(database, Language.NotApplicable))
                {
                    HtmlPartialViewPage viewLocalGitRepository = configurationTabContent.GetPartialView("LOCAL-GIT-REPOSITORIES");

                    viewLocalGitRepository.SetText("ROOT-DIRECTORY", rootPath.Directory, HtmlViewPage.ArgumentOptions.JavascriptEncoding);
                }
            }
        }
    }
}