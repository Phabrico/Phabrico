using Phabrico.Controllers;
using Phabrico.Http.Response;

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
    }
}
