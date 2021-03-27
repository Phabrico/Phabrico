using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Phabrico.Http.Response
{
    /// <summary>
    /// Represents the HTTP response to form a javascript file
    /// </summary>
    public class Script : HttpFound
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="httpServer">Reference to TCP server</param>
        /// <param name="browser">Reference to browser object (i.e. session information)</param>
        /// <param name="url">URL pointing to javascript file</param>
        public Script(Http.Server httpServer, Browser browser, string url = null) : base(httpServer, browser, url)
        {
            ContentType = "application/javascript";

            if (url == null)
            {
                Content = "";
            }
            else
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                string resourceName = string.Format("Phabrico.Scripts.{0}", url)
                                            .Split('?')
                                            .FirstOrDefault()
                                            .Trim('/');
                Stream stream = assembly.GetManifestResourceStream(resourceName);

                if (stream == null)
                {
                    resourceName = string.Format("Phabrico.Plugin.Scripts.{0}", url)
                                            .Split('?')
                                            .FirstOrDefault()
                                            .Trim('/');

                    foreach (Plugin.PluginBase plugin in Http.Server.Plugins)
                    {
                        stream = plugin.Assembly.GetManifestResourceStream(resourceName);
                        if (stream != null) break;
                    }
                }

                if (stream != null)
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        Content = reader.ReadToEnd();

                        Content = Miscellaneous.Locale.TranslateJavascript(Content, Browser.Session.Locale);
                    }

                    stream.Dispose();
                }
            }
        }

        /// <summary>
        /// Sends a sequence of bytes to the web browser
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="data"></param>
        public override void Send(Browser browser, byte[] data = null)
        {
            data = UTF8Encoding.UTF8.GetBytes(Content);
            base.Send(browser, data); 
        }
    }
}