using Phabrico.Controllers;
using Phabrico.Http;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;
using System;
using System.Linq;
using System.Reflection;

namespace Phabrico.Plugin
{
    abstract public class PluginWithoutConfigurationBase : IDisposable
    {
        /// <summary>
        /// Identifiers for the state of a plugin
        /// </summary>
        internal enum PluginState
        {
            /// <summary>
            /// Initial state: plugin has not been loaded yet
            /// </summary>
            NotLoaded,

            /// <summary>
            /// Plugin has been loaded but no user has been logged on yet.
            /// Only unencrypted database access is possible at this point
            /// </summary>
            Loaded,

            /// <summary>
            /// Plugin is loaded and a user has been logged on (he might be logged off again)
            /// Encrypted database access is possible at this point
            /// </summary>
            Initialized
        }

        public delegate void SynchronizationMethod(Synchronization.SynchronizationParameters synchronizationParameters, int processedDuration, int totalDuration);

        /// <summary>
        /// Current state of the plugin
        /// </summary>
        internal PluginState State = PluginState.NotLoaded;

        /// <summary>
        /// A plugin can be executed from several places.
        /// This property holds the origin from where the plugin was executed.
        /// </summary>
        public PluginTypeAttribute.UsageType CurrentUsageType { get; internal set; }

        /// <summary>
        /// internal link to the Phabrico database
        /// </summary>
        private Storage.Database database = null;

        /// <summary>
        /// The DLL assembly where the plugin code is located in
        /// </summary>
        public Assembly Assembly { get; set; }

        /// <summary>
        /// Link to the Phabrico database
        /// </summary>
        public Storage.Database Database
        {
            get
            {
                return database;
            }

            set
            {
                if (database != null)
                {
                    database.Dispose();
                }

                database = value;
            }
        }

        /// <summary>
        /// The FontAwesome icon name that should be shown in the navigator menu
        /// </summary>
        public virtual string Icon
        {
            get
            {
                return "";
            }
        }
        
        /// <summary>
        /// The URL which should execute this plugin.
        /// E.g.  "MyPlugin" would for example point to http://localhost:13467/MyPlugin/
        /// </summary>
        public abstract string URL { get; }

        /// <summary>
        /// This method is fired when the plugin is disposed
        /// </summary>
        public void Dispose()
        {
            if (Database != null)
            {
                Database.Dispose();
            }
        }

        /// <summary>
        /// The description that should be shown in the tooltip in the navigator menu
        /// </summary>
        /// <param name="locale">Language in which the description should be translated to</param>
        /// <returns>Translated description that will be shown in the tooltip in the navigator menu</returns>
        public virtual string GetDescription(string locale)
        {
            return "My Plugin Description";
        }

        /// <summary>
        /// The name that should be shown in the navigator menu
        /// </summary>
        /// <param name="locale">Language in which the name should be translated to</param>
        /// <returns>Translated name that will be shown in the navigator menu</returns>
        public virtual string GetName(Language locale)
        {
            return "My Plugin";
        }

        /// <summary>
        /// Returns a HtmlViewPage by name
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="viewName"></param>
        /// <returns></returns>
        public HtmlViewPage GetViewPage(Http.Browser browser, string viewName)
        {
            // determine viewName
            viewName = viewName.Split('?').FirstOrDefault();   // in case url has parameters, skip parameters
            viewName = string.Format("Phabrico.Plugin.View.{0}.html", viewName);
            viewName = GetType().Assembly
                                .GetManifestResourceNames()
                                .FirstOrDefault(resourceName => resourceName.Equals(viewName, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(viewName))
            {
                // viewName not found in plugin
                return null;
            }
            
            // load content
            HtmlViewPage htmlViewPage = new HtmlViewPage(browser.HttpServer, browser, true);
            htmlViewPage.Content = htmlViewPage.GetViewData(viewName, GetType().Assembly);
            htmlViewPage.Customize(browser);

            return htmlViewPage;
        }

        /// <summary>
        /// Executes some initialization code.
        /// The Database property contains the Encryption key: if needed, encrypted data can be read from  or written to the SQLite database
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// Retuns if the plugin should be visible in an application (e.g. Phriction or Maniphest)
        /// </summary>
        /// <param name="database"></param>
        /// <param name="browser"></param>
        /// <param name="phabricatorObject"></param>
        /// <returns></returns>
        public virtual bool IsVisibleInApplication(Storage.Database database, Http.Browser browser, string phabricatorObject)
        {
            return true;
        }

        /// <summary>
        /// Retuns if the plugin should be visible and accessible via the navigator menu
        /// </summary>
        /// <param name="browser"></param>
        /// <returns></returns>
        public abstract bool IsVisibleInNavigator(Http.Browser browser);

        /// <summary>
        /// Executes some initialization code.
        /// The Database property does not contain the Encryption key: only unencrypted data can be read from or written to the SQLite database
        /// </summary>
        /// <returns>True if initialization was successfull. If false, the plugin will not be loaded</returns>
        public abstract bool Load();

        /// <summary>
        /// Preprocesses the remarkup content before Remarkup engine decodes it to HTML
        /// </summary>
        /// <param name="browser">Browser object</param>
        /// <param name="remarkupText">Remarkup content to be preprocessed</param>
        public virtual void RemarkupPreprocessHTML(Browser browser, ref string remarkupText)
        {
        }

        /// <summary>
        /// Preprocesses the remarkup content before the remarkup is stored into the local Phabrico database
        /// (e.g. after a download from Phabricator  or  a manual edit)
        /// </summary>
        /// <param name="browser">Browser object</param>
        /// <param name="remarkupText">Remarkup content to be preprocessed</param>
        public virtual void RemarkupPreprocessSynchronization(Browser browser, ref string remarkupText)
        {
        }

        /// <summary>
        /// Executes some termination code.
        /// </summary>
        /// <returns>True if initialization was successfull. If false, the plugin will not be loaded</returns>
        public abstract void UnlLoad();

        /// <summary>
        /// If set, this method will be executed during synchronization.
        /// This can for example be used to download some (plugin-specific) data from Phabricator
        /// </summary>
        public SynchronizationMethod SynchronizationReadData = null;
    }
}
