using System;

namespace Phabrico.Exception
{
    /// <summary>
    /// This exception will be thrown in case the phabrico.exe.config file is missing
    /// </summary>
    [Serializable]
    public class InvalidConfigurationException : System.Exception
    {
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="errorMessage"></param>
        public InvalidConfigurationException(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }
    }
}
