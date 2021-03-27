using System;

namespace Phabrico.Exception
{
    /// <summary>
    /// This exception will never be thrown...
    /// ... except if you are a sneaky bastard
    /// by browsing with a public account to 
    /// specific URLs which are forbidden
    /// e.g. Configuration
    /// </summary>
    [Serializable]
    public class AccessDeniedException : System.Exception
    {
        /// <summary>
        /// URL which caused the AccessDeniedException
        /// </summary>
        public string URL { get; private set; }

        /// <summary>
        /// Extra information why a AccessDeniedException was thrown
        /// </summary>
        public string Comment { get; private set; }

        /// <summary>
        /// Initializes a AccessDeniedException instance
        /// </summary>
        /// <param name="url"></param>
        /// <param name="comment"></param>
        public AccessDeniedException(string url, string comment)
        {
            URL = url;
            Comment = comment;
        }
    }
}
