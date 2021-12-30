using Phabrico.Http;
using Phabrico.Http.Response;

namespace Phabrico.Plugin
{
    abstract public class PluginBase : PluginWithoutConfigurationBase
    {
        /// <summary>
        /// With a plugin extension, you can add an extra URL action to your plugin
        /// A plugin extension, however, can not have a separate configuration screen
        /// </summary>
        public virtual PluginWithoutConfigurationBase[] Extensions { get; } = new PluginWithoutConfigurationBase[0];

        /// <summary>
        /// Returns the HtmlViewPage which is shown in the configuration screen
        /// If NULL is returned, no plugin-specific configuration screen is shown.
        /// </summary>
        /// <param name="browser"></param>
        /// <returns></returns>
        public HtmlViewPage GetConfigurationViewPage(Browser browser)
        {
            return GetViewPage(browser, "Configuration");
        }
    }
}
