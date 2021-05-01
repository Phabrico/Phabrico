using System;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace Phabrico.Miscellaneous
{
    /// <summary>
    /// Upgrades the app.config file if needed.
    /// The MSI setup will not upgrade the app.config file (because it might contain custom parameters, e.g. directory where sqlite database file is stored)
    /// This class will load the content of the app.config file during the startup of the application and will verify if all the latest xml tags are
    /// in it. If not, the missing XML tags are added and the app.config file will be overwritten.
    /// </summary>
    public class AppConfigLoader
    {
        /// <summary>
        /// Full path to app.config file
        /// </summary>
        private readonly string configFileName = AppDomain.CurrentDomain.GetData("APP_CONFIG_FILE").ToString();

        /// <summary>
        /// Constructor
        /// </summary>
        public AppConfigLoader()
        {
            // read content of app.config file and upgrade if necessary
            UpdateContentConfigFile();

            // (re)load app.config file
            AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", configFileName);

            typeof(ConfigurationManager)
                            .GetField("s_initState", BindingFlags.NonPublic |
                                                     BindingFlags.Static)
                            .SetValue(null, 0);

            typeof(ConfigurationManager)
                .GetField("s_configSystem", BindingFlags.NonPublic |
                                            BindingFlags.Static)
                .SetValue(null, null);

            typeof(ConfigurationManager)
                .Assembly.GetTypes()
                .Where(x => x.FullName ==
                            "System.Configuration.ClientConfigPaths")
                .First()
                .GetField("s_current", BindingFlags.NonPublic |
                                       BindingFlags.Static)
                .SetValue(null, null);
        }

        /// <summary>
        /// Reads the content of the app.config file and upgrades it if necessary
        /// </summary>
        private void UpdateContentConfigFile()
        {
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.Load(configFileName);

            // == Version 2 ========================================================================================================================
            // check if runtime tag exists in app.config
            XmlNode xmlNodeRuntime = xmlDocument.SelectSingleNode("//configuration/runtime");
            if (xmlNodeRuntime == null)
            {
                //create new xml node
                string xmlRuntime = @"<runtime>
                                          <assemblyBinding xmlns='urn:schemas-microsoft-com:asm.v1'>
                                              <dependentAssembly>
                                                  <assemblyIdentity name='System.Runtime.InteropServices.RuntimeInformation' publicKeyToken='b03f5f7f11d50a3a' culture='neutral' />
                                                  <bindingRedirect oldVersion='0.0.0.0-4.0.1.0' newVersion='4.0.1.0' />
                                              </dependentAssembly>
                                          </assemblyBinding>
                                      </runtime>";
                XmlDocument xmlDocumentRuntime = new XmlDocument();
                xmlDocumentRuntime.LoadXml(xmlRuntime);
                xmlNodeRuntime = xmlDocumentRuntime.DocumentElement;

                // add new xml node to existing xml
                xmlNodeRuntime = xmlDocument.ImportNode(xmlNodeRuntime, true);
                xmlDocument.DocumentElement.AppendChild(xmlNodeRuntime);

                // save config file
                xmlDocument.Save(configFileName);
            }
        }
    }
}
