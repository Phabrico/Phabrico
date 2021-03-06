# Customization

Phabrico can be installed as a customizable HTTP module in IIS.

## Quick guide
Basically you need to implement the following things:
- a class that inherits from `IHttpModule`
- a static reference to `Phabrico.Http.Server`
- a static constructor in which you initialize and customize Phabrico by means of the `Customization` object
- an `Init` and `Dispose` method (inheriting from `IHttpModule`)
  - the `Init` class method should execute the `Init` method from the `Phabrico.Http.Server`
  
The parameters for the `Phabrico.Http.Server` constuctor should be:

| #  | Parameter                  | Value                                       |
|----|----------------------------|---------------------------------------------|
| 1  | `bool remoteAccessEnabled` | true                                        |
| 2  | `int listenTcpPortNr`      | -1                                          |
| 3  | `string rootPath`          | (the baseURI you want to use in your URL)   |
| 4  | `bool isHttpModule`        | true                                        |

An exampe in C#:

``` cs
using System.Drawing;
using System.Web;

namespace Phabrico
{
    public class IISModule : IHttpModule
    {
        static Phabrico.Http.Server httpServer;

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
            httpServer.Customization.Language = "en";

            httpServer.Customization.HideConfig = true;
            httpServer.Customization.HideFiles = true;
            httpServer.Customization.HideManiphest = true;
            httpServer.Customization.HideNavigatorTooltips = false;
            httpServer.Customization.HideOfflineChanges = true;
            httpServer.Customization.HidePhriction = false;
            httpServer.Customization.HidePhrictionActionMenu = true;
            httpServer.Customization.HidePhrictionFavorites = true;
            httpServer.Customization.HideProjects = true;
            httpServer.Customization.HideSearch = false;
            httpServer.Customization.HideUsers = true;
            httpServer.Customization.IsReadonly = true;
        }

        public void Init(HttpApplication application)
        {
            httpServer.Init(application);
        }

        public void Dispose()
        {
        }
    }
}
```

![Customization-01](Customization-01.png) <br />


## Customization parameters

| Parameter                                   | Description                                                                                                                                                             | Default
| ------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------
| ApplicationCSS                              | Global cascading style sheets which are injected in each page                                                                                                           | 
| ApplicationLogo                             | The logo that should be shown in the top left corner                                                                                                                    | Phabrico logo
| ApplicationLogoStyle                        | CSS styles for formatting the ApplicationLogo                                                                                                                           | 
| ApplicationName                             | The name of the application that should be shown in the top left corner                                                                                                 | Phabrico
| ApplicationNameStyle                        | CSS styles for formatting the ApplicationName                                                                                                                           | 
| AuthenticationFactor                        | If Knowledge, one should authenticate with username and password to access Phabrico; If Public, no authentication is needed                                             | Knowledge
| FavIcon                                     | If set to a PNG image, the PNG image will be used as icon in the browser tab; if not set, the default Phabrico icon will be used                                        | Phabrico icon
| HideConfig                                  | If true, Config screen will not be accessible                                                                                                                           | false
| HideFiles                                   | If true, Files screen will not be accessible                                                                                                                            | false
| HideOfflineChanges                          | If true, Offline Changes screen will not be accessible                                                                                                                  | false
| HideManiphest                               | If true, Maniphest tasks will not be accessible                                                                                                                         | false
| HideNavigatorTooltips                       | If true, the tooltips for the menu items in the homepage will not be shown                                                                                              | false
| HidePhriction                               | If true, Phriction/wiki documents will not be accessible                                                                                                                | false
| HidePhrictionActionMenu                     | If true, the menu on the right side of the Phriction documents is no longer visible                                                                                     | false
| HidePhrictionChanges                        | If true, the changes made in Phriction/wiki documents can not be seen or undone                                                                                         | false
| HidePhrictionFavorites                      | If true, Phriction/wiki documents can not be marked as favorite                                                                                                         | false
| HideProjects                                | If true, Phabricator projects will not be accessible                                                                                                                    | false
| HideUsers                                   | If true, Phabricator users will not be accessible                                                                                                                       | false
| HideSearch                                  | If true, Search field will not be accessible                                                                                                                            | false
| IsReadonly                                  | If true, no Phriction document or Maniphest task can be edited                                                                                                          | false
| Language                                    | Language code for Phabrico application.  (Content of Phriction documents or Maniphest tasks will not be translated). If set, the language cannot be changed by the user | Language of browser or English
| MasterDataIsAccessible                      | If false, the master data on Phabricator is not accessible via Phabrico. If IsReadonly is true, MasterDataIsAccessible wil be false                                     | true
| Theme                                       | Auto, Light or Dark; If Auto, the user can change the theme in the Config screen (if accessible)                                                                        | Auto

## IIS Configuration
* WebSocket protocol should be installed in IIS:

![Customization-02](Customization-02.png) <br />

* 2 extra lines should be added to the C:\inetpub\wwwroot\web.config:
  * `probing/privatePath` points to the subdirectory in the C:\inetpub\wwwroot directory where the Phabrico IIS module is installed.
  * `modules/add` should contain a reference to your Phabrico IIS module
* a 3rd line is optional:
  * if `httpErrors/existingResponse` is set to `PassThrough`, you will see Phabrico-styled error pages instead of the IIS ones

![Customization-03](Customization-03.png) <br />

You can make use of a symbolic link in case you want your Phabrico IIS module not installed in the C:\inetpub\wwwroot directory.
If you execute the following statements in a command prompt window, you can keep your Phabrico IIS module in `C:\Program Files\PhabricoIIS`.
For IIS they are accessible via `MyPhabrico`

```
c:
cd c:\inetpub\wwwroot
mklink /D MyPhabrico "C:\Program Files\PhabricoIIS"
```

