using System.Drawing;
using System.Web;

namespace Phabrico
{
    public class IISModule : IHttpModule
    {
        static readonly Phabrico.Http.Server httpServer;

        /// <summary>
        /// Static constructor
        /// </summary>
        static IISModule()
        {
            httpServer = new Phabrico.Http.Server(true, -1, "holiday", true);

            httpServer.Customization.ApplicationName = "The Holiday Company";
            httpServer.Customization.ApplicationNameStyle["color"] = "white";
            httpServer.Customization.ApplicationNameStyle["font-weight"] = "bold";
            httpServer.Customization.ApplicationNameStyle["font-size"] = "17px";
            httpServer.Customization.ApplicationNameStyle["font-family"] = "lato,sans-serif";

            httpServer.Customization.ApplicationLogo = new Bitmap(typeof(IISModule).Assembly.GetManifestResourceStream("Phabrico.Images.logo.png"));

            httpServer.Customization.Theme = ApplicationCustomization.ApplicationTheme.Dark;
            httpServer.Customization.AvailableLanguages = new Phabrico.Miscellaneous.Language[] { "en" };

            httpServer.Customization.HideConfig = true;
            httpServer.Customization.HideFiles = true;
            httpServer.Customization.HideManiphest = true;
            httpServer.Customization.HideNavigatorTooltips = false;
            httpServer.Customization.HideOfflineChanges = true;
            httpServer.Customization.HidePhriction = false;
            httpServer.Customization.HidePhrictionActionMenu = false;
            httpServer.Customization.HidePhrictionFavorites = true;
            httpServer.Customization.HideProjects = true;
            httpServer.Customization.HideSearch = false;
            httpServer.Customization.HideUsers = true;
            httpServer.Customization.IsReadonly = true;
        }

        /// <summary>
        /// Initializes the IISModule
        /// </summary>
        /// <param name="application"></param>
        public void Init(HttpApplication application)
        {
            httpServer.Init(application);
        }

        /// <summary>
        /// Disposes the IISModule
        /// </summary>
        public void Dispose()
        {
        }
    }
}
