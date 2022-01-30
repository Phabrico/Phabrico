using Phabrico.Miscellaneous;
using System.Collections.Generic;
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

                try
                {
                    using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            Content = reader.ReadToEnd();
                        }
                    }
                }
                catch
                {
                    throw new Exception.HttpNotFound(url);
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

            List<string> localizedBlocks = RegexSafe.Split(Content, "^\\w*" + regexedDeclaration, RegexOptions.Multiline)
                                                    .Skip(1)
                                                    .Select(block => block.Trim(' ', '\t', '\r', '\n'))
                                                    .ToList();
            if (localizedBlocks.Any())
            {
                localizedBlocks[localizedBlocks.Count - 1] = localizedBlocks[localizedBlocks.Count - 1].Substring(0, 1 + localizedBlocks[localizedBlocks.Count - 1].IndexOf('}'));
            }

            string result = declaration + " " + string.Join("\n\n" + declaration + " ", localizedBlocks) + "\n";

            return result;
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