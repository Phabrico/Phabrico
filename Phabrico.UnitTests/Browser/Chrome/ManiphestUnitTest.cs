using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using Phabrico.UnitTests.Synchronization;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using WebDriverManager.DriverConfigs.Impl;

namespace Phabrico.UnitTests.Browser.Chrome
{
    [TestClass]
    public class ManiphestUnitTests : BrowserUnitTest
    {
        public ManiphestUnitTests() : base(new ChromeConfig())
        {
            WebBrowser = new ChromeDriver();
        }
        
        [TestMethod]
        public void OpenManiphestAndEdit()
        {
            Logon();

            // click on 'Maniphest' in the menu navigator
            IWebElement navigatorManiphest = WebBrowser.FindElement(By.PartialLinkText("Maniphest"));
            navigatorManiphest.Click();

            // wait a while
            WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("maniphest-list-head")));

            // validate if Maniphest was opened
            IWebElement maniphestTasksOverview = WebBrowser.FindElement(By.ClassName("maniphest-list-head"));
            string maniphestTasksOverviewTitle = maniphestTasksOverview.Text;
            Assert.IsTrue(maniphestTasksOverviewTitle.Equals("High"), "Unable to open Maniphest");

            // check if we have 2 High priority tasks
            maniphestTasksOverview = WebBrowser.FindElement(By.ClassName("maniphest-list-view"));
            ReadOnlyCollection<IWebElement> tasks = maniphestTasksOverview.FindElements(By.ClassName("maniphest-list-item"));
            Assert.IsTrue(tasks.Count == 2, "Invalid number of tasks");

            // open task
            IWebElement taskPlayIntroChildInTime = WebBrowser.FindElement(By.LinkText("Play the intro of Child In Time"));
            taskPlayIntroChildInTime.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.Id("remarkupContent")));

            // validate if task content is correct was opened
            IWebElement maniphestTaskContent = WebBrowser.FindElement(By.Id("remarkupContent"));
            Assert.IsTrue(maniphestTaskContent.Text.Equals("G2 G2 A2"), "Maniphest task content incorrect");

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
            wait.Until(condition => condition.FindElement(By.LinkText("Edit Task")));
            IWebElement edit = WebBrowser.FindElement(By.LinkText("Edit Task"));
            edit.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("maniphest-task-edit")));

            // edit content
            IWebElement textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.Home);
            textarea.SendKeys("Left: ");
            textarea.SendKeys(OpenQA.Selenium.Keys.End);
            textarea.SendKeys(OpenQA.Selenium.Keys.Enter);
            textarea.SendKeys("Right: C5 B4 A4 G4 F4 G4 E4");

            // click Save button
            IWebElement btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("maniphest-task")));

            // validate if modifications were stored
            maniphestTaskContent = WebBrowser.FindElement(By.Id("remarkupContent"));
            Assert.IsTrue(maniphestTaskContent.Text.Equals("Left: G2 G2 A2\r\nRight: C5 B4 A4 G4 F4 G4 E4"), "Task content was not modified");
            
            // verify if new saved task is searchable
            IWebElement searchPhabrico = WebBrowser.FindElement(By.Id("searchPhabrico"));
            searchPhabrico.SendKeys("child");
            wait.Until(condition => condition.FindElement(By.ClassName("search-result")));
            IWebElement searchResult = WebBrowser.FindElement(By.ClassName("search-result"));
            Assert.IsTrue(searchResult.Displayed);
            Assert.AreEqual("T2145: Play the intro of Child In Time", searchResult.GetAttribute("name"));

            // add new comment
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys("Sounds good, but can you also play the solo of 'Burn' ? {F1234, size=full}");
            IWebElement btnSetSailForAdventure = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Set Sail for Adventure')]"));
            btnSetSailForAdventure.Click();
            wait.Until(condition => condition.FindElement(By.XPath("//*[contains(@src, '/file/data/1234/')]")));
        }
        
        [TestMethod]
        public void OpenManiphestAndEditWithWindowsAuthentication()
        {
            Logon();

            // click on 'Config' in the menu navigator
            IWebElement navigatorConfig = WebBrowser.FindElement(By.PartialLinkText("Config"));
            navigatorConfig.Click();

            // wait a while
            WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.Id("autoLogon")));

            // hide first-time help
            IWebElement overlay = WebBrowser.FindElement(By.ClassName("overlay"));
            overlay.Click();

            // configure Windows autoLogon
            IWebElement cmbAutoLogon = WebBrowser.FindElement(By.Id("autoLogon"));
            cmbAutoLogon.Click();
            cmbAutoLogon.FindElements(By.TagName("option"))
                                        .Single(option => option.Text == "Yes (Windows authentication)")
                                        .Click();

            // click on logo to go back to the homepage
            IWebElement logo = WebBrowser.FindElement(By.XPath("//a[contains(@href, '/')]"));
            logo.Click();

            // go back to configuration to verify if autologon has been changed
            navigatorConfig = WebBrowser.FindElement(By.PartialLinkText("Config"));
            navigatorConfig.Click();

            // verify cmbAutoLogon
            cmbAutoLogon = WebBrowser.FindElement(By.Id("autoLogon"));
            Assert.IsTrue( cmbAutoLogon.FindElements(By.TagName("option"))
                                       .Single(option => option.Text == "Yes (Windows authentication)")
                                       .Selected
                         );

            // click on logo to go back to the homepage
            logo = WebBrowser.FindElement(By.XPath("//a[contains(@href, '/')]"));
            logo.Click();

            // click on 'Maniphest' in the menu navigator
            IWebElement navigatorManiphest = WebBrowser.FindElement(By.PartialLinkText("Maniphest"));
            navigatorManiphest.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("maniphest-list-head")));

            // validate if Maniphest was opened
            IWebElement maniphestTasksOverview = WebBrowser.FindElement(By.ClassName("maniphest-list-head"));
            string maniphestTasksOverviewTitle = maniphestTasksOverview.Text;
            Assert.IsTrue(maniphestTasksOverviewTitle.Equals("High"), "Unable to open Maniphest");

            // check if we have 2 High priority tasks
            maniphestTasksOverview = WebBrowser.FindElement(By.ClassName("maniphest-list-view"));
            ReadOnlyCollection<IWebElement> tasks = maniphestTasksOverview.FindElements(By.ClassName("maniphest-list-item"));
            Assert.IsTrue(tasks.Count == 2, "Invalid number of tasks");

            // open task
            IWebElement taskPlayIntroChildInTime = WebBrowser.FindElement(By.LinkText("Play the intro of Child In Time"));
            taskPlayIntroChildInTime.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.Id("remarkupContent")));

            // validate if task content is correct was opened
            IWebElement maniphestTaskContent = WebBrowser.FindElement(By.Id("remarkupContent"));
            Assert.IsTrue(maniphestTaskContent.Text.Equals("G2 G2 A2"), "Maniphest task content incorrect");

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
            wait.Until(condition => condition.FindElement(By.LinkText("Edit Task")));
            IWebElement edit = WebBrowser.FindElement(By.LinkText("Edit Task"));
            edit.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("maniphest-task-edit")));

            // edit content
            IWebElement textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.Home);
            textarea.SendKeys("Left: ");
            textarea.SendKeys(OpenQA.Selenium.Keys.End);
            textarea.SendKeys(OpenQA.Selenium.Keys.Enter);
            textarea.SendKeys("Right: C5 B4 A4 G4 F4 G4 E4");

            // click Save button
            IWebElement btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("maniphest-task")));

            // validate if modifications were stored
            maniphestTaskContent = WebBrowser.FindElement(By.Id("remarkupContent"));
            Assert.IsTrue(maniphestTaskContent.Text.Equals("Left: G2 G2 A2\r\nRight: C5 B4 A4 G4 F4 G4 E4"), "Task content was not modified");
            
            // verify if new saved task is searchable
            IWebElement searchPhabrico = WebBrowser.FindElement(By.Id("searchPhabrico"));
            searchPhabrico.SendKeys("child");
            wait.Until(condition => condition.FindElement(By.ClassName("search-result")));
            IWebElement searchResult = WebBrowser.FindElement(By.ClassName("search-result"));
            Assert.IsTrue(searchResult.Displayed);
            Assert.AreEqual("T2145: Play the intro of Child In Time", searchResult.GetAttribute("name"));

            // add new comment
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys("Sounds good, but can you also play the solo of 'Burn' ? {F1234, size=full}");
            IWebElement btnSetSailForAdventure = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Set Sail for Adventure')]"));
            btnSetSailForAdventure.Click();
            wait.Until(condition => condition.FindElement(By.XPath("//*[contains(@src, '/file/data/1234/')]")));
        }
        
        [TestMethod]
        public void OpenManiphestAndUnassignNonStagedTask()
        {
            Logon();

            // click on 'Maniphest' in the menu navigator
            IWebElement navigatorManiphest = WebBrowser.FindElement(By.PartialLinkText("Maniphest"));
            navigatorManiphest.Click();

            // wait a while
            WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("maniphest-list-head")));

            // validate if Maniphest was opened
            IWebElement maniphestTasksOverview = WebBrowser.FindElement(By.ClassName("maniphest-list-head"));
            string maniphestTasksOverviewTitle = maniphestTasksOverview.Text;
            Assert.IsTrue(maniphestTasksOverviewTitle.Equals("High"), "Unable to open Maniphest");

            // check if we have 2 High priority tasks
            maniphestTasksOverview = WebBrowser.FindElement(By.ClassName("maniphest-list-view"));
            ReadOnlyCollection<IWebElement> tasks = maniphestTasksOverview.FindElements(By.ClassName("maniphest-list-item"));
            Assert.IsTrue(tasks.Count == 2, "Invalid number of tasks");

            // open task
            IWebElement taskPlayIntroChildInTime = WebBrowser.FindElement(By.LinkText("Play the intro of Child In Time"));
            taskPlayIntroChildInTime.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.Id("remarkupContent")));

            // validate if task content is correct was opened
            IWebElement maniphestTaskContent = WebBrowser.FindElement(By.Id("remarkupContent"));
            Assert.IsTrue(maniphestTaskContent.Text.Equals("G2 G2 A2"), "Maniphest task content incorrect");

            // unassign task via 'Add action' drop down
            IWebElement cmbAddAction = WebBrowser.FindElement(By.ClassName("transaction"));
            cmbAddAction.Click();
            cmbAddAction.FindElements(By.TagName("option"))
                                        .Single(option => option.Text == "Assign / Claim")
                                        .Click();

            IWebElement removeAssignedUserTag = WebBrowser.FindElement(By.Id("transaction-owner"))
                                                          .FindElement(By.TagName("a"));
            removeAssignedUserTag.Click();

            // verify if no user is assigned to task
            Assert.IsTrue( WebBrowser.FindElement(By.Id("transaction-owner"))
                                     .FindElements(By.TagName("a"))
                                     .Count == 0
                         );

            // save
            IWebElement btnSetSailForAdventure = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Set Sail for Adventure')]"));
            btnSetSailForAdventure.Click();

            // if action pane is collapsed -> expand it
            bool actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                                 .GetAttribute("class")
                                                 .Contains("right-collapsed");

            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // verify if 'assigned-user-tag' is set to '(none)' in the action pane
            IWebElement assignedTo = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Assigned To')]"))
                                               .FindElement(By.XPath("./.."))                   // parent tag
                                               .FindElement(By.XPath("following-sibling::*"))   // next sibling tag
                                               .FindElement(By.TagName("a"));                   // anchor to 'assigned-user'
            Assert.AreEqual(assignedTo.Text, "(none)");

            // verify if last transaction item
            IWebElement lastTransactionItem = WebBrowser.FindElement(By.ClassName("timeline")).FindElements(By.ClassName("timeline-item")).LastOrDefault();
            Assert.IsTrue(lastTransactionItem.Text.StartsWith("Johnny Birddog reassigned the task from Johnny Birddog to (none)"));
        }
         
        [TestMethod]
        public void OpenManiphestAndUnassignStagedTask()
        {
            Logon();

            // click on 'Maniphest' in the menu navigator
            IWebElement navigatorManiphest = WebBrowser.FindElement(By.PartialLinkText("Maniphest"));
            navigatorManiphest.Click();

            // wait a while
            WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("maniphest-list-head")));

            // validate if Maniphest was opened
            IWebElement maniphestTasksOverview = WebBrowser.FindElement(By.ClassName("maniphest-list-head"));
            string maniphestTasksOverviewTitle = maniphestTasksOverview.Text;
            Assert.IsTrue(maniphestTasksOverviewTitle.Equals("High"), "Unable to open Maniphest");

            // check if we have 2 High priority tasks
            maniphestTasksOverview = WebBrowser.FindElement(By.ClassName("maniphest-list-view"));
            ReadOnlyCollection<IWebElement> tasks = maniphestTasksOverview.FindElements(By.ClassName("maniphest-list-item"));
            Assert.IsTrue(tasks.Count == 2, "Invalid number of tasks");

            // open task
            IWebElement taskPlayIntroChildInTime = WebBrowser.FindElement(By.LinkText("Play the intro of Child In Time"));
            taskPlayIntroChildInTime.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.Id("remarkupContent")));

            // validate if task content is correct was opened
            IWebElement maniphestTaskContent = WebBrowser.FindElement(By.Id("remarkupContent"));
            Assert.IsTrue(maniphestTaskContent.Text.Equals("G2 G2 A2"), "Maniphest task content incorrect");

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
            wait.Until(condition => condition.FindElement(By.LinkText("Edit Task")));
            IWebElement edit = WebBrowser.FindElement(By.LinkText("Edit Task"));
            edit.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("maniphest-task-edit")));

            // edit content
            IWebElement textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.Home);
            textarea.SendKeys("Left: ");
            textarea.SendKeys(OpenQA.Selenium.Keys.End);
            textarea.SendKeys(OpenQA.Selenium.Keys.Enter);
            textarea.SendKeys("Right: C5 B4 A4 G4 F4 G4 E4");

            // click Save button
            IWebElement btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("maniphest-task")));

            // validate if modifications were stored
            maniphestTaskContent = WebBrowser.FindElement(By.Id("remarkupContent"));
            Assert.IsTrue(maniphestTaskContent.Text.Equals("Left: G2 G2 A2\r\nRight: C5 B4 A4 G4 F4 G4 E4"), "Task content was not modified");
            
            // verify if new saved task is searchable
            IWebElement searchPhabrico = WebBrowser.FindElement(By.Id("searchPhabrico"));
            searchPhabrico.SendKeys("child");
            wait.Until(condition => condition.FindElement(By.ClassName("search-result")));
            IWebElement searchResult = WebBrowser.FindElement(By.ClassName("search-result"));
            Assert.IsTrue(searchResult.Displayed);
            Assert.AreEqual("T2145: Play the intro of Child In Time", searchResult.GetAttribute("name"));

            // unassign task via 'Add action' drop down
            IWebElement cmbAddAction = WebBrowser.FindElement(By.ClassName("transaction"));
            cmbAddAction.Click();
            cmbAddAction.FindElements(By.TagName("option"))
                                        .Single(option => option.Text == "Assign / Claim")
                                        .Click();

            IWebElement removeAssignedUserTag = WebBrowser.FindElement(By.Id("transaction-owner"))
                                                          .FindElement(By.TagName("a"));
            removeAssignedUserTag.Click();

            // verify if no user is assigned to task
            Assert.IsTrue( WebBrowser.FindElement(By.Id("transaction-owner"))
                                     .FindElements(By.TagName("a"))
                                     .Count == 0
                         );

            // save
            IWebElement btnSetSailForAdventure = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Set Sail for Adventure')]"));
            btnSetSailForAdventure.Click();

            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");

            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // verify if 'assigned-user-tag' is set to '(none)' in the action pane
            IWebElement assignedTo = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Assigned To')]"))
                                               .FindElement(By.XPath("./.."))                   // parent tag
                                               .FindElement(By.XPath("following-sibling::*"))   // next sibling tag
                                               .FindElement(By.TagName("a"));                   // anchor to 'assigned-user'
            Assert.AreEqual(assignedTo.Text, "(none)");

            // verify if last transaction item
            IWebElement lastTransactionItem = WebBrowser.FindElement(By.ClassName("timeline")).FindElements(By.ClassName("timeline-item")).LastOrDefault();
            Assert.IsTrue(lastTransactionItem.Text.StartsWith("Johnny Birddog assigned the task to (none)"));
        }

        [TestMethod]
        public void OpenManiphestAndAddOtherProjectTagAndSynchronize()
        {
            DummyPhabricatorWebServer dummyPhabricatorWebServer = new DummyPhabricatorWebServer();

            try
            {
                Logon();

                // click on 'Maniphest' in the menu navigator
                IWebElement navigatorManiphest = WebBrowser.FindElement(By.PartialLinkText("Maniphest"));
                navigatorManiphest.Click();

                // wait a while
                WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElement(By.ClassName("maniphest-list-head")));

                // validate if Maniphest was opened
                IWebElement maniphestTasksOverview = WebBrowser.FindElement(By.ClassName("maniphest-list-head"));
                string maniphestTasksOverviewTitle = maniphestTasksOverview.Text;
                Assert.IsTrue(maniphestTasksOverviewTitle.Equals("High"), "Unable to open Maniphest");

                // check if we have 2 High priority tasks
                maniphestTasksOverview = WebBrowser.FindElement(By.ClassName("maniphest-list-view"));
                ReadOnlyCollection<IWebElement> tasks = maniphestTasksOverview.FindElements(By.ClassName("maniphest-list-item"));
                Assert.IsTrue(tasks.Count == 2, "Invalid number of tasks");

                // open task
                IWebElement taskPlayIntroChildInTime = WebBrowser.FindElement(By.LinkText("Play the intro of Child In Time"));
                taskPlayIntroChildInTime.Click();

                // wait a while
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElement(By.Id("remarkupContent")));

                // validate if task content is correct was opened
                IWebElement maniphestTaskContent = WebBrowser.FindElement(By.Id("remarkupContent"));
                Assert.IsTrue(maniphestTaskContent.Text.Equals("G2 G2 A2"), "Maniphest task content incorrect");

                // Add 'Classic' project tag by means of 'Add action' drop down
                IWebElement cmbAddAction = WebBrowser.FindElement(By.ClassName("transaction"));
                cmbAddAction.Click();
                cmbAddAction.FindElements(By.TagName("option"))
                                            .Single(option => option.Text == "Change Project Tags")
                                            .Click();

                IWebElement inputProjectTags = WebBrowser.FindElement(By.Id("transaction-projectPHIDs"))
                                                         .FindElement(By.ClassName("input-tag"));
                inputProjectTags.Click();
                inputProjectTags = inputProjectTags.FindElement(By.ClassName("focused"))
                                                   .FindElements(By.TagName("input"))
                                                   .FirstOrDefault(input => input.GetAttribute("type").Equals("text"));
                inputProjectTags.SendKeys("classic");
                Thread.Sleep(500);  // wait some milliseconds to make sure the AJAX call for the projects-menu has been finished
                inputProjectTags.SendKeys(OpenQA.Selenium.Keys.Enter);

                // save
                IWebElement btnSetSailForAdventure = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Set Sail for Adventure')]"));
                btnSetSailForAdventure.Click();

                // if action pane is collapsed -> expand it
                bool actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                                     .GetAttribute("class")
                                                     .Contains("right-collapsed");

                if (actionPaneCollapsed)
                {
                    IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                    expandActionPane.Click();
                }

                // verify if no project tag is added to task
                Assert.IsTrue(WebBrowser.FindElements(By.ClassName("maniphest-list-item-project"))
                                        .Any(element => element.FindElements(By.TagName("a"))
                                                               .Any(anchor => anchor.GetAttribute("href").EndsWith("/project/info/PHID-PROJ-classic/"))
                                            )
                             );

                // verify if last transaction item
                IWebElement lastTransactionItem = WebBrowser.FindElement(By.ClassName("timeline")).FindElements(By.ClassName("timeline-item")).LastOrDefault();
                Assert.IsTrue(lastTransactionItem.Text.StartsWith("Johnny Birddog assigned the following projects: Classic"));

                // click on logo to go back to the homepage
                IWebElement logo = WebBrowser.FindElement(By.XPath("//a[contains(@href, '/')]"));
                logo.Click();

                // click on 'Synchronize' in the menu navigator
                IWebElement navigatorSynchronize = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Synchronize')]"));
                navigatorSynchronize.Click();

                // verify if sync-confirmation dialog is visible
                IWebElement confirmDialog = WebBrowser.FindElement(By.Id("dlgRequestSynchronizeDetail"));
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(2));
                wait.Until(condition => confirmDialog.Text.Equals("There are 2 local modifications ready to be uploaded the Phabricator server."));

                // click on Yes button
                IWebElement btnConfirmSynchronization = WebBrowser.FindElement(By.Id("dlgRequestSynchronize"))
                                                                  .FindElement(By.XPath("//*[contains(text(), 'Yes')]"));
                btnConfirmSynchronization.Click();

                // wait until synchronization process is finished
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElement(By.Id("dlgSynchronizing")).Displayed);
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(30));
                wait.Until(condition => condition.FindElement(By.Id("dlgSynchronizing")).Displayed == false);

                // verify if we don't have any local offline changes anymore
                System.Threading.Thread.Sleep(500);  // wait some milliseconds to make sure the page has been reloaded
                IWebElement hdrNumberOfUncommittedObjects = WebBrowser.FindElement(By.XPath("//label[contains(text(), 'Number of uncommitted objects:')]"));
                Assert.AreEqual(hdrNumberOfUncommittedObjects.FindElement(By.XPath(".//../.."))
                                                           .FindElements(By.TagName("td"))
                                                           .LastOrDefault()
                                                           .Text, 
                                 "0"
                               );
            }
            finally
            {
                dummyPhabricatorWebServer.Stop();
            }
        }
    }
}