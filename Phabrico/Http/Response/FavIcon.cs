using System.IO;
using System.Reflection;

namespace Phabrico.Http.Response
{
    /// <summary>
    /// Represents a HTTP response which identifies the favicon
    /// </summary>
    public class FavIcon : HttpFound
    {
        private byte[] _content;

        /// <summary>
        /// Initializes a FavIcon response
        /// </summary>
        /// <param name="tcp"></param>
        /// <param name="browser"></param>
        /// <param name="url"></param>
        public FavIcon(Http.Server tcp, Browser browser, string url) : base(tcp, browser, url)
        {
            ContentType = "image/x-icon";
            CharSet = null;

            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = "Phabrico.Images.favicon.ico";
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                _content = new byte[stream.Length];
                stream.Read(_content, 0, _content.Length);
            }
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