using System;

namespace Phabrico.Exception
{
    /// <summary>
    /// This exception will be thrown incase the current logged on user can not be determined.
    /// This can happen during the delayed started Preloading after the user has been logged off.
    /// Usually this should only happen during unit tests.
    /// </summary>
    [Serializable]
    public class InvalidWhoAmIException : System.Exception
    {
    }
}
