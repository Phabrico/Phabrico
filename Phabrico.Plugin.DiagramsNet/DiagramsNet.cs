using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Phabricator.Data;

namespace Phabrico.Plugin
{
    /// <summary>
    /// Integrates the Diagrams.Net (formerly known as Draw.io) application into Phabrico
    /// </summary>
    [PluginType(Usage = PluginTypeAttribute.UsageType.Navigator)]
    public class DiagramsNet : PluginBase
    {
        /// <summary>
        /// Icon to be shown in Phabrico's navigator
        /// </summary>
        public override string Icon
        {
            get
            {
                return "fa-sitemap";
            }
        }

        /// <summary>
        /// Controller URL to be used in Phabrico's navigator
        /// </summary>
        public override string URL
        {
            get
            {
                return "diagrams.net";
            }
        }

        /// <summary>
        /// Tooltip to be shown in Phabrico's navigator
        /// </summary>
        /// <param name="locale"></param>
        /// <returns></returns>
        public override string GetDescription(string locale)
        {
            return Locale.TranslateText("Editor for flowcharts, process diagrams, org charts, UML, ER and network diagrams.", locale);
        }

        /// <summary>
        /// Name to be shown in Phabrico's navigator
        /// </summary>
        /// <param name="locale"></param>
        /// <returns></returns>
        public override string GetName(string locale)
        {
            return Locale.TranslateText("Diagram", locale);
        }

        /// <summary>
        /// Executes some initialization code.
        /// The Database property contains the Encryption key: if needed, encrypted data can be read from  or written to the SQLite database
        /// </summary>
        public override void Initialize()
        {
        }

        /// <summary>
        /// Retuns if the plugin should be visible and accessible via the navigator menu
        /// </summary>
        /// <param name="browser"></param>
        /// <returns></returns>
        public override bool IsVisible(Browser browser)
        {
            return true;
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
        /// Terminates the Diagrams plugin
        /// </summary>
        public override void UnlLoad()
        {
        }
    }
}
