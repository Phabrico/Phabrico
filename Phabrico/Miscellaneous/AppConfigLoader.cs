using System;
using System.Collections.Specialized;
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
        /// Contains the appSettings from the phabrico.exe.config
        /// </summary>
        private static NameValueCollection appSettings = null;

        /// <summary>
        /// Full path to app.config file
        /// </summary>
        private static string configFileName = null;

        /// <summary>
        /// Full path to app.config file
        /// </summary>
        public static string ConfigFileName
        {
            get
            {
                if (configFileName == null)
                {
                    InitializeAppSettings();
                }

                return configFileName;
            }
        }

        /// <summary>
        /// Wrapper for ConfigurationManager.AppSettings
        /// </summary>
        public static NameValueCollection AppSettings
        {
            get
            {
                if (appSettings == null)
                {
                    InitializeAppSettings();
                }

                return appSettings;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public AppConfigLoader()
        {
            // read content of app.config file and upgrade if necessary
            UpdateContentConfigFile();

            // (re)load app.config file
            AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", ConfigFileName);

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

            // fix version issue with System.Runtime.InteropServices.RuntimeInformation.dll
            AppDomain.CurrentDomain.AssemblyResolve += delegate (object sender, ResolveEventArgs e)
            {
                AssemblyName requestedName = new AssemblyName(e.Name);

                if (requestedName.Name.Equals("System.Runtime.InteropServices.RuntimeInformation", StringComparison.OrdinalIgnoreCase))
                {
                    return Assembly.LoadFrom("System.Runtime.InteropServices.RuntimeInformation.dll");
                }
                else
                {
                    return null;
                }
            };
        }

        /// <summary>
        /// Initializes the application settings
        /// </summary>
        private static void InitializeAppSettings()
        {
            appSettings = ConfigurationManager.AppSettings;
            configFileName = AppDomain.CurrentDomain.GetData("APP_CONFIG_FILE").ToString();

            // in case we're running as IIS HTTP module, the appsettings is pointing to c:\inetpub\wwwroot\web.config
            // it may or may not contain the Phabrico configuration.
            // if not: search for the Phabrico.exe.config and change to directory in which the phabrico.exe.config file is located
            if (appSettings.Keys.Cast<string>().Contains("DatabaseDirectory") == false)
            {
                if (AppDomain.CurrentDomain.SetupInformation.ConfigurationFile.EndsWith("\\web.config", StringComparison.OrdinalIgnoreCase) == false)
                {
                    // we are not using a web.config -> use the config file we found initially
                    return;
                }

                string phabricoConfigFilePath = new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath + ".config";
                if (System.IO.File.Exists(phabricoConfigFilePath) == false)
                {
                    // phabrico.exe.config file not found -> use the config file we found initially
                    return;
                }

                // load appSettings from phabrico.exe.config file
                appSettings = new NameValueCollection();
                ExeConfigurationFileMap exeConfigurationFileMap = new ExeConfigurationFileMap { ExeConfigFilename = phabricoConfigFilePath };
                foreach (KeyValueConfigurationElement keyValueConfigurationElement in ConfigurationManager.OpenMappedExeConfiguration(exeConfigurationFileMap, ConfigurationUserLevel.None).AppSettings.Settings)
                {
                    appSettings.Add(keyValueConfigurationElement.Key, keyValueConfigurationElement.Value);
                }

                // verify path of DatabaseDirectory
                if (appSettings.Keys.Cast<string>().Contains("DatabaseDirectory"))
                {
                    string databaseDirectory = appSettings["DatabaseDirectory"];
                    if (databaseDirectory.StartsWith("\\") == false && (databaseDirectory.Length < 2 || databaseDirectory[1] != ':'))
                    {
                        // DatabaseDirectory contains a relative path -> change it to an absolute file path
                        string absoluteFilePathDatabaseDirectory = System.IO.Path.GetDirectoryName(phabricoConfigFilePath) + "\\" + databaseDirectory;

                        // overwrite "DatabaseDirectory" with absolute filepath
                        appSettings["DatabaseDirectory"] = absoluteFilePathDatabaseDirectory;

                        // set ConfigFileName
                        configFileName = phabricoConfigFilePath;

                        // make sure that the Phabrico directory is found in the local PATH
                        // This way dependent assemblies (from plugins for example) can still be loaded
                        string path = Environment.GetEnvironmentVariable("PATH");
                        string appDirectory = System.IO.Path.GetDirectoryName(phabricoConfigFilePath).TrimEnd('\\');
                        if (path.Split(';').All(pathDirs => pathDirs.Trim(' ', '\\').Equals(appDirectory, StringComparison.OrdinalIgnoreCase)) == false)
                        {
                            // appDirectory not found in PATH -> put it in PATH
                            Environment.SetEnvironmentVariable("PATH", appDirectory + ";" + path, EnvironmentVariableTarget.Process);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reads the content of the app.config file and upgrades it if necessary
        /// </summary>
        private void UpdateContentConfigFile()
        {
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.Load(ConfigFileName);

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
                xmlDocument.Save(ConfigFileName);
            }
        }
    }
}
