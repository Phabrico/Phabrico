using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Linq;
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
        public void CreateFirstPhrictionAndEditAgain()
        {
            // remove all Phriction documents in current test database
            Storage.Phriction phrictionStorage = new Storage.Phriction();
            foreach (Phabricator.Data.Phriction phrictionDocumentToBeRmoved in phrictionStorage.Get(Database).ToList())
            {
                phrictionStorage.Remove(Database, phrictionDocumentToBeRmoved);
            }

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
            Assert.IsTrue(phrictionDocumentTitle.Equals("Page Not Found"), "Unable to open Phriction");

            // click on Create this Document button
            IWebElement btnCreateThisDocument = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Create this Document')]"));
            btnCreateThisDocument.Click();
            wait.Until(condition => condition.FindElement(By.Id("title")));

            // enter data for first Phriction document
            IWebElement inputTitle = WebBrowser.FindElement(By.Id("title"));
            inputTitle.SendKeys("My first wiki document");
            IWebElement textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys("I have a lot to say. Hang on tightly to the branches of the trees.");

            // click save button
            IWebElement btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();
            wait.Until(condition => condition.FindElement(By.ClassName("phriction")));

            // validate document title
            string documentTitle = WebBrowser.FindElement(By.ClassName("phui-document"))
                                             .FindElement(By.TagName("H1"))
                                             .Text;
            Assert.AreEqual("My first wiki document", documentTitle);

            // validate document content
            string documentContent = WebBrowser.FindElement(By.Id("remarkupContent"))
                                               .Text;
            Assert.AreEqual("I have a lot to say. Hang on tightly to the branches of the trees.", documentContent);

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
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("phriction-edit")));

            // edit content
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.End);
            textarea.SendKeys("\nToo bad you already knew what I was gonna tell");

            // click Save button
            btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("phui-document")));

            // validate if modifications were stored
            // validate document title
            documentTitle = WebBrowser.FindElement(By.ClassName("phui-document"))
                                      .FindElement(By.TagName("H1"))
                                      .Text;
            Assert.AreEqual("My first wiki document", documentTitle);

            // validate document content
            documentContent = WebBrowser.FindElement(By.Id("remarkupContent"))
                                        .Text;
            Assert.AreEqual("I have a lot to say. Hang on tightly to the branches of the trees.\nToo bad you already knew what I was gonna tell", documentContent.Replace("\r", ""));

            // verify if new saved document is searchable
            IWebElement searchPhabrico = WebBrowser.FindElement(By.Id("searchPhabrico"));
            searchPhabrico.SendKeys("tightly");
            wait.Until(condition => condition.FindElement(By.ClassName("search-result")));
            IWebElement searchResult = WebBrowser.FindElement(By.ClassName("search-result"));
            Assert.IsTrue(searchResult.Displayed);
            Assert.AreEqual("My first wiki document", searchResult.GetAttribute("name"));
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

        [TestMethod]
        public void CreateNewSubdocuments()
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

            // navigate to "Story of my dad's life" document
            IWebElement linkStoryDadsLife = WebBrowser.FindElement(By.XPath("//*[contains(text(), \"Story of my dad's life\")]"));
            linkStoryDadsLife.Click();

            // validate if correct document is shown
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            phrictionDocumentTitle = phrictionDocument.Text.Split('\r', '\n')[0];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Story of my dad's life"), "Unable to open dad's life story");

            //////////////////////////////////////////////////////////////////////////////////////////////////////
            // create first subdocument: click on New Document button
            IWebElement btnNewDocument = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'New Document')]"));
            btnNewDocument.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("phriction-edit")));

            // enter data for first Phriction document
            IWebElement inputTitle = WebBrowser.FindElement(By.Id("title"));
            inputTitle.SendKeys("Today is the greatest day I've ever known");
            IWebElement textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys("But I can't tell what tomorrow will be");

            // click save button
            IWebElement btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();
            wait.Until(condition => condition.FindElement(By.ClassName("phriction")));

            // validate document title
            string documentTitle = WebBrowser.FindElement(By.ClassName("phui-document"))
                                             .FindElement(By.TagName("H1"))
                                             .Text;
            Assert.AreEqual("Today is the greatest day I've ever known", documentTitle);

            // validate document content
            string documentContent = WebBrowser.FindElement(By.Id("remarkupContent"))
                                               .Text;
            Assert.AreEqual("But I can't tell what tomorrow will be", documentContent);

            // validate crumbs
            string navigationCrumbs = WebBrowser.FindElement(By.Id("crumbsContainer"))
                                                .Text
                                                .Split('\n')[0]
                                                .Replace("\r", "");
            Assert.AreEqual("Phriction > Story of my dad's life > Today is the greatest day I've ever known", navigationCrumbs);


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
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("phriction-edit")));

            // edit content
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.End);
            textarea.SendKeys("\n10 to 1 it will not be back to the future");

            // click Save button
            btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("phui-document")));

            // validate if modifications were stored
            documentTitle = WebBrowser.FindElement(By.ClassName("phui-document"))
                                      .FindElement(By.TagName("H1"))
                                      .Text;
            Assert.AreEqual("Today is the greatest day I've ever known", documentTitle);

            // validate document content
            documentContent = WebBrowser.FindElement(By.Id("remarkupContent"))
                                               .Text
                                               .Replace("\r", "");
            Assert.AreEqual("But I can't tell what tomorrow will be\n10 to 1 it will not be back to the future", documentContent);

            // validate crumbs
            navigationCrumbs = WebBrowser.FindElement(By.Id("crumbsContainer"))
                                         .Text
                                         .Split('\n')[0]
                                         .Replace("\r", "");
            Assert.AreEqual("Phriction > Story of my dad's life > Today is the greatest day I've ever known", navigationCrumbs);

            // verify if new saved document is searchable
            IWebElement searchPhabrico = WebBrowser.FindElement(By.Id("searchPhabrico"));
            searchPhabrico.SendKeys("future");
            wait.Until(condition => condition.FindElement(By.ClassName("search-result")));
            IWebElement searchResult = WebBrowser.FindElement(By.ClassName("search-result"));
            Assert.IsTrue(searchResult.Displayed);
            Assert.AreEqual("Today is the greatest day I've ever known", searchResult.GetAttribute("name"));

            // clear search field
            searchPhabrico.Clear();


            //////////////////////////////////////////////////////////////////////////////////////////////////////
            // create second subdocument: click on New Document button
            btnNewDocument = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'New Document')]"));
            btnNewDocument.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("phriction-edit")));

            // enter data for first Phriction document
            inputTitle = WebBrowser.FindElement(By.Id("title"));
            inputTitle.SendKeys("After five years in the institution...");
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys("now I wanna be a good boy");

            // click save button
            btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();
            wait.Until(condition => condition.FindElement(By.ClassName("phriction")));

            // validate document title
            documentTitle = WebBrowser.FindElement(By.ClassName("phui-document"))
                                             .FindElement(By.TagName("H1"))
                                             .Text;
            Assert.AreEqual("After five years in the institution...", documentTitle);

            // validate document content
            documentContent = WebBrowser.FindElement(By.Id("remarkupContent"))
                                               .Text;
            Assert.AreEqual("now I wanna be a good boy", documentContent);

            // validate crumbs
            navigationCrumbs = WebBrowser.FindElement(By.Id("crumbsContainer"))
                                                .Text
                                                .Split('\n')[0]
                                                .Replace("\r", "");
            Assert.AreEqual("Phriction > Story of my dad's life > Today is the greatest day I've ever known > After five years in the institution...", navigationCrumbs);


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
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("phriction-edit")));

            // edit content
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.End);
            textarea.SendKeys("\nAfter eating that chicken vindaloo, I wanna be well");

            // click Save button
            btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("phui-document")));

            // validate if modifications were stored
            documentTitle = WebBrowser.FindElement(By.ClassName("phui-document"))
                                      .FindElement(By.TagName("H1"))
                                      .Text;
            Assert.AreEqual("After five years in the institution...", documentTitle);

            // validate document content
            documentContent = WebBrowser.FindElement(By.Id("remarkupContent"))
                                               .Text
                                               .Replace("\r", "");
            Assert.AreEqual("now I wanna be a good boy\nAfter eating that chicken vindaloo, I wanna be well", documentContent);

            // validate crumbs
            navigationCrumbs = WebBrowser.FindElement(By.Id("crumbsContainer"))
                                         .Text
                                         .Split('\n')[0]
                                         .Replace("\r", "");
            Assert.AreEqual("Phriction > Story of my dad's life > Today is the greatest day I've ever known > After five years in the institution...", navigationCrumbs);
        }


        [TestMethod]
        public void CreateNewSubdocumentByMeansOfNewLink()
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

             // navigate to "Story of my dad's life" document
            IWebElement linkStoryDadsLife = WebBrowser.FindElement(By.XPath("//*[contains(text(), \"Story of my dad's life\")]"));
            linkStoryDadsLife.Click();

            // validate if correct document is shown
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            phrictionDocumentTitle = phrictionDocument.Text.Split('\r', '\n')[0];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Story of my dad's life"), "Unable to open dad's life story");

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
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("phriction-edit")));

            // edit content
            IWebElement textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.End);
            textarea.SendKeys("\n\n[[ ./new-link | This is a new link ]]\n");

            // click Save button
            IWebElement btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("phui-document")));

            // get newly created anchor link
            IWebElement anchor = WebBrowser.FindElement(By.LinkText("This is a new link"));
            Assert.IsTrue(anchor.GetAttribute("href").EndsWith("/w/x/new-link?title=This%20is%20a%20new%20link"));

            // click on anchor
            anchor.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("phui-document")));

            // validate if Phriction was opened
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            phrictionDocumentTitle = phrictionDocument.Text.Split('\r', '\n')[0];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Page Not Found"), "Unable to open Phriction");

            // click on Create this Document button
            IWebElement btnCreateThisDocument = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Create this Document')]"));
            btnCreateThisDocument.Click();
            wait.Until(condition => condition.FindElement(By.Id("title")));

            // enter data for first Phriction document
            IWebElement inputTitle = WebBrowser.FindElement(By.Id("title"));
            Assert.IsTrue(inputTitle.GetAttribute("value").Equals("This is a new link"), "Title of new document not correctly set");

            // Enter some text
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys("Bird is the word");

            // click save button
            btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();
            wait.Until(condition => condition.FindElement(By.ClassName("phriction")));

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("phui-document")));

            // validate if modifications were stored
            string documentTitle = WebBrowser.FindElement(By.ClassName("phui-document"))
                                      .FindElement(By.TagName("H1"))
                                      .Text;
            Assert.AreEqual("This is a new link", documentTitle);

            // validate document content
            string documentContent = WebBrowser.FindElement(By.Id("remarkupContent"))
                                               .Text
                                               .Replace("\r", "");
            Assert.AreEqual("Bird is the word", documentContent);

            // validate crumbs
            string navigationCrumbs = WebBrowser.FindElement(By.Id("crumbsContainer"))
                                         .Text
                                         .Split('\n')[0]
                                         .Replace("\r", "");
            Assert.AreEqual("Phriction > Story of my dad's life > This is a new link", navigationCrumbs);
        }
    }
}