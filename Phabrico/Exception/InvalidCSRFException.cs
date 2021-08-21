using System;

namespace Phabrico.Exception
{
    /// <summary>
    /// This exception will be thrown if a given CSRF is not known (anymore) by the HTTP Server
    /// </summary>
    [Serializable]
    public class InvalidCSRFException : System.Exception
    {
    }
}
