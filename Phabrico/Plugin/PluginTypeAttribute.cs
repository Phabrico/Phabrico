using System;

namespace Phabrico.Plugin
{
    /// <summary>
    /// Determines the type of plugin
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class PluginTypeAttribute : Attribute
    {
        /// <summary>
        /// Where should the plugin be executed
        /// </summary>
        public enum UsageType
        {
            /// <summary>
            /// Plugin is accessible via hte navigator menu
            /// </summary>
            Navigator,

            /// <summary>
            /// Plugin is accessible via the actions menu in a Phriction document
            /// </summary>
            PhrictionDocument,

            /// <summary>
            /// Plugin is accessible via the menu in the Maniphest tasks overview
            /// </summary>
            ManiphestOverview,

            /// <summary>
            /// Plugin is accessible via the actions menu  in a Maniphest task
            /// </summary>
            ManiphestTask,

            /// <summary>
            /// Plugin is accessible via the edit button on referenced file object
            /// </summary>
            FileEditor
        }

        /// <summary>
        /// Where should the plugin be executed
        /// </summary>
        public UsageType Usage { get; set; } = UsageType.Navigator;
    }
}
