using Phabrico.Controllers;
using Phabrico.Http;
using Phabrico.Miscellaneous;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;

namespace Phabrico.Plugin
{
    /// <summary>
    /// Represents a screen in which you can manage your local git repositories
    /// </summary>
    [PluginType(Usage = PluginTypeAttribute.UsageType.Navigator)]
    public class Gitanos : PluginBase
    {
        /// <summary>
        /// Icon to be shown in Phabrico's navigator
        /// </summary>
        public override string Icon
        {
            get
            {
                return "fa-git";
            }
        }

        /// <summary>
        /// Controller URL to be used in Phabrico's navigator
        /// </summary>
        public override string URL
        {
            get
            {
                return "gitanos";
            }
        }

        /// <summary>
        /// Tooltip to be shown in Phabrico's navigator
        /// </summary>
        /// <param name="locale"></param>
        /// <returns></returns>
        public override string GetDescription(string locale)
        {
            return Locale.TranslateText("Shows the status of all local git repositories", locale);
        }

        /// <summary>
        /// Name to be shown in Phabrico's navigator
        /// </summary>
        /// <param name="locale"></param>
        /// <returns></returns>
        public override string GetName(Language locale)
        {
            return Locale.TranslateText("Gitanos", locale);
        }

        /// <summary>
        /// Executes some initialization code.
        /// The Database property contains the Encryption key: if needed, encrypted data can be read from  or written to the SQLite database
        /// </summary>
        public override void Initialize()
        {
        }

        /// <summary>
        /// Retuns if the plugin should be visible and accessible
        /// </summary>
        /// <param name="browser"></param>
        /// <returns></returns>
        public override bool IsVisibleInNavigator(Browser browser)
        {
            return true;
        }

        /// <summary>
        /// Executes some initialization code.
        /// The Database property does not contain the Encryption key: only unencrypted data can be read from or written to the SQLite database
        /// </summary>
        /// <returns>True if initialization was successfull. If false, the plugin will not be loaded</returns>
        public override bool Load()
        {
            try
            {
                // create database
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       CREATE TABLE IF NOT EXISTS gitanosConfigurationRootPath(
                           directory            VARCHAR PRIMARY KEY
                       );

                       CREATE TABLE IF NOT EXISTS gitanosPhabricatorRepositories(
                           name                 VARCHAR PRIMARY KEY,
                           uri                  VARCHAR,
                           callsign             VARCHAR,
                           shortName            VARCHAR,
                           description          VARCHAR,
                           dateModified         SQLITE3_UINT64
                       );
                   ", Database.Connection))
                {
                    dbCommand.ExecuteNonQuery();
                }

                // load git states for notification
                string[] gitStates = Storage.GitanosConfiguration.GetNotificationStates(Database);
                if (gitStates != null)
                {
                    Model.GitanosConfigurationRepositoryPath.UseAdded = gitStates.Contains("Added");
                    Model.GitanosConfigurationRepositoryPath.UseModified = gitStates.Contains("Modified");
                    Model.GitanosConfigurationRepositoryPath.UseRemoved = gitStates.Contains("Removed");
                    Model.GitanosConfigurationRepositoryPath.UseRenamed = gitStates.Contains("Renamed");
                    Model.GitanosConfigurationRepositoryPath.UseUntracked = gitStates.Contains("Untracked");
                }

                // start monitoring the local git repositories
                Storage.GitanosConfigurationRootPath gitanosConfigurationRootPathStorage = new Storage.GitanosConfigurationRootPath();
                IEnumerable<Model.GitanosConfigurationRootPath> rootPaths = gitanosConfigurationRootPathStorage.Get(Database, Language.NotApplicable);
                DirectoryMonitor.Start(rootPaths);

                SynchronizationReadData = GitanosSynchronizationReadData;

                return true;
            }
            catch (System.Exception initializationException)
            {
                Logging.WriteException("gitanos", initializationException);
                return false;
            }
        }

        private void GitanosSynchronizationReadData(Synchronization.SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration)
        {
            DateTimeOffset modifiedSince;
            Storage.GitanosPhabricatorRepository gitanosPhabricatorRepositoryStorage = new Storage.GitanosPhabricatorRepository();
            try
            {
                modifiedSince = gitanosPhabricatorRepositoryStorage.Get(synchronizationParameters.database, Language.NotApplicable)
                                                                   .Select(record => record.DateModified)
                                                                   .DefaultIfEmpty(new DateTimeOffset(1970, 1, 1, 0, 0, 1, new TimeSpan()))
                                                                   .Max(dateTimeOffset => dateTimeOffset)
                                                                   .AddSeconds(1);
            }
            catch
            {
                modifiedSince = new DateTime(1970, 1, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
            }

            Phabricator.API.Diffusion diffusion = new Phabricator.API.Diffusion();
            IEnumerable<Phabricator.Data.Diffusion> modifiedRepositories = diffusion.GetModifiedRepositories(synchronizationParameters.database,
                                                                                                             synchronizationParameters.browser.Conduit,
                                                                                                             null,
                                                                                                             modifiedSince
                                                                                                            );
            foreach (Phabricator.Data.Diffusion modifiedRepository in modifiedRepositories.Where(repo => repo.Status.Equals("Active", StringComparison.InvariantCultureIgnoreCase)))
            {
                gitanosPhabricatorRepositoryStorage.Add(synchronizationParameters.database, modifiedRepository);
            }
        }

        /// <summary>
        /// Terminates the Gitanos plugin
        /// </summary>
        public override void UnlLoad()
        {
            DirectoryMonitor.Stop();
        }
    }
}
