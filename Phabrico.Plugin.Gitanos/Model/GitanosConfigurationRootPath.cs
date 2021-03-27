namespace Phabrico.Plugin.Model
{
    /// <summary>
    /// This class represents a directory which contains directories which contain git repositories
    /// (See also GitanosConfigurationRepositoryPath)
    /// </summary>
    public class GitanosConfigurationRootPath
    {
        /// <summary>
        /// The full path of the root path
        /// </summary>
        public string Directory { get; set; }
    }
}
