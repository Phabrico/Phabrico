using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Phabrico.Miscellaneous;
using Phabrico.UnitTests.Synchronization;
using System;
using System.Linq;
using System.Threading;
using WebDriverManager.DriverConfigs.Impl;

namespace Phabrico.UnitTests.Selenium.Browser
{
    [TestClass]
    public class SynchronizationUnitTests : BrowserUnitTest
    {
        [TestMethod]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        [DataRow(typeof(FirefoxConfig), "")]
        [DataRow(typeof(FirefoxConfig), "phabrico")]
        public void SynchronizeAndShowLatestChanges(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);

            DummyPhabricatorWebServer dummyPhabricatorWebServer = new DummyPhabricatorWebServer();

            try
            {
                Logon();

                // ## Step 1: Light synchronization #################################################################################

                // Click 'Synchronize'
                IWebElement btnSynchronize = WebBrowser.FindElement(By.XPath("//button[contains(text(), 'Synchronize')]"));
                btnSynchronize.Click();

                // wait until confirmation dialog is shown
                WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElements(By.ClassName("modal")).Any(message => message.Displayed));

                // confirm synchronization
                IWebElement dlgConfirmSynchronizing = WebBrowser.FindElements(By.ClassName("aphront-dialog-view"))
                                                                .Where(dialog => dialog.Displayed
                                                                              && dialog.GetAttribute("class").Split(' ').Contains("modal")
                                                                      )
                                                                .FirstOrDefault();
                IWebElement btnYes = dlgConfirmSynchronizing.FindElement(By.XPath("//button[contains(text(), 'Yes')]"));
                btnYes.Click();

                // wait until synchronization process is finished
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(30));
                wait.Until(condition => condition.FindElements(By.Id("dlgSynchronizing")).Any(elem => elem.Displayed));
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(30));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.Id("dlgSynchronizing")).Any(elem => elem.Displayed == false));


                // ## Step 2: Prepare for new synchronization #######################################################################

                // modify Maniphest task directly in database
                Storage.Maniphest maniphestStorage = new Storage.Maniphest();
                Phabricator.Data.Maniphest maniphestTask = maniphestStorage.Get(Database, Language.NotApplicable).FirstOrDefault(task => task.Name.Contains("Make coffee"));
                maniphestTask.Description += "\n# Go to bed";
                maniphestStorage.Add(Database, maniphestTask);

                // overwrite latest-sync-timestamps
                Storage.Project projectStorage = new Storage.Project();
                foreach (Phabricator.Data.Project project in projectStorage.Get(Database, Language.NotApplicable).ToArray())
                {
                    project.DateSynchronized = DateTimeOffset.FromUnixTimeSeconds(1578831320 - 1);  // timestamp take from maniphest.search.json
                    projectStorage.Add(Database, project);
                }

                Storage.User userStorage = new Storage.User();
                foreach (Phabricator.Data.User user in userStorage.Get(Database, Language.NotApplicable).ToArray())
                {
                    user.DateSynchronized = DateTimeOffset.FromUnixTimeSeconds(1578831320 - 1);  // timestamp take from maniphest.search.json
                    userStorage.Add(Database, user);
                }

                // wait a while (otherwise the btnSynchronize.Click event is sometimes missed by the WebDriver)
                Thread.Sleep(500);



                // ## Step 3: Full synchronization ##################################################################################

                // Click 'Synchronize'
                btnSynchronize = WebBrowser.FindElement(By.XPath("//button[contains(text(), 'Synchronize')]"));
                btnSynchronize.Click();

                // wait until confirmation dialog is shown
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElements(By.ClassName("modal")).Any(message => message.Displayed));

                // confirm synchronization
                dlgConfirmSynchronizing = WebBrowser.FindElements(By.ClassName("aphront-dialog-view"))
                                                    .Where(dialog => dialog.Displayed
                                                                  && dialog.GetAttribute("class").Split(' ').Contains("modal")
                                                          )
                                                    .FirstOrDefault();
                btnYes = dlgConfirmSynchronizing.FindElement(By.XPath("//button[contains(text(), 'Yes')]"));
                btnYes.Click();

                // wait until synchronization process is finished
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(30));
                wait.Until(condition => condition.FindElements(By.Id("dlgSynchronizing")).Any(elem => elem.Displayed));
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(30));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.Id("dlgSynchronizing")).Any(elem => elem.Displayed == false));
                Thread.Sleep(1000);  // wait a while to make sure the General Overview has been refreshed

                // click on synchronization logging link
                IWebElement syncLogging = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Last synchronized with Phabricator at:')]"))
                                                    .FindElement(By.XPath("./../.."))
                                                    .FindElement(By.TagName("a"));
                syncLogging.Click();

                // verify if modified task has been shown in sync-logging
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElements(By.PartialLinkText("Make coffee")).Any(elem => elem.Displayed));

                // view changes
                IWebElement maniphestTaskChanges = WebBrowser.FindElement(By.PartialLinkText("Make coffee"))
                                                             .FindElement(By.XPath("./../.."))
                                                             .FindElement(By.PartialLinkText("View changes"));
                maniphestTaskChanges.Click();

                // verify changes
                IWebElement leftModification = WebBrowser.FindElements(By.TagName("td"))
                                                         .FirstOrDefault(td => td.Text.Equals("# Go to bed"));
                Assert.IsTrue(leftModification.GetAttribute("class")
                                              .Split(' ')
                                              .Contains("delete")
                             );

                // go back to sync-loggingoverview
                IWebElement btnOverviewLatestSynchronization = WebBrowser.FindElement(By.XPath("//a[contains(text(), 'Overview latest synchronization')]"));
                btnOverviewLatestSynchronization.Click();

                // verify if modified task has been shown in sync-logging
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElements(By.PartialLinkText("Make coffee")).Any(elem => elem.Displayed));
            }
            finally
            {
                dummyPhabricatorWebServer.Stop();
            }
        }
    }
}