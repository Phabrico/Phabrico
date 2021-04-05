using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.ObjectModel;
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
            bool actionPaneCollapsed = true;
            try
            {
                WebBrowser.FindElement(By.ClassName("right-collapsed"));
            }
            catch
            {
                actionPaneCollapsed = false;
            }

            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // click on Edit
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
        }
    }
}