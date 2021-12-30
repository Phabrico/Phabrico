using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Plugin.Extensions;

namespace Phabrico.Plugin
{
    /// <summary>
    /// Represents a menuitem in the Phriction screen which allows you to translate a Phriction document and all its underlying documents
    /// </summary>
    [PluginType(Usage = PluginTypeAttribute.UsageType.PhrictionDocument)]
    public class PhrictionTranslator : PluginBase
    {
        public override PluginWithoutConfigurationBase[] Extensions
        {
            get
            {
                return new PluginWithoutConfigurationBase[] {
                    new PhrictionProofReader()
                };
            }
        }

        /// <summary>
        /// Icon to be shown in Phricion's action pane
        /// </summary>
        public override string Icon
        {
            get
            {
                return "fa-comments ";
            }
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
            if (translation == null) return true;       // no translation found: show Translate button
            if (translation.DateModified < masterPhrictionDocument.DateModified) return true;  // mster document has been updated: show Translate button
            if (translation.IsReviewed) return false;   // translation has been reviewed: do not show Translate button anymore

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
        /// Controller URL to be used in Phriction's action pane
        /// </summary>
        public override string URL
        {
            get
            {
                return "PhrictionTranslator";
            }
        }

        /// <summary>
        /// Name to be shown in Phriction's action pane
        /// </summary>
        /// <param name="locale"></param>
        /// <returns></returns>
        public override string GetName(Language locale)
        {
            return Locale.TranslateText("PluginName.PhrictionTranslator", locale);
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
        /// Terminates the PhrictionTranslator plugin
        /// </summary>
        public override void UnlLoad()
        {
        }
    }
}
