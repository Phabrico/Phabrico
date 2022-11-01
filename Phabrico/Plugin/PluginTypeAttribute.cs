using System;
using System.Collections.Generic;
using System.Linq;

namespace Phabrico.Plugin
{
    /// <summary>
    /// Determines the type of plugin
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class PluginTypeAttribute : Attribute
    {
        private string keyboardShortcut = "";

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
            ManiphestTask
        }

        /// <summary>
        /// Where should the plugin be executed
        /// </summary>
        public UsageType Usage { get; set; } = UsageType.Navigator;

        /// <summary>
        /// Keyboard shortcut to activate plugin
        /// </summary>
        public string KeyboardShortcut
        {
            get
            {
                return keyboardShortcut;
            }

            set
            {
                keyboardShortcut = value.ToUpper().Replace("-", "+").Replace(" ", "");

                List<string> keys = keyboardShortcut.Split('+').ToList();
                if (keys.Contains("CTRL") == false && keys.Contains("ALT") == false && keys.Contains("SHIFT") == false)
                {
                    // there is no CTRL, ALT or SHIFT
                    keyboardShortcut = "";
                    return;
                }
                
                int ctrlIndex = keys.IndexOf("CTRL");
                int altIndex = keys.IndexOf("ALT");
                int shiftIndex = keys.IndexOf("SHIFT");

                if (keys.Count != 1 + (ctrlIndex == -1 ? 0 : 1) + (altIndex == -1 ? 0 : 1) + (shiftIndex == -1 ? 0 : 1))
                {
                    // there is only CTRL, ALT and/or SHIFT (there is no specific key)
                    keyboardShortcut = "";
                    return;
                }

                if (shiftIndex != -1)
                {
                    // move SHIFT to first position
                    keys.RemoveAt(shiftIndex);
                    keys.Insert(0, "SHIFT");
                }

                if (altIndex != -1)
                {
                    // move ALT to first position
                    altIndex = keys.IndexOf("ALT");
                    keys.RemoveAt(altIndex);
                    keys.Insert(0, "ALT");
                }

                if (ctrlIndex != -1)
                {
                    // move CTRL to first position
                    ctrlIndex = keys.IndexOf("CTRL");
                    keys.RemoveAt(ctrlIndex);
                    keys.Insert(0, "CTRL");
                }

                // make sure meta keys are in the correct order
                keyboardShortcut = String.Join("+", keys);
            }
        }
    }
}
