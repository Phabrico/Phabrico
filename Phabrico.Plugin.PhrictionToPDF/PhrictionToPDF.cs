using Phabrico.Http;
using Phabrico.Miscellaneous;

namespace Phabrico.Plugin
{
    /// <summary>
    /// Represents a menuitem in the Phriction screen which allows you to export a Phriction (and all underlying
    /// documents) to a PDF file
    /// </summary>
    [PluginType(Usage = PluginTypeAttribute.UsageType.PhrictionDocument)]
    public class PhrictionToPDF : PluginBase
    {
        /// <summary>
        /// Icon to be shown in Phricion's action pane
        /// </summary>
        public override string Icon
        {
            get
            {
                return "fa-file-pdf-o";
            }
        }

        /// <summary>
        /// Retuns if the plugin should be visible and accessible via the navigator menu
        /// </summary>
        /// <param name="browser"></param>
        /// <returns></returns>
        public override bool IsVisible(Browser browser)
        {
            return false;
        }

        /// <summary>
        /// Controller URL to be used in Phriction's action pane
        /// </summary>
        public override string URL
        {
            get
            {
                return "PhrictionToPDF";
            }
        }

        /// <summary>
        /// Name to be shown in Phriction's action pane
        /// </summary>
        /// <param name="locale"></param>
        /// <returns></returns>
        public override string GetName(string locale)
        {
            return Locale.TranslateText("PluginName.PhrictionToPDF", locale);
        }

        /// <summary>
        /// Executes some initialization code.
        /// The Database property contains the Encryption key: if needed, encrypted data can be read from  or written to the SQLite database
        /// </summary>
        public override void Initialize()
        {
        }

        /// <summary>
        /// Executes some initialization code.
        /// The Database property does not contain the Encryption key: only unencrypted data can be read from or written to the SQLite database
        /// </summary>
        /// <returns>True if initialization was successfull. If false, the plugin will not be loaded</returns>
        public override bool Load()
        {
            return true;
        }

        /// <summary>
        /// Terminates the PhrictionToPDF plugin
        /// </summary>
        public override void UnlLoad()
        {
        }
    }
}
