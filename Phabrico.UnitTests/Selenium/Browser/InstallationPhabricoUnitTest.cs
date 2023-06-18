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
        public void TestEnterInvalidCreateNewUserDataDuringNewInstallation(Type browser, string httpRootPath)
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
                IWebElement usernameError = WebBrowser.FindElement(By.Id("invalidUserNameReason"));
                IWebElement passwordError = WebBrowser.FindElement(By.Id("invalidPasswordReason"));
                IWebElement password2Error = WebBrowser.FindElement(By.Id("passwordVerificationReason"));
                IWebElement urlError = WebBrowser.FindElement(By.Id("invalidPhabricatorUrlReason"));
                IWebElement apiError = WebBrowser.FindElement(By.Id("invalidConduitApiTokenReason"));

                // test 1: enter all input fields but no username
                ClearInputFields(username, password, password2, phabricatorUrl, conduitApiToken);
                password.SendKeys(Password);
                password2.SendKeys(Password);
                phabricatorUrl.SendKeys("http://127.0.0.2:46975");
                conduitApiToken.SendKeys("api-pqi9w9qiumanoosy7e9oqb0apqiu");
                Assert.AreEqual("User name should be at least 1 character long", usernameError.Text);

                // test 2: enter different passwords
                ClearInputFields(username, password, password2, phabricatorUrl, conduitApiToken);
                username.SendKeys("johnny");
                password.SendKeys(Password);
                password2.SendKeys(Password + "_");
                phabricatorUrl.SendKeys("http://127.0.0.2:46975");
                conduitApiToken.SendKeys("api-pqi9w9qiumanoosy7e9oqb0apqiu");
                Assert.AreEqual("The passwords entered do not match", password2Error.Text);

                // test 3: enter short passwords
                ClearInputFields(username, password, password2, phabricatorUrl, conduitApiToken);
                username.SendKeys("johnny");
                password.SendKeys("abc");
                password2.SendKeys("abc");
                phabricatorUrl.SendKeys("http://127.0.0.2:46975");
                conduitApiToken.SendKeys("api-pqi9w9qiumanoosy7e9oqb0apqiu");
                Assert.AreEqual("Password should be at least 12 characters long", passwordError.Text);
                
                // test 4: enter lowercased passwords
                ClearInputFields(username, password, password2, phabricatorUrl, conduitApiToken);
                username.SendKeys("johnny");
                password.SendKeys("abcdefghijkl");
                password2.SendKeys("abcdefghijkl");
                phabricatorUrl.SendKeys("http://127.0.0.2:46975");
                conduitApiToken.SendKeys("api-pqi9w9qiumanoosy7e9oqb0apqiu");
                Assert.AreEqual("Password should contain at least 1 capital letter", passwordError.Text);
                
                // test 5: enter lowercased passwords
                ClearInputFields(username, password, password2, phabricatorUrl, conduitApiToken);
                username.SendKeys("johnny");
                password.SendKeys("ABCDEFGHIJKL");
                password2.SendKeys("ABCDEFGHIJKL");
                phabricatorUrl.SendKeys("http://127.0.0.2:46975");
                conduitApiToken.SendKeys("api-pqi9w9qiumanoosy7e9oqb0apqiu");
                Assert.AreEqual("Password should contain at least 1 lowercase letter", passwordError.Text);
            
                // test 6: enter mixed lower/uppercase passwords
                ClearInputFields(username, password, password2, phabricatorUrl, conduitApiToken);
                username.SendKeys("johnny");
                password.SendKeys("abcdEFGHijkl");
                password2.SendKeys("abcdEFGHijkl");
                phabricatorUrl.SendKeys("http://127.0.0.2:46975");
                conduitApiToken.SendKeys("api-pqi9w9qiumanoosy7e9oqb0apqiu");
                Assert.AreEqual("Password should contain at least 1 number", passwordError.Text);
            
                // test 7: enter mixed lower/uppercase passwords with numbers
                ClearInputFields(username, password, password2, phabricatorUrl, conduitApiToken);
                username.SendKeys("johnny");
                password.SendKeys("abcdEFGH1234");
                password2.SendKeys("abcdEFGH1234");
                phabricatorUrl.SendKeys("http://127.0.0.2:46975");
                conduitApiToken.SendKeys("api-pqi9w9qiumanoosy7e9oqb0apqiu");
                Assert.AreEqual("Password should contain at least 1 punctuation character", passwordError.Text);
            
                // test 8: enter mixed lower/uppercase passwords with numbers
                ClearInputFields(username, password, password2, phabricatorUrl, conduitApiToken);
                username.SendKeys("johnny");
                password.SendKeys("abcdEFGH12?!");
                password2.SendKeys("abcdEFGH12?!");
                phabricatorUrl.SendKeys("http://127.0.0.2:46975");
                conduitApiToken.SendKeys("api-pqi9w9qiumanoosy7e9oqb0apqiu");
                Assert.AreEqual("", passwordError.Text);

                // test 9: enter invalid Phabricator URL
                ClearInputFields(username, password, password2, phabricatorUrl, conduitApiToken);
                username.SendKeys("johnny");
                password.SendKeys(Password);
                password2.SendKeys(Password);
                phabricatorUrl.SendKeys("ftp://127.0.0.2:46975");
                conduitApiToken.SendKeys("api-pqi9w9qiumanoosy7e9oqb0apqiu");
                Assert.AreEqual("Only HTTP or HTTPS urls are supported", urlError.Text);

                // test 10: enter invalid Conduit API token (1)
                ClearInputFields(username, password, password2, phabricatorUrl, conduitApiToken);
                username.SendKeys("johnny");
                password.SendKeys(Password);
                password2.SendKeys(Password);
                phabricatorUrl.SendKeys("http://127.0.0.2:46975");
                conduitApiToken.SendKeys("pqi9w9qiumanoosy7e9oqb0apqiu");
                Assert.AreEqual("Invalid API token", apiError.Text);

                // test 11: enter invalid Conduit API token (2)
                ClearInputFields(username, password, password2, phabricatorUrl, conduitApiToken);
                username.SendKeys("johnny");
                password.SendKeys(Password);
                password2.SendKeys(Password);
                phabricatorUrl.SendKeys("http://127.0.0.2:46975");
                conduitApiToken.SendKeys("APY-pqi9w9qiumanoosy7e9oqb0apqiu");
                Assert.AreEqual("Invalid API token", apiError.Text);

                // test 12: enter invalid Conduit API token (2)
                ClearInputFields(username, password, password2, phabricatorUrl, conduitApiToken);
                username.SendKeys("johnny");
                password.SendKeys(Password);
                password2.SendKeys(Password);
                phabricatorUrl.SendKeys("http://127.0.0.2:46975");
                conduitApiToken.SendKeys("pqi9w9qiumanoosy7e9oqb0apqiuAPI-");
                Assert.AreEqual("Invalid API token", apiError.Text);
            }
            finally
            {
                dummyPhabricatorWebServer.Stop();

                Database.Dispose();
                HttpServer.Stop();
            }
        }

        private void ClearInputFields(IWebElement username, IWebElement password, IWebElement password2, IWebElement phabricatorUrl, IWebElement conduitApiToken)
        {
            username.Clear();
            password.Clear();
            password2.Clear();
            phabricatorUrl.Clear();
            conduitApiToken.Clear();
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