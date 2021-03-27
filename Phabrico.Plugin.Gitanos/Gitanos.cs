using Phabrico.Http;
using Phabrico.Miscellaneous;
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
        public override string GetName(string locale)
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
        public override bool IsVisible(Browser browser)
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
                IEnumerable<Model.GitanosConfigurationRootPath> rootPaths = gitanosConfigurationRootPathStorage.Get(Database);
                DirectoryMonitor.Start(rootPaths);

                return true;
            }
            catch (System.Exception initializationException)
            {
                Logging.WriteException("gitanos", initializationException);
                return false;
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
