using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using WebDriverManager;
using WebDriverManager.DriverConfigs;
using WebDriverManager.DriverConfigs.Impl;

namespace Phabrico.UnitTests.Selenium
{
    [TestClass]
    public class BrowserUnitTest : PhabricoUnitTest, IDisposable
    {
        protected IDriverConfig DriverConfig { get; private set; }
        protected WebDriver WebBrowser { get; set; }

        protected string DownloadDirectory
        {
            get
            {
                return System.IO.Path.GetTempPath() + "PhabricoUnitTests";
            }
        }

        public bool AreImagesSimilar(byte[] imageData1, byte[] imageData2, double tolerance = 0.01)
        {
            using (MemoryStream ms1 = new MemoryStream(imageData1))
            using (MemoryStream ms2 = new MemoryStream(imageData2))
            {
                using (Bitmap bmp1 = new Bitmap(ms1))
                using (Bitmap bmp2 = new Bitmap(ms2))
                {
                    if (bmp1.Width != bmp2.Width || bmp1.Height != bmp2.Height)
                        return false; // different dimensions => cannot be identical.

                    int totalPixels = bmp1.Width * bmp1.Height;
                    int differingPixels = 0;

                    for (int y = 0; y < bmp1.Height; y++)
                    {
                        for (int x = 0; x < bmp1.Width; x++)
                        {
                            Color pixel1 = bmp1.GetPixel(x, y);
                            Color pixel2 = bmp2.GetPixel(x, y);

                            if (!AreColorsSimilar(pixel1, pixel2))
                            {
                                differingPixels++;
                            }
                        }
                    }

                    double differenceRatio = (double)differingPixels / totalPixels;
                    return differenceRatio <= tolerance; // True if within tolerance, false if images differ too much.
                }
            }
        }

        public bool AreColorsSimilar(Color color1, Color color2, int tolerance = 10)
        {
            int rDiff = Math.Abs(color1.R - color2.R);
            int gDiff = Math.Abs(color1.G - color2.G);
            int bDiff = Math.Abs(color1.B - color2.B);

            return rDiff <= tolerance && gDiff <= tolerance && bDiff <= tolerance;
        }

        /// <summary>
        /// Verifies that there are no javascript errors generated in the browser
        /// </summary>
        public void AssertNoJavascriptErrors()
        {
            if (WebBrowser == null) return;

            var errorStrings = new List<string>
            {
                "SyntaxError",
                "EvalError",
                "ReferenceError",
                "RangeError",
                "TypeError",
                "URIError"
            };

            var jsErrors = WebBrowser.Manage()
                                     .Logs
                                     .GetLog(LogType.Browser)
                                     .Where(x => errorStrings.Any(e => x.Message.Contains(e)));

            if (jsErrors.Any())
            {
                Assert.Fail("JavaScript error(s):" + Environment.NewLine + jsErrors.Aggregate("", (s, entry) => s + entry.Message + Environment.NewLine));
            }
        }

        /// <summary>
        /// Executes some last tests before cleaning up the BrowserUnitTest
        /// </summary>
        [TestCleanup]
        public void CleanupTest()
        {
            AssertNoJavascriptErrors();
        }

        /// <summary>
        /// Disposes a BrowserUnitTest instance
        /// </summary>
        public override void Dispose()
        {
            if (WebBrowser != null)
            {
                WebBrowser.Quit();
            }

            if (System.IO.Directory.Exists(DownloadDirectory))
            {
                System.IO.DirectoryInfo directoryInfo = new System.IO.DirectoryInfo(DownloadDirectory);
                foreach (System.IO.FileInfo file in directoryInfo.EnumerateFiles("*.*", System.IO.SearchOption.AllDirectories).ToArray())
                {
                    file.Attributes = System.IO.FileAttributes.Archive;
                    file.Delete();
                }

                System.IO.Directory.Delete(DownloadDirectory, true);
            }

            base.Dispose();
        }

        /// <summary>
        /// Initializes a BrowserUnitTest
        /// </summary>
        public virtual void Initialize(Type browser, string httpRootPath)
        {
            Http.Server.UnitTesting = true;
            string singleBrowserTest = Environment.GetEnvironmentVariable("PHABRICO.TEST.BROWSER", EnvironmentVariableTarget.Machine);

            // (re)create directory where files can downloaded into
            if (System.IO.Directory.Exists(DownloadDirectory))
            {
                Plugin.DirectoryMonitor.Stop();

                System.IO.DirectoryInfo directoryInfo = new System.IO.DirectoryInfo(DownloadDirectory);
                foreach (System.IO.FileInfo file in directoryInfo.EnumerateFiles("*.*", System.IO.SearchOption.AllDirectories).ToArray())
                {
                    file.Attributes = System.IO.FileAttributes.Archive;
                    file.Delete();
                }

                System.IO.Directory.Delete(DownloadDirectory, true);
            }
            System.IO.Directory.CreateDirectory(DownloadDirectory);

            // start configuring browser engines
            if (browser == typeof(ChromeConfig))
            {
                if (string.IsNullOrWhiteSpace(singleBrowserTest) == false && singleBrowserTest.Equals("chrome", StringComparison.OrdinalIgnoreCase) == false)
                {
                    // break off test: we only want to test 1 browser and it's not Chrome
                    Assert.Inconclusive();
                }

                base.Initialize(httpRootPath);

                DriverConfig = new ChromeConfig();
                new DriverManager().SetUpDriver(DriverConfig);

                ChromeOptions options = new ChromeOptions();
                options.AddUserProfilePreference("download.default_directory", DownloadDirectory);
                options.AddUserProfilePreference("download.prompt_for_download", false);
                options.AddUserProfilePreference("download.directory_upgrade", true);
                options.AddUserProfilePreference("safebrowsing.enabled", true);
                options.AddUserProfilePreference("disable-popup-blocking", "true");

                options.AddArgument("--allow-running-insecure-content");
                options.AddArgument("--unsafely-treat-insecure-origin-as-secure=" + HttpServer.Address);

                WebBrowser = new OpenQA.Selenium.Chrome.ChromeDriver(options);
                return;
            }

            if (browser == typeof(EdgeConfig))
            {
                if (string.IsNullOrWhiteSpace(singleBrowserTest) == false && singleBrowserTest.Equals("edge", StringComparison.OrdinalIgnoreCase) == false)
                {
                    // break off test: we only want to test 1 browser and it's not Edge
                    Assert.Inconclusive();
                }

                base.Initialize(httpRootPath);

                DriverConfig = new EdgeConfig();
                new DriverManager().SetUpDriver(DriverConfig);

                EdgeOptions options = new EdgeOptions();
                options.AddUserProfilePreference("download.default_directory", DownloadDirectory);

                WebBrowser = new OpenQA.Selenium.Edge.EdgeDriver(options);
                return;
            }

            if (browser == typeof(FirefoxConfig))
            {
                if (string.IsNullOrWhiteSpace(singleBrowserTest) == false && singleBrowserTest.Equals("firefox", StringComparison.OrdinalIgnoreCase) == false)
                {
                    // break off test: we only want to test 1 browser and it's not Firefox
                    Assert.Inconclusive();
                }

                base.Initialize(httpRootPath);

                DriverConfig = new FirefoxConfig();
                new DriverManager().SetUpDriver(DriverConfig);

                FirefoxProfile firefoxProfile = new FirefoxProfile();
                firefoxProfile.SetPreference("pdfjs.disabled", true);
                firefoxProfile.SetPreference("browser.download.folderList", 2);
                firefoxProfile.SetPreference("browser.download.dir", DownloadDirectory);
                firefoxProfile.SetPreference("browser.download.downloadDir", DownloadDirectory);
                firefoxProfile.SetPreference("browser.download.defaultFolder", DownloadDirectory);
                firefoxProfile.SetPreference("plugin.disable_full_page_plugin_for_types", "application/pdf, application/force-download");
                firefoxProfile.SetPreference("browser.helperApps.neverAsk.saveToDisk", "application/pdf, application/force-download");
                firefoxProfile.SetPreference("browser.helperApps.neverAsk.openFile", "application/pdf, application/force-download");

                WebBrowser = new OpenQA.Selenium.Firefox.FirefoxDriver(new FirefoxOptions
                {
                    Profile = firefoxProfile
                });
                return;
            }

            throw new System.Exception("Invalid browser type");
        }

        /// <summary>
        /// Completes the authentication dialog
        /// </summary>
        public void Logon(bool navigateToAuthenticationScreen = true)
        {
            if (navigateToAuthenticationScreen)
            {
                WebBrowser.Navigate().GoToUrl(HttpServer.Address);
            }

            IWebElement username = WebBrowser.FindElement(By.Name("username"));
            IWebElement password = WebBrowser.FindElement(By.Name("password"));
            IWebElement btnLogIn = WebBrowser.FindElement(By.Id("btnLogIn"));

            // log on with invalid credentials
            username.Clear();
            username.SendKeys("johnny");
            password.Clear();
            password.SendKeys(Password);
            btnLogIn.Click();

            // wait a while to make sure the logon has been processed
            WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phabrico-page-content"))
                                             .Any(elem => elem.Enabled && elem.Displayed)
                      );

            // wait until some init-javascript functions are finished (e.g. for enabling the Synchronize button)
            Thread.Sleep(1000);

            AssertNoJavascriptErrors();
        }
    }
}