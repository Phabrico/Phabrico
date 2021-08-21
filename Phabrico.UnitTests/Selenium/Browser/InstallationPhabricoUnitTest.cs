using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Phabrico.UnitTests.Synchronization;
using System;
using System.Linq;
using System.Threading;
using WebDriverManager.DriverConfigs.Impl;

namespace Phabrico.UnitTests.Selenium.Browser
{
    [TestClass]
    public class InstallationPhabricoUnitTest : BrowserUnitTest
    {
        [TestMethod]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        [DataRow(typeof(FirefoxConfig), "")]
        [DataRow(typeof(FirefoxConfig), "phabrico")]
        public void TestNewInstallation(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);

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
                HttpServer = new Http.Server(false, 13468, httpRootPath);
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
                WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElements(By.ClassName("firsttime-help")).Any());
                IWebElement firsttimeHelp = WebBrowser.FindElement(By.ClassName("firsttime-help"));
                Assert.IsTrue(firsttimeHelp.Displayed);  // is dialog visible ?

                // wait until synchronization process is finished
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(30));
                wait.Until(condition => condition.FindElements(By.Id("dlgSynchronizing")).Any(elem => elem.Displayed));
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(30));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.Id("dlgSynchronizing")).Any(elem => elem.Displayed == false));
            }
            finally
            {
                dummyPhabricatorWebServer.Stop();

                Database.Dispose();
                HttpServer.Stop();
            }
        }

        [TestMethod]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        [DataRow(typeof(FirefoxConfig), "")]
        [DataRow(typeof(FirefoxConfig), "phabrico")]
        public void TestNewSpanishInstallation(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);

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
                HttpServer = new Http.Server(false, 13468, httpRootPath);
                HttpListenerContext = new Miscellaneous.HttpListenerContext();

                // browse to Phabrico url
                WebBrowser.Navigate().GoToUrl(HttpServer.Address);


                // change language to Spanish
                IWebElement language = WebBrowser.FindElement(By.Id("newLanguage"));
                language.Click();
                language.FindElements(By.TagName("option"))
                                        .Single(option => option.Text == " Español")
                                        .Click();
                Thread.Sleep(500);  // wait a while to make sure the javascript code has been finished

                // verify new language selection (1)
                language = WebBrowser.FindElement(By.Id("newLanguage"));
                Assert.IsTrue( language.FindElements(By.TagName("option"))
                                       .Single(option => option.Text == " Español")
                                       .Selected
                             );

                // verify if user sees HomePage.AuthenticationDialogCreateUser view
                IWebElement username = WebBrowser.FindElement(By.Name("username"));
                IWebElement password = WebBrowser.FindElement(By.Name("password"));
                IWebElement password2 = WebBrowser.FindElement(By.Name("password2"));
                IWebElement phabricatorUrl = WebBrowser.FindElement(By.Id("phabricatorUrl"));
                IWebElement conduitApiToken = WebBrowser.FindElement(By.Id("conduitApiToken"));
                IWebElement btnCreateUser = WebBrowser.FindElement(By.Id("btnCreateUser"));

                // verify new language selection (2)
                WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => username.FindElement(By.XPath("./.."))
                                                .FindElement(By.XPath("./../label"))
                                                .Text
                                                .Equals("Nombre Usuario")
                          );

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
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElements(By.ClassName("firsttime-help")).Any());
                IWebElement firsttimeHelp = WebBrowser.FindElement(By.ClassName("firsttime-help"));
                Assert.IsTrue(firsttimeHelp.Displayed);  // is dialog visible ?

                // wait until synchronization process is finished
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(30));
                wait.Until(condition => condition.FindElements(By.Id("dlgSynchronizing")).Any(elem => elem.Displayed));
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(30));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.Id("dlgSynchronizing")).Any(elem => elem.Displayed == false));

                // verify new language selection (3)
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElement(By.ClassName("help-1"))
                                                 .FindElement(By.TagName("h2"))
                                                 .Text.Equals("Introducción a Phabrico")
                          );
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