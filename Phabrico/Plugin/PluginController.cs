using Phabrico.Controllers;
using Phabrico.Http.Response;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Phabrico.Plugin
{
    public class PluginController : Controller
    {
        /// <summary>
        /// For a Phriction/Maniphest plugin, the plugin controller method might be executed twice.
        /// The first run may execute some checks and shows a yes/no/cancel messagebox.
        /// If the user clicks on yes/no, a second run is executed which will perform the final action.
        /// This is not a requirement: the first run can also execute the final action without showing
        /// a messagebox.
        /// To show a messagebox, the first run should return a JSON with a Status and a Message property.
        /// The Status property should be "Confirm"
        /// E.g. { Status = "Confirm", Message = "Are you sure ?" }
        /// </summary>
        public enum ConfirmResponse
        {
            /// <summary>
            /// Initial state. Will execute the first run
            /// </summary>
            None,

            /// <summary>
            /// Second run: user clicked Yes button
            /// </summary>
            Yes,

            /// <summary>
            /// Second run: user clicked No button
            /// </summary>
            No
        }

        /// <summary>
        /// Subclass which contains the formdata for the Maniphest task
        /// These data can be used in the plugin code
        /// </summary>
        public class ManiphestTaskDataType
        {
            public string TaskID { get; set; }
            public ConfirmResponse ConfirmState { get; set; }
        }

        /// <summary>
        /// Subclass which contains the formdata for the Phriction document
        /// These data can be used in the plugin code
        /// </summary>
        public class PhrictionDataType
        {
            public string Content { get; set; }
            public string TOC { get; set; }
            public string Crumbs { get; set; }
            public string Path { get; set; }
            public bool IsPrepared { get; set; }
            public ConfirmResponse ConfirmState { get; set; }
        }
        
        public ManiphestTaskDataType ManiphestTaskData { get; set; }
        public PhrictionDataType PhrictionData { get; set; }

        /// <summary>
        /// Is executed after GetConfigurationViewPage and fills in all the data in the plugin tab in the configuration screen
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="configurationTabContent"></param>
        public virtual void LoadConfigurationParameters(PluginBase plugin, HtmlPartialViewPage configurationTabContent)
        {
        }

        /// <summary>
        /// Reads embedded resource files from plugins
        /// </summary>
        /// <param name="assembly"></param>
        /// <param name="httpServer"></param>
        /// <param name="rootPath"></param>
        /// <param name="originalURL"></param>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        protected HttpFound ReadResourceContent(Assembly assembly, Http.Server httpServer, string rootPath, string originalURL, string resourceName)
        {
            string fileExtenstion = resourceName.Split('.')
                                                .LastOrDefault()
                                                .ToLower();

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    string content = reader.ReadToEnd();

                    if (fileExtenstion.StartsWith("htm"))
                    {
                        HtmlViewPage htmlViewPage = new HtmlViewPage(httpServer, browser, false, originalURL);
                        htmlViewPage.Content = content;

                        return htmlViewPage;
                    }

                    if (fileExtenstion.StartsWith("css"))
                    {
                        StyleSheet styleSheet = new StyleSheet(httpServer, browser);
                        styleSheet.Content = content;
                        return styleSheet;
                    }

                    if (fileExtenstion.StartsWith("js"))
                    {
                        Script javaScript = new Script(httpServer, browser);
                        javaScript.Content = content;
                        return javaScript;
                    }

                    if (fileExtenstion.StartsWith("txt"))
                    {
                        Http.Response.PlainTextMessage plainTextMessage = new PlainTextMessage(content);
                        return plainTextMessage;
                    }

                    return new Http.Response.File(
                        httpServer, 
                        browser, 
                        rootPath.TrimEnd('/') + "/" + originalURL.Split('?').FirstOrDefault(),
                        assembly
                    );
                }
            }
        }
    }
}
