using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using WebDriverManager.DriverConfigs.Impl;

namespace Phabrico.UnitTests.Browser.Chrome
{
    [TestClass]
    public class AuthenticationUnitTests : BrowserUnitTest
    {
        public AuthenticationUnitTests() : base(new ChromeConfig())
        {
            WebBrowser = new ChromeDriver();
        }
        
        [TestMethod]
        public void TestInvalidCredentials()
        {
            WebBrowser.Navigate().GoToUrl(HttpServer.Address);

            IWebElement username = WebBrowser.FindElement(By.Name("username"));
            IWebElement password = WebBrowser.FindElement(By.Name("password"));
            IWebElement btnLogIn = WebBrowser.FindElement(By.Id("btnLogIn"));

            // log on with invalid credentials
            username.Clear();
            username.SendKeys("johnny");
            password.Clear();
            password.SendKeys("Invalid-Password-1234567890");
            btnLogIn.Click();

            // wait a while to make sure the logon has been processed
            IWebElement errorMessage;
            WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition =>
            {
                errorMessage = condition.FindElement(By.ClassName("phui-info-severity-error"));
                if (errorMessage == null) return false;
                return errorMessage.Displayed;
            });

            // validate invalid logon
            errorMessage = WebBrowser.FindElement(By.ClassName("phui-info-severity-error"));
            Assert.IsTrue(errorMessage.Displayed);
            Assert.AreEqual(errorMessage.Text, "Username or password are incorrect.");
        }
        
        [TestMethod]
        public void TestValidCredentials()
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
            wait.Until(condition => condition.FindElement(By.ClassName("phabrico-page-content")));

            // validate invalid logon
            IWebElement generalOverview = WebBrowser.FindElement(By.ClassName("phabrico-page-content"));
            string generalOverviewTitle = generalOverview.Text.Split('\r', '\n')[0];
            Assert.IsTrue(generalOverviewTitle.Equals("General Overview"));
        }
    }
}