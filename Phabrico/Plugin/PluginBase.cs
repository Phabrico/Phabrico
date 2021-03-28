using System;
using System.Linq;
using System.Reflection;
using Phabrico.Http;
using Phabrico.Http.Response;

namespace Phabrico.Plugin
{
    abstract public class PluginBase : IDisposable
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

        /// <summary>
        /// Current state of the plugin
        /// </summary>
        internal PluginState State = PluginState.NotLoaded;

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
        /// Returns the HtmlViewPage which is shown in the configuration screen
        /// If NULL is returned, no plugin-specific configuration screen is shown.
        /// </summary>
        /// <param name="browser"></param>
        /// <returns></returns>
        public HtmlViewPage GetConfigurationViewPage(Browser browser)
        {
            if (IsVisible(browser) == false)
            {
                return null;
            }

            return GetViewPage(browser, "Configuration");
        }

        /// <summary>
        /// The name that should be shown in the navigator menu
        /// </summary>
        /// <param name="locale">Language in which the name should be translated to</param>
        /// <returns>Translated name that will be shown in the navigator menu</returns>
        public virtual string GetName(string locale)
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

            return htmlViewPage;
        }

        /// <summary>
        /// Executes some initialization code.
        /// The Database property contains the Encryption key: if needed, encrypted data can be read from  or written to the SQLite database
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// Retuns if the plugin should be visible and accessible
        /// </summary>
        /// <param name="browser"></param>
        /// <returns></returns>
        public abstract bool IsVisible(Http.Browser browser);

        /// <summary>
        /// Executes some initialization code.
        /// The Database property does not contain the Encryption key: only unencrypted data can be read from or written to the SQLite database
        /// </summary>
        /// <returns>True if initialization was successfull. If false, the plugin will not be loaded</returns>
        public abstract bool Load();

        /// <summary>
        /// Opens a editor to edit some file data
        /// </summary>
        /// <param name="fileObject">File object to be modified</param>
        /// <param name="url">URL to editor</param>
        /// <param name="initJavascript">Javascript code to be executed after editor is started</param>
        /// <returns>True if the file object was successfully opened by the editor</returns>
        public virtual bool OpenFileEditor(Phabricator.Data.File fileObject, out string url, out string initJavascript)
        {
            url = null;
            initJavascript = null;
            return false;
        }

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
    }
}
