using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;
using System;
using WebDriverManager;
using WebDriverManager.DriverConfigs;

namespace Phabrico.UnitTests.Browser
{
    [TestClass]
    public class BrowserUnitTest : PhabricoUnitTest, IDisposable
    {
        protected IDriverConfig DriverConfig { get; private set; }
        protected RemoteWebDriver WebBrowser { get; set; }

        /// <summary>
        /// Initializes a BrowserUnitTest
        /// </summary>
        /// <param name="driverConfig"></param>
        public BrowserUnitTest(IDriverConfig driverConfig)
        {
            DriverConfig = driverConfig;

            new DriverManager().SetUpDriver(driverConfig);
        }

        /// <summary>
        /// Disposes a BrowserUnitTest instance
        /// </summary>
        public override void Dispose()
        {
            WebBrowser.Quit();

            base.Dispose();
        }

        /// <summary>
        /// Completes the authentication dialog
        /// </summary>
        public void Logon()
        {
            WebBrowser.Navigate().GoToUrl(HttpServer.Address);

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
            wait.Until(condition => condition.FindElements(By.ClassName("phabrico-page-content")).Count > 0);
        }
    }
}
