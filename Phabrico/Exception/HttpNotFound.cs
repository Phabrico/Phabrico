using System;

namespace Phabrico.Exception
{
    /// <summary>
    /// This exception will be thrown when a URL is requested which can't be processed by Phabrico.
    /// </summary>
    [Serializable]
    public class HttpNotFound : System.Exception
    {
    }
}
