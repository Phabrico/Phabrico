using Phabrico.Http;
using Phabrico.Miscellaneous;

namespace Phabrico.Plugin.Extensions
{
    public class PhrictionProofReader : PluginWithoutConfigurationBase
    {
        /// <summary>
        /// Icon to be shown in Phricion's action pane
        /// </summary>
        public override string Icon
        {
            get
            {
                return "fa-check-square-o";
            }
        }

        /// <summary>
        /// Controller URL to be used in Phriction's action pane
        /// </summary>
        public override string URL
        {
            get
            {
                return "PhrictionProofReader";
            }
        }

        /// <summary>
        /// Name to be shown in Phriction's action pane
        /// </summary>
        /// <param name="locale"></param>
        /// <returns></returns>
        public override string GetName(Language locale)
        {
            return Locale.TranslateText("PluginName.PhrictionProofReader", locale);
        }

        /// <summary>
        /// Executes some initialization code.
        /// The Database property contains the Encryption key: if needed, encrypted data can be read from  or written to the SQLite database
        /// </summary>
        public override void Initialize()
        {
        }

        /// <summary>
        /// Retuns if the plugin should be visible in an application (e.g. Phriction or Maniphest)
        /// </summary>
        /// <param name="database"></param>
        /// <param name="browser"></param>
        /// <param name="phabricatorObject"></param>
        /// <returns></returns>
        public override bool IsVisibleInApplication(Storage.Database database, Browser browser, string phabricatorObject)
        {
            Storage.Phriction phrictionStorage = new Storage.Phriction();
            Phabricator.Data.Phriction masterPhrictionDocument = phrictionStorage.Get(database, phabricatorObject, Language.NotApplicable);
            if (masterPhrictionDocument == null) return false;

            Storage.Content content = new Storage.Content(database);
            Storage.Content.Translation translation = content.GetTranslation(masterPhrictionDocument.Token, browser.Session.Locale);
            if (translation != null) return true;

            return false;
        }

        /// <summary>
        /// Retuns if the plugin should be visible and accessible via the navigator menu
        /// </summary>
        /// <param name="browser"></param>
        /// <returns></returns>
        public override bool IsVisibleInNavigator(Browser browser)
        {
            return false;
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
        /// Terminates the PhrictionTranslator plugin
        /// </summary>
        public override void UnlLoad()
        {
        }
    }
}
