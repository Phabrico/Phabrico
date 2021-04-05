using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using WebDriverManager.DriverConfigs.Impl;

namespace Phabrico.UnitTests.Browser.Chrome
{
    [TestClass]
    public class PhrictionUnitTests : BrowserUnitTest
    {
        public PhrictionUnitTests() : base(new ChromeConfig())
        {
            WebBrowser = new ChromeDriver();
        }
        
        [TestMethod]
        public void OpenPhrictionAndEdit()
        {
            Logon();

            // click on 'Phriction' in the menu navigator
            IWebElement navigatorPhriction = WebBrowser.FindElement(By.LinkText("Phriction"));
            navigatorPhriction.Click();

            // wait a while
            WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("phui-document")));

            // validate if Phriction was opened
            IWebElement phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            string phrictionDocumentTitle = phrictionDocument.Text.Split('\r', '\n')[0];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Story of my life"), "Unable to open Phriction");

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
            IWebElement edit = WebBrowser.FindElement(By.LinkText("Edit Document"));
            edit.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("phriction-edit")));

            // edit content
            IWebElement textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.End);
            textarea.SendKeys(" and saw it was all good...");

            // click Save button
            IWebElement btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("phui-document")));

            // validate if modifications were stored
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            phrictionDocumentTitle = phrictionDocument.Text.Replace("\r", "").Split('\n')[1];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Once upon a time, I was reading this story over and over again and saw it was all good..."), "Modifications couldn't be saved");

            // verify if new saved document is searchable
            IWebElement searchPhabrico = WebBrowser.FindElement(By.Id("searchPhabrico"));
            searchPhabrico.SendKeys("good");
            wait.Until(condition => condition.FindElement(By.ClassName("search-result")));
            IWebElement searchResult = WebBrowser.FindElement(By.ClassName("search-result"));
            Assert.IsTrue(searchResult.Displayed);
            Assert.AreEqual("Story of my life", searchResult.GetAttribute("name"));
        }
    }
}