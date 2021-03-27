using System;

namespace Phabrico.Exception
{
    /// <summary>
    /// This exception will be thrown when the token has been invalidated and the user is trying to access another page
    /// (It will trigger the login dialog again)
    /// </summary>
    [Serializable]
    public class AuthorizationException : System.Exception
    {
    }
}
