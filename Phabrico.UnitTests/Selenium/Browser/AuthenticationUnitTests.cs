using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;
using System.Linq;
using System.Threading;
using WebDriverManager.DriverConfigs.Impl;

namespace Phabrico.UnitTests.Selenium.Browser
{
    [TestClass]
    public class AuthenticationUnitTests : BrowserUnitTest
    {
        [TestMethod]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        // [DataRow(typeof(FirefoxConfig), "")]           // disabled: get ElementNotInteractableException at mnuChangeLanguage.Click() ?!?
        // [DataRow(typeof(FirefoxConfig), "phabrico")]   // disabled: get ElementNotInteractableException at mnuChangeLanguage.Click() ?!?
        public void LogOnAndSetLanguageToSpanish(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);

            Logon();

            // open user menu
            IWebElement userMenu = WebBrowser.FindElement(By.ClassName("user-menu"));
            userMenu.Click();

            // click 'Change language'
            IWebElement mnuChangeLanguage = userMenu.FindElement(By.PartialLinkText("Change language"));
            mnuChangeLanguage.Click();

            // change language to Spanish
            IWebElement language = WebBrowser.FindElement(By.Id("newLanguage"));
            language.Click();
            language.FindElements(By.TagName("option"))
                    .Single(option => option.Text == " Español")
                    .Click();
            language.Click();

            // verify new language selection
            language = WebBrowser.FindElement(By.Id("newLanguage"));
            Assert.IsTrue( language.FindElements(By.TagName("option"))
                                   .Single(option => option.Text == " Español")
                                   .Selected
                         );
            language.SendKeys(Keys.Enter);

            // click 'Change language'
            IWebElement btnChangeLanguage = WebBrowser.FindElement(By.XPath("//button[contains(text(), 'Change language')]"));
            btnChangeLanguage.Click();
            Thread.Sleep(500);  // wait a while to make sure the redirect call is finished

            // verify new language change
            IWebElement search = WebBrowser.FindElement(By.Id("searchPhabrico"));
            Assert.AreEqual(search.GetAttribute("placeholder"), "Buscar");
        }

        [TestMethod]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        [DataRow(typeof(FirefoxConfig), "")]
        [DataRow(typeof(FirefoxConfig), "phabrico")]
        public void TestInvalidCredentials(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);

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
            wait.Until(condition => condition.FindElements(By.ClassName("phui-info-severity-error")).Any(message => message.Displayed));

            // validate invalid logon
            errorMessage = WebBrowser.FindElement(By.ClassName("phui-info-severity-error"));
            Assert.IsTrue(errorMessage.Displayed);
            Assert.AreEqual(errorMessage.Text, "Username or password are incorrect.");
        }
        
        [TestMethod]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        [DataRow(typeof(FirefoxConfig), "")]
        [DataRow(typeof(FirefoxConfig), "phabrico")]
        public void TestValidCredentials(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);

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
            wait.Until(condition => condition.FindElements(By.ClassName("phabrico-page-content")).Any());

            // validate invalid logon
            IWebElement generalOverview = WebBrowser.FindElements(By.ClassName("phabrico-page-content")).FirstOrDefault(elem => elem.Displayed);
            string generalOverviewTitle = generalOverview.Text.Split('\r', '\n')[0];
            Assert.IsTrue(generalOverviewTitle.Equals("General Overview"));
        }
        
        [TestMethod]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        [DataRow(typeof(FirefoxConfig), "")]
        [DataRow(typeof(FirefoxConfig), "phabrico")]
        public void TestValidCredentialsWithRedirect(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);

            WebBrowser.Navigate().GoToUrl(HttpServer.Address + "w");

            Logon(false);

            // validate if Phriction was opened
            IWebElement phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            string phrictionDocumentTitle = phrictionDocument.Text.Split('\r', '\n')[0];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Story of my life"), "Unable to open Phriction");

            // validate if we don't have double slashes in the URL
            Assert.IsFalse(WebBrowser.Url.Substring("http://".Length).Contains("//"));
        }

        [TestMethod]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        // [DataRow(typeof(FirefoxConfig), "")]          // disabled: get ElementNotInteractableException at logOff.Click ?!?
        // [DataRow(typeof(FirefoxConfig), "phabrico")]  // disabled: get ElementNotInteractableException at logOff.Click ?!?
        public void TestLogOnAndLogOff(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);

            Logon();

            // open user menu
            IWebElement userMenu = WebBrowser.FindElement(By.ClassName("user-menu"));
            userMenu.Click();

            // click 'Log off'
            IWebElement logOff = userMenu.FindElement(By.PartialLinkText("Log out"));
            logOff.Click();
            Thread.Sleep(5);  // wait a while to make sure the redirect call is finished
            
            // wait until logon dialog is shown again
            WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
            wait.Until(condition => condition.FindElements(By.ClassName("aphront-dialog-head")).Any(title => title.Text.Equals("Log In")));
        }
        
        [TestMethod]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        // [DataRow(typeof(FirefoxConfig), "")]           // disabled: get ElementNotInteractableException at mnuChangePassword.Click() ?!?
        // [DataRow(typeof(FirefoxConfig), "phabrico")]   // disabled: get ElementNotInteractableException at mnuChangePassword.Click() ?!?
        public void TestModificationPassword(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);

            Logon();

            // open user menu
            IWebElement userMenu = WebBrowser.FindElement(By.ClassName("user-menu"));
            userMenu.Click();

            // click 'Change password'
            IWebElement mnuChangePassword = userMenu.FindElement(By.PartialLinkText("Change password"));
            mnuChangePassword.Click();

            // enter old password
            IWebElement oldPassword = WebBrowser.FindElement(By.Id("oldPassword"));
            oldPassword.SendKeys(Password);

            // enter new password
            string newPassword = "In-München-steht-1-Hofbrauhaus";
            IWebElement newPassword1 = WebBrowser.FindElement(By.Id("newPassword1"));
            newPassword1.SendKeys(newPassword);

            // enter new password again
            IWebElement newPassword2 = WebBrowser.FindElement(By.Id("newPassword2"));
            newPassword2.SendKeys(newPassword);

            // click 'Change password'
            IWebElement btnChangePassword = WebBrowser.FindElement(By.XPath("//button[contains(text(), 'Change password')]"));
            btnChangePassword.Click();
            Thread.Sleep(500);  // wait a while to make sure the redirect call is finished
            
            // wait until logon dialog is shown again
            WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
            wait.Until(condition => condition.FindElements(By.ClassName("aphront-dialog-head")).Any(title => title.Text.Equals("Log In")));

            // log on with new password
            IWebElement username = WebBrowser.FindElement(By.Name("username"));
            IWebElement password = WebBrowser.FindElement(By.Name("password"));
            IWebElement btnLogIn = WebBrowser.FindElement(By.Id("btnLogIn"));

            // log on with invalid credentials
            username.Clear();
            username.SendKeys("johnny");
            password.Clear();
            password.SendKeys(newPassword);
            btnLogIn.Click();

            // wait a while to make sure the logon has been processed
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phabrico-page-content"))
                                             .Any(elem => elem.Enabled && elem.Displayed)
                      );
        }
   }
}