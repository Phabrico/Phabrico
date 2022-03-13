using Phabrico.Parsers.Base64;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Phabrico.Http.Response
{
    /// <summary>
    /// Represents a HTTP response which identifies a referenced file
    /// </summary>
    public class File : HttpFound
    {
        private Base64EIDOStream base64EIDOStream { get; set; }

        /// <summary>
        /// Initializes a File HTTP response
        /// </summary>
        /// <param name="inputStream"></param>
        /// <param name="contentType"></param>
        /// <param name="fileName"></param>
        /// <param name="isAttachment"></param>
        public File(Base64EIDOStream inputStream, string contentType, string fileName, bool isAttachment)
            : base(null, null, null)
        {
            base64EIDOStream = inputStream;
            ContentType = contentType;
            FileName = fileName;
            IsAttachment = isAttachment;
        }
        
        /// <summary>
        /// Initializes a File HTTP response
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="url"></param>
        /// <param name="assembly"></param>
        public File(Server httpServer, Browser browser, string url, Assembly assembly = null) : base(httpServer, browser, url)
        {
            // determine content-type
            ContentType = null;
            
            if (url.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            {
                ContentType = "image/png";
            }

            if (url.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase))
            {
                ContentType = "image/jpg";
            }

            if (url.EndsWith(".svg", System.StringComparison.OrdinalIgnoreCase))
            {
                ContentType = "image/svg+xml";
            }

            if (ContentType == null)
            {
                ContentType = "application/octet-stream";
            }

            // read data
            string filePath = url.Replace("/", ".")
                                 .TrimStart('.')
                                 .Split('?')
                                 .FirstOrDefault()
                                 .TrimEnd('.');
            Assembly[] assemblies = new Assembly[] {
                assembly,
                Assembly.GetExecutingAssembly(),
                Assembly.GetCallingAssembly()
            };

            string resourceName = null;
            foreach (Assembly assemblyToCheck in assemblies.Where(asm => asm != null))
            {
                assembly = assemblyToCheck;

                resourceName = string.Format("Phabrico.{0}", filePath);
                resourceName = assembly.GetManifestResourceNames()
                                       .FirstOrDefault(name => name.StartsWith(resourceName, System.StringComparison.OrdinalIgnoreCase)); // get case-sensitive correct name
                if (resourceName != null) break;

                // not found in Phabrico: search further in plugin code
                resourceName = string.Format("Phabrico.Plugin.{0}", filePath);

                resourceName = assembly.GetManifestResourceNames()
                                       .FirstOrDefault(name => name.StartsWith(resourceName, System.StringComparison.OrdinalIgnoreCase)); // get case-sensitive correct name

                if (resourceName != null) break;
            }

            if (resourceName == null)
            {
                throw new Exception.HttpNotFound(url);
            }

            byte[] _content;
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new Exception.HttpNotFound(url);
                }

                _content = new byte[stream.Length];
                stream.Read(_content, 0, _content.Length);
            }

            // make data available for Phabrico
            base64EIDOStream = new Base64EIDOStream();
            base64EIDOStream.WriteDecodedData(_content);
        }

        /// <summary>
        /// Sends a sequence of bytes to the web browser
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="data"></param>
        public override void Send(Browser browser, byte[] data = null)
        {
            // send data
            base.Send(browser, base64EIDOStream);
        }
    }
}