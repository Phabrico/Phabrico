using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
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

        [TestMethod]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        [DataRow(typeof(FirefoxConfig), "")]
        [DataRow(typeof(FirefoxConfig), "phabrico")]
        public void SynchronizeUnfrozenChangesOnly(Type browser, string httpRootPath)
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

                // click on 'Phriction' in the menu navigator
                IWebElement navigatorPhriction = WebBrowser.FindElement(By.LinkText("Phriction"));
                navigatorPhriction.Click();
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElements(By.PartialLinkText("Grandchild")).Any(elem => elem.Displayed));

                // click on 'Grandchild' sub-document
                IWebElement phrictionGrandchild = WebBrowser.FindElement(By.PartialLinkText("Grandchild"));
                phrictionGrandchild.Click();
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any(elem => elem.Displayed && string.IsNullOrWhiteSpace(elem.Text) == false && elem.Text.Split('\r', '\n')[0].Equals("Grandchild")));

                // if action pane is collapsed -> expand it
                bool actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                                     .GetAttribute("class")
                                                     .Contains("right-collapsed");

                if (actionPaneCollapsed)
                {
                    IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                    expandActionPane.Click();
                }

                // click on Edit
                IWebElement edit = WebBrowser.FindElement(By.LinkText("Edit Document"));
                edit.Click();

                // wait a while
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

                // edit content
                IWebElement textarea = WebBrowser.FindElement(By.Id("textarea"));
                textarea.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.End);
                textarea.SendKeys("\nand I'm proud of it");

                // click Save button
                IWebElement btnSave = WebBrowser.FindElement(By.Id("btnSave"));
                btnSave.Click();
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any(elem => elem.Displayed && string.IsNullOrWhiteSpace(elem.Text) == false && elem.Text.Split('\r', '\n')[0].Equals("Grandchild")));



                // click on 'Child' (parent)-document
                IWebElement phrictionChild = WebBrowser.FindElement(By.PartialLinkText("Child"));
                phrictionChild.Click();
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any(elem => elem.Displayed && string.IsNullOrWhiteSpace(elem.Text) == false && elem.Text.Split('\r', '\n')[0].Equals("Child")));

                // if action pane is collapsed -> expand it
                actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                                .GetAttribute("class")
                                                .Contains("right-collapsed");

                if (actionPaneCollapsed)
                {
                    IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                    expandActionPane.Click();
                }

                // click on Edit
                edit = WebBrowser.FindElement(By.LinkText("Edit Document"));
                edit.Click();

                // wait a while
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

                // edit content
                textarea = WebBrowser.FindElement(By.Id("textarea"));
                textarea.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.End);
                textarea.SendKeys("\nand I'm proud of it");

                // click Save button
                btnSave = WebBrowser.FindElement(By.Id("btnSave"));
                btnSave.Click();
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any(elem => elem.Displayed && string.IsNullOrWhiteSpace(elem.Text) == false && elem.Text.Split('\r', '\n')[0].Equals("Child")));

                // click on logo to go back to the homepage
                IWebElement logo = WebBrowser.FindElement(By.XPath("//a[contains(@href, '')]"));
                logo.Click();

                // click on 'Maniphest' in the menu navigator
                IWebElement navigatorManiphest = WebBrowser.FindElement(By.PartialLinkText("Maniphest"));
                navigatorManiphest.Click();
                Thread.Sleep(1000);

                // create new task
                IWebElement btnCreateTask = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Create Task')]"));
                btnCreateTask.Click();

                // wait a while
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElements(By.XPath("//*[contains(text(), 'Save Changes')]")).Any());

                // Enter new task details
                Actions builder = new Actions(WebBrowser);
                builder.SendKeys("My first new task");
                builder.SendKeys(Keys.Tab);
                builder.SendKeys("Jeanie");
                builder.Perform();
                Thread.Sleep(500); // wait for javascript
                builder = new Actions(WebBrowser);
                builder.SendKeys(Keys.Enter);
                builder.Perform();
                builder = new Actions(WebBrowser);
                builder.SendKeys(Keys.Tab);
                builder.SendKeys(Keys.Tab);
                builder.SendKeys("Clas");
                builder.Perform();
                Thread.Sleep(500); // wait for javascript
                builder = new Actions(WebBrowser);
                builder.SendKeys(Keys.Enter);
                builder.Perform();
                builder = new Actions(WebBrowser);
                builder.SendKeys(Keys.Tab);
                builder.SendKeys("Mus");
                builder.Perform();
                Thread.Sleep(500); // wait for javascript
                builder = new Actions(WebBrowser);
                builder.SendKeys(Keys.Enter);
                builder.Perform();
                builder = new Actions(WebBrowser);
                builder.SendKeys(Keys.Tab);
                builder.SendKeys("This is a very difficult task");

                // save data
                builder.SendKeys(Keys.Tab);
                builder.SendKeys(Keys.Enter);
                builder.Perform();

                // wait a while
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElements(By.ClassName("maniphest-list-item-title")).Any(taskTitle => taskTitle.Text.Equals("T-1 My first new task")));


                // create second new task
                btnCreateTask = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Create Task')]"));
                btnCreateTask.Click();

                // wait a while
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElements(By.XPath("//*[contains(text(), 'Save Changes')]")).Any());

                // Enter new task details
                builder = new Actions(WebBrowser);
                builder.SendKeys("My second new task");
                builder.SendKeys(Keys.Tab);
                builder.SendKeys("Jeanie");
                builder.Perform();
                Thread.Sleep(500); // wait for javascript
                builder = new Actions(WebBrowser);
                builder.SendKeys(Keys.Enter);
                builder.Perform();
                builder = new Actions(WebBrowser);
                builder.SendKeys(Keys.Tab);
                builder.SendKeys(Keys.Tab);
                builder.SendKeys("Clas");
                builder.Perform();
                Thread.Sleep(500); // wait for javascript
                builder = new Actions(WebBrowser);
                builder.SendKeys(Keys.Enter);
                builder.Perform();
                builder = new Actions(WebBrowser);
                builder.SendKeys(Keys.Tab);
                builder.SendKeys("Mus");
                builder.Perform();
                Thread.Sleep(500); // wait for javascript
                builder = new Actions(WebBrowser);
                builder.SendKeys(Keys.Enter);
                builder.Perform();
                builder = new Actions(WebBrowser);
                builder.SendKeys(Keys.Tab);
                builder.SendKeys("This is an easier task");

                // save data
                builder.SendKeys(Keys.Tab);
                builder.SendKeys(Keys.Enter);
                builder.Perform();

                // wait a while
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElements(By.ClassName("maniphest-list-item-title")).Any(taskTitle => taskTitle.Text.Equals("T-2 My second new task")));

                // click on logo to go back to the homepage
                logo = WebBrowser.FindElement(By.XPath("//a[contains(@href, '')]"));
                logo.Click();

                // wait a while
                Thread.Sleep(1000);

                // Open 'Offline changes' screen
                IWebElement navigatorOfflineChanges = WebBrowser.FindElement(By.PartialLinkText("Offline changes"));
                navigatorOfflineChanges.Click();

                // wait a while
                Thread.Sleep(1000);

                // verify content of Offline Changes screen
                Assert.IsTrue(WebBrowser.FindElements(By.XPath("//a[contains(text(), 'T-1: My first new task')]")).Count(elem => elem.Displayed) == 1);
                Assert.IsTrue(WebBrowser.FindElements(By.XPath("//a[contains(text(), 'T-2: My second new task')]")).Count(elem => elem.Displayed) == 1);
                Assert.IsTrue(WebBrowser.FindElements(By.XPath("//a[contains(text(), 'Child')]")).Count(elem => elem.Displayed) == 2);
                Assert.IsTrue(WebBrowser.FindElements(By.XPath("//a[contains(text(), 'Child > Grandchild')]")).Count(elem => elem.Displayed) == 1);

                // Freeze first task
                IWebElement taskToFreeze = WebBrowser.FindElement(By.XPath("//a[contains(text(), 'T-1: My first new task')]"));
                IWebElement freezeButton = taskToFreeze.FindElement(By.XPath("../.."))
                                                       .FindElement(By.ClassName("freeze"))
                                                       .FindElement(By.TagName("a"));
                freezeButton.Click();
                Thread.Sleep(500);

                // verify is task is frozen
                IWebElement frozenTask = WebBrowser.FindElement(By.XPath("//a[contains(text(), 'T-1: My first new task')]"));
                Assert.IsTrue(frozenTask.FindElement(By.XPath("../..")).GetAttribute("class").Split(' ').Contains("frozen"));

                // Freeze grandchild document
                IWebElement wikiToFreeze = WebBrowser.FindElement(By.XPath("//a[contains(text(), 'Child > Grandchild')]"));
                freezeButton = wikiToFreeze.FindElement(By.XPath("../.."))
                                                       .FindElement(By.ClassName("freeze"))
                                                       .FindElement(By.TagName("a"));
                freezeButton.Click();
                Thread.Sleep(500);

                // verify is wiki document is frozen
                IWebElement frozenWiki = WebBrowser.FindElement(By.XPath("//a[contains(text(), 'T-1: My first new task')]"));
                Assert.IsTrue(frozenWiki.FindElement(By.XPath("../..")).GetAttribute("class").Split(' ').Contains("frozen"));


                // Open 'Offline changes' screen again
                navigatorOfflineChanges = WebBrowser.FindElement(By.PartialLinkText("Offline changes"));
                navigatorOfflineChanges.Click();

                // wait a while
                Thread.Sleep(1000);

                // verify content of Offline Changes screen
                Assert.IsTrue(WebBrowser.FindElements(By.XPath("//a[contains(text(), 'T-1: My first new task')]")).Count(elem => elem.Displayed) == 1);
                Assert.IsTrue(WebBrowser.FindElements(By.XPath("//a[contains(text(), 'T-2: My second new task')]")).Count(elem => elem.Displayed) == 1);
                Assert.IsTrue(WebBrowser.FindElements(By.XPath("//a[contains(text(), 'Child')]")).Count(elem => elem.Displayed) == 2);
                Assert.IsTrue(WebBrowser.FindElements(By.XPath("//a[contains(text(), 'Child > Grandchild')]")).Count(elem => elem.Displayed) == 1);
                Assert.IsTrue(WebBrowser.FindElements(By.ClassName("frozen")).Count == 2);
            }
            finally
            {
                dummyPhabricatorWebServer.Stop();
            }
        }
    }
}