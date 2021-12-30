using Newtonsoft.Json.Linq;
using System.Linq;

namespace Phabrico.Plugin.Storage
{
    class GitanosConfiguration
    {
        /// <summary>
        /// Loads the configured git notification states from the database.
        /// The number you see in the notification in the navigator, is based on this configuration.
        /// If some git state is unchecked in the configuration, the number of modifications with
        /// this git state (e.g. Added) will not be included in the summation
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public static string[] GetNotificationStates(Phabrico.Storage.Database database)
        {
            string jsonArrayGitStates = database.GetConfigurationParameter("Gitanos::gitStates");
            if (string.IsNullOrEmpty(jsonArrayGitStates) == false)
            {
                return JArray.Parse(jsonArrayGitStates)
                             .ToObject<string[]>();
            }

            return null;
        }

        /// <summary>
        /// Saves the configured git notification states in to the database
        /// See also GetNotificationStates
        /// </summary>
        /// <param name="database"></param>
        /// <param name="states"></param>
        public static void SetNotificationStates(Phabrico.Storage.Database database, string[] states)
        {
            database.SetConfigurationParameter("Gitanos::gitStates", JArray.FromObject(states)
                                                                  .ToString()
                                              );

            Model.GitanosConfigurationRepositoryPath.UseAdded = states.Contains("Added");
            Model.GitanosConfigurationRepositoryPath.UseModified = states.Contains("Modified");
            Model.GitanosConfigurationRepositoryPath.UseRemoved = states.Contains("Removed");
            Model.GitanosConfigurationRepositoryPath.UseRenamed = states.Contains("Renamed");
            Model.GitanosConfigurationRepositoryPath.UseUntracked = states.Contains("Untracked");
        }
    }
}
