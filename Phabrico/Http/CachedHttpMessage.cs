using Phabrico.Miscellaneous;
using System;

namespace Phabrico.Http
{
    /// <summary>
    /// Phabrico contains a lot of reflection code.
    /// Results retrieved from this reflection code is cached for performance reasons.
    /// This class represents a cached instance for a HttpFound object.
    /// This can be a HtmlViewPage, a file-object, ...
    /// </summary>
    public class CachedHttpMessage
    {
        /// <summary>
        /// ContentType of cached data
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Encrypted content of the cached data
        /// </summary>
        public byte[] EncryptedData { get; set; }

        /// <summary>
        /// Content-Length of the cached data
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// Timestamp when the data was cached
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Initializes a new instance of a CachedHttpMessage
        /// </summary>
        /// <param name="encryptionKey"></param>
        /// <param name="data"></param>
        /// <param name="contentType"></param>
        public CachedHttpMessage(string encryptionKey, byte[] data, string contentType)
        {
            EncryptedData = Encryption.Encrypt(encryptionKey, data);
            Size = EncryptedData.Length;
            ContentType = contentType;
        }
    }
}
