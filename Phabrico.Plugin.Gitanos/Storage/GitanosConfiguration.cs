using Newtonsoft.Json.Linq;
using System.Linq;

namespace Phabrico.Plugin.Storage
{
    class GitanosConfiguration
    {
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
