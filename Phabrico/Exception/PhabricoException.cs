using System;

namespace Phabrico.Exception
{
    /// <summary>
    /// This is an exception for handling program errors
    /// Of course, this exception will never be thrown...
    /// </summary>
    [Serializable]
    public class PhabricoException : System.Exception
    {
        /// <summary>
        /// Initializes a new PhabricoException instance
        /// </summary>
        /// <param name="message"></param>
        public PhabricoException(string message) : base(message)
        {
        }
    }
}
