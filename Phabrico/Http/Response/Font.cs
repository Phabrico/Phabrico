using System.IO;
using System.Linq;
using System.Reflection;

namespace Phabrico.Http.Response
{
    /// <summary>
    /// Represents a HTTP response which identifies a referenced font
    /// </summary>
    public class Font : HttpFound
    {
        private byte[] _content;

        /// <summary>
        /// Initializes a new HTTP object which identifies a Font
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="url"></param>
        public Font(Http.Server httpServer, Browser browser, string url) : base(httpServer, browser, url)
        {
            ContentType = "font/" + url.Split('?', '/').FirstOrDefault()
                                        .Split('.').LastOrDefault();

            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = string.Format("Phabrico.Fonts.{0}", url.Replace('/', '.'))
                                        .Split('?')
                                        .FirstOrDefault()
                                        .TrimEnd('.');
            Stream stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new Exception.HttpNotFound(url);
            }

            _content = new byte[stream.Length];
            stream.Read(_content, 0, _content.Length);

            stream.Dispose();
        }

        /// <summary>
        /// Sends a sequence of bytes to the web browser
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="data"></param>
        public override void Send(Browser browser, byte[] data = null)
        {
            base.Send(browser, _content); 
        }
    }
}