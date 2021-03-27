using Phabrico.Miscellaneous;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Phabrico.Http.Response
{
    /// <summary>
    /// Represents the HTTP response to form a cascading stylesheet file
    /// </summary>
    public class StyleSheet : HttpFound
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="httpServer">Reference to TCP server</param>
        /// <param name="browser">Reference to browser object (i.e. session info)</param>
        /// <param name="url">URL pointing to cascading stylesheet file</param>
        public StyleSheet(Http.Server httpServer, Browser browser, string url = null) : base(httpServer, browser, url)
        {
            ContentType = "text/css";

            if (url == null)
            {
                Content = "";
            }
            else
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                string resourceName = string.Format("Phabrico.Stylesheets.{0}", url.Replace('/', '.'))
                                            .Split('?')
                                            .FirstOrDefault()
                                            .TrimEnd('.');
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        Content = reader.ReadToEnd();
                    }
                }
            }
        }

        /// <summary>
        /// Returns the CSS style properties of a given CSS declaration (e.g. a class)
        /// This method will only work if the declaration starts at the beginning of the line
        /// </summary>
        /// <param name="declaration"></param>
        /// <returns></returns>
        public string GetCssProperties(string declaration)
        {
            string regexedDeclaration = "";
            foreach (char c in declaration)
            {
                regexedDeclaration += "[" + c + "]";
            }

            Match matchCssDeclaration = RegexSafe.Match(Content,  "^" + regexedDeclaration + "\\W[^{]*[{][^}]*[}]", RegexOptions.Multiline);
            if (matchCssDeclaration.Success == false)
            {
                return "";
            }

            return matchCssDeclaration.Value;
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