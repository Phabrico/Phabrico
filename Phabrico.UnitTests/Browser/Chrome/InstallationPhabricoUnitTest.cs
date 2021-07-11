using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Phabrico.UnitTests.Synchronization;
using System;
using WebDriverManager.DriverConfigs.Impl;

namespace Phabrico.UnitTests.Browser.Chrome
{
    [TestClass]
    public class InstallationPhabricoUnitTest : BrowserUnitTest
    {
        public InstallationPhabricoUnitTest() : base(new ChromeConfig())
        {
            WebBrowser = new ChromeDriver();
        }

        [TestMethod]
        public void TestNewInstallation()
        {
            DummyPhabricatorWebServer dummyPhabricatorWebServer = new DummyPhabricatorWebServer();

            try
            {
                Database.Dispose();
                HttpServer.Stop();

                System.IO.File.Delete(Storage.Database.DataSource);

                // recreate database and HTTP server settings
                Storage.Database.DataSource = ".\\TestNewDatabase";
                if (System.IO.File.Exists(Storage.Database.DataSource))
                {
                    System.IO.File.Delete(Storage.Database.DataSource);
                }
                Storage.Database._dbVersionInDataFile = 0;
                Database = new Storage.Database(EncryptionKey);
                Database.PrivateEncryptionKey = PrivateEncryptionKey;
                HttpServer = new Http.Server(false, 13468);
                HttpListenerContext = new Miscellaneous.HttpListenerContext();

                // browse to Phabrico url
                WebBrowser.Navigate().GoToUrl(HttpServer.Address);

                // verify if user sees HomePage.AuthenticationDialogCreateUser view
                IWebElement username = WebBrowser.FindElement(By.Name("username"));
                IWebElement password = WebBrowser.FindElement(By.Name("password"));
                IWebElement password2 = WebBrowser.FindElement(By.Name("password2"));
                IWebElement phabricatorUrl = WebBrowser.FindElement(By.Id("phabricatorUrl"));
                IWebElement conduitApiToken = WebBrowser.FindElement(By.Id("conduitApiToken"));
                IWebElement btnCreateUser = WebBrowser.FindElement(By.Id("btnCreateUser"));

                // complete input fields
                username.Clear();
                username.SendKeys("johnny");
                password.Clear();
                password.SendKeys(Password);
                password2.Clear();
                password2.SendKeys(Password);
                phabricatorUrl.Clear();
                phabricatorUrl.SendKeys("http://127.0.0.2:46975");
                conduitApiToken.Clear();
                conduitApiToken.SendKeys("api-pqi9w9qiumanoosy7e9oqb0apqiu");
                btnCreateUser.Click();

                // wait until configuration screen is shown, together with the firsttime help dialog
                WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(10));
                wait.Until(condition => condition.FindElements(By.ClassName("firsttime-help")).Count > 0);
                IWebElement firsttimeHelp = WebBrowser.FindElement(By.ClassName("firsttime-help"));
                Assert.IsTrue(firsttimeHelp.Displayed);  // is dialog visible ?

                // wait until synchronization process is finished
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(30));
                wait.Until(condition => condition.FindElement(By.Id("dlgSynchronizing")).Displayed);
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(30));
                wait.Until(condition => condition.FindElement(By.Id("dlgSynchronizing")).Displayed == false);
            }
            finally
            {
                dummyPhabricatorWebServer.Stop();

                Database.Dispose();
                HttpServer.Stop();
            }
        }
    }
}