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
    public class PhrictionUnitTests : BrowserUnitTest
    {
        [TestMethod]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        [DataRow(typeof(FirefoxConfig), "")]
        [DataRow(typeof(FirefoxConfig), "phabrico")]
        public void CreateFirstPhrictionAndEditAgain(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);

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
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

            // validate if Phriction was opened
            IWebElement phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            string phrictionDocumentTitle = phrictionDocument.Text.Split('\r', '\n')[0];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Page Not Found"), "Unable to open Phriction");

            // click on Create this Document button
            IWebElement btnCreateThisDocument = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Create this Document')]"));
            btnCreateThisDocument.Click();
            wait.Until(condition => condition.FindElements(By.Id("title")).Any());

            // enter data for first Phriction document
            IWebElement inputTitle = WebBrowser.FindElement(By.Id("title"));
            inputTitle.SendKeys("My first wiki document");
            IWebElement textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys("I have a lot to say. Hang on tightly to the branches of the trees.");

            // wait a while to make sure the verify-title AJAX call is finished
            wait.Until(condition => condition.FindElements(By.Id("btnSave")).Any(button => button.Enabled && button.Displayed));

            // click save button
            IWebElement btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();
            wait.Until(condition => condition.FindElements(By.ClassName("phriction")).Any());

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
            wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

            // edit content
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.End);
            textarea.SendKeys("\nToo bad you already knew what I was gonna tell");

            // click Save button
            btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

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
            wait.Until(condition => condition.FindElements(By.ClassName("search-result")).Any());
            IWebElement searchResult = WebBrowser.FindElement(By.ClassName("search-result"));
            Assert.IsTrue(searchResult.Displayed);
            Assert.AreEqual("My first wiki document", searchResult.GetAttribute("name"));
        }

        [TestMethod]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        [DataRow(typeof(FirefoxConfig), "")]
        [DataRow(typeof(FirefoxConfig), "phabrico")]
        public void OpenPhrictionAndEdit(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);
            Logon();

            // click on 'Phriction' in the menu navigator
            IWebElement navigatorPhriction = WebBrowser.FindElement(By.LinkText("Phriction"));
            navigatorPhriction.Click();

            // wait a while
            WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

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
            wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

            // edit content
            IWebElement textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.End);
            textarea.SendKeys(" and saw it was all good...");

            // click Save button
            IWebElement btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

            // validate if modifications were stored
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            phrictionDocumentTitle = phrictionDocument.Text.Replace("\r", "").Split('\n')[1];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Once upon a time, I was reading this story over and over again and saw it was all good..."), "Modifications couldn't be saved");

            // verify if new saved document is searchable
            IWebElement searchPhabrico = WebBrowser.FindElement(By.Id("searchPhabrico"));
            searchPhabrico.SendKeys("good");
            wait.Until(condition => condition.FindElements(By.ClassName("search-result")).Any());
            IWebElement searchResult = WebBrowser.FindElement(By.ClassName("search-result"));
            Assert.IsTrue(searchResult.Displayed);
            Assert.AreEqual("Story of my life", searchResult.GetAttribute("name"));

            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");

            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // click on 'Add to favorites'
            IWebElement addToFavorites = WebBrowser.FindElement(By.LinkText("Add to favorites"));
            addToFavorites.Click();
            Thread.Sleep(500);  // wait a while to make sure the javascript code has been finished
            
            // click on logo to go back to the homepage
            IWebElement logo = WebBrowser.FindElement(By.XPath("//a[contains(@href, '')]"));
            logo.Click();

            // verify that 'Story of my life' is shown as a favorite on the homepage
            IWebElement favorite = WebBrowser.FindElements(By.ClassName("app-main-window"))
                                             .Where(elem => elem.FindElements(By.PartialLinkText("Story of my life")).Any())
                                             .FirstOrDefault();
            Assert.IsNotNull(favorite);
            favorite = favorite.FindElement(By.PartialLinkText("Story of my life"));

            // go back to 'Story of my life'
            favorite.Click();

            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");

            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }


            // click on 'View local changes'
            IWebElement viewLocalChanges = WebBrowser.FindElement(By.LinkText("View local changes"));
            viewLocalChanges.Click();
            Thread.Sleep(500);  // wait a while to make sure the javascript code has been finished

            // verify that our addition is shown in the right diff window
            string diffLocalChanges = string.Join("", WebBrowser.FindElements(By.TagName("em")).Select(elem => elem.Text));
            Assert.AreEqual(" and saw it was all good...", diffLocalChanges);

            // add left line after right line
            IWebElement btnAddLeftAfterRight = WebBrowser.FindElement(By.ClassName("insert-after"));
            btnAddLeftAfterRight.Click();

            // verify right content
            IWebElement rightContent = WebBrowser.FindElement(By.Id("fileRight"));
            Assert.AreEqual(rightContent.Text, "1 Once upon a time, I was reading this story over and over again and saw it was all good...\r\nOnce upon a time, I was reading this story over and over again");

            // save modifications
            IWebElement btnSaveLocalChanges = WebBrowser.FindElement(By.Id("btnSaveLocalChanges"));
            btnSaveLocalChanges.Click();
            Thread.Sleep(500);  // wait a while to make sure the javascript code has been finished

            // click on 'Story of my life'
            phrictionDocument = WebBrowser.FindElement(By.LinkText("Story of my life"));
            phrictionDocument.Click();


            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");

            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // click on 'Dismiss local changes'
            IWebElement dismissLocalChanges = WebBrowser.FindElement(By.LinkText("Dismiss local changes"));
            dismissLocalChanges.Click();

            // click on Yes button
            IWebElement confirmDismissLocalChanges = WebBrowser.FindElement(By.XPath("//button[text()='Yes']"));
            confirmDismissLocalChanges.Click();
            Thread.Sleep(500);  // wait a while to make sure the javascript code has been finished
            
            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");

            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // Verify that the 'Dismiss local changes' action is not visible anymore
            dismissLocalChanges = WebBrowser.FindElements(By.LinkText("Dismiss local changes")).FirstOrDefault();
            Assert.IsNull(dismissLocalChanges);


            // click on 'Remove from favorites'
            IWebElement removeFromFavorites = WebBrowser.FindElement(By.LinkText("Remove from favorites"));
            removeFromFavorites.Click();
            Thread.Sleep(500);  // wait a while to make sure the javascript code has been finished
            
            // click on logo to go back to the homepage
            logo = WebBrowser.FindElement(By.XPath("//a[contains(@href, '')]"));
            logo.Click();

            // verify that 'Story of my life' is not shown anymore as a favorite on the homepage
            favorite = WebBrowser.FindElements(By.ClassName("app-main-window"))
                                 .Where(elem => elem.FindElements(By.PartialLinkText("Story of my life")).Any())
                                 .FirstOrDefault();
            Assert.IsNull(favorite);
        }

        [TestMethod]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        // [DataRow(typeof(FirefoxConfig), "")]         : disabled because of drag/drop issues with MouseActions.ClickAndHold 
        // [DataRow(typeof(FirefoxConfig), "phabrico")] : disabled because of drag/drop issues with MouseActions.ClickAndHold 
        public void CreateNewSubdocuments(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);
            Logon();

            // click on 'Phriction' in the menu navigator
            IWebElement navigatorPhriction = WebBrowser.FindElement(By.LinkText("Phriction"));
            navigatorPhriction.Click();

            // wait a while
            WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

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

            // click on 'Add to favorites'
            IWebElement addToFavorites = WebBrowser.FindElement(By.LinkText("Add to favorites"));
            addToFavorites.Click();
            Thread.Sleep(500);  // wait a while to make sure the javascript code has been finished

            //////////////////////////////////////////////////////////////////////////////////////////////////////
            // create first subdocument: click on New Document button
            IWebElement btnNewDocument = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'New Document')]"));
            btnNewDocument.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

            // enter data for first Phriction document
            IWebElement inputTitle = WebBrowser.FindElement(By.Id("title"));
            inputTitle.SendKeys("Today is the greatest day I've ever known");
            IWebElement textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys("But I can't tell what tomorrow will be");

            // wait a while to make sure the verify-title AJAX call is finished
            wait.Until(condition => condition.FindElements(By.Id("btnSave")).Any(button => button.Enabled && button.Displayed));

            // click save button
            IWebElement btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phriction")).Any());

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
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
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
            wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

            // edit content
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.End);
            textarea.SendKeys("\n10 to 1 it will not be back to the future");

            // click Save button
            btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

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
            wait.Until(condition => condition.FindElements(By.ClassName("search-result")).Any());
            IWebElement searchResult = WebBrowser.FindElement(By.ClassName("search-result"));
            Assert.IsTrue(searchResult.Displayed);
            Assert.AreEqual("Today is the greatest day I've ever known", searchResult.GetAttribute("name"));

            // clear search field
            searchPhabrico.Clear();

            
            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");

            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // click on 'Add to favorites'
            addToFavorites = WebBrowser.FindElement(By.LinkText("Add to favorites"));
            addToFavorites.Click();
            Thread.Sleep(500);  // wait a while to make sure the javascript code has been finished

            //////////////////////////////////////////////////////////////////////////////////////////////////////
            // create second subdocument: click on New Document button
            btnNewDocument = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'New Document')]"));
            btnNewDocument.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

            // enter data for first Phriction document
            inputTitle = WebBrowser.FindElement(By.Id("title"));
            inputTitle.SendKeys("After five years in the institution...");
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys("now I wanna be a good boy");

            // wait a while to make sure the verify-title AJAX call is finished
            wait.Until(condition => condition.FindElements(By.Id("btnSave")).Any(button => button.Enabled && button.Displayed));

            // click save button
            btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();
            wait.Until(condition => condition.FindElements(By.ClassName("phriction")).Any());

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
            wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

            // edit content
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.End);
            textarea.SendKeys("\nAfter eating that chicken vindaloo, I wanna be well");

            // click Save button
            btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

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

            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");

            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // click on 'Add to favorites'
            addToFavorites = WebBrowser.FindElement(By.LinkText("Add to favorites"));
            addToFavorites.Click();
            Thread.Sleep(500);  // wait a while to make sure the javascript code has been finished

            // click on logo to go back to the homepage
            IWebElement logo = WebBrowser.FindElement(By.XPath("//a[contains(@href, '')]"));
            logo.Click();

            // verify that 'Story of my dad's life' is shown as a favorite on the homepage
            IWebElement dadsStory = WebBrowser.FindElements(By.ClassName("app-main-window"))
                                              .SelectMany(elem => elem.FindElements(By.PartialLinkText("Story of my dad's life")))
                                              .FirstOrDefault()
                                              .FindElement(By.XPath("./../.."));
            Assert.IsNotNull(dadsStory);

            // verify that 'Story of my dad's life > Today is the greatest day I've ever known' is shown as a favorite on the homepage
            IWebElement todayIsGreat = WebBrowser.FindElements(By.ClassName("app-main-window"))
                                                 .SelectMany(elem => elem.FindElements(By.PartialLinkText("Story of my dad's life > Today is the greatest day I've ever known")))
                                                 .FirstOrDefault()
                                                 .FindElement(By.XPath("./../.."));
            Assert.IsNotNull(todayIsGreat);

            // verify that 'Story of my dad's life > Today is the greatest day I've ever known > After five years in the institution...' is shown as a favorite on the homepage
            IWebElement institution = WebBrowser.FindElements(By.ClassName("app-main-window"))
                                                .SelectMany(elem => elem.FindElements(By.PartialLinkText("Story of my dad's life > Today is the greatest day I've ever known > After five years in the institution...")))
                                                .FirstOrDefault()
                                                .FindElement(By.XPath("./../.."));
            Assert.IsNotNull(institution);

            // verify we have only 3 favorite items
            IWebElement[] favoritesList = dadsStory.FindElement(By.XPath("./.."))
                                                   .FindElements(By.ClassName("favorite-item"))
                                                   .ToArray();
            Assert.AreEqual(favoritesList.Length, 3);

            // verify the order of the favorite items
            Assert.AreEqual(favoritesList[0], dadsStory);
            Assert.AreEqual(favoritesList[1], todayIsGreat);
            Assert.AreEqual(favoritesList[2], institution);
/* disabled: for some reason the test code for the favorites drag/drop doesn't work anymore
            Actions mouseActions = new Actions(WebBrowser);
            mouseActions.ClickAndHold(institution)   // move 'institution' to the top
                        .MoveToElement(dadsStory)
                        .Release()
                        .Perform();

            // verify new order of the favorite items
            IWebElement[] newFavoritesList = dadsStory.FindElement(By.XPath("./.."))
                                                      .FindElements(By.ClassName("favorite-item"))
                                                      .ToArray();
            Assert.AreEqual(newFavoritesList[0], institution);
            Assert.AreEqual(newFavoritesList[1], dadsStory);
            Assert.AreEqual(newFavoritesList[2], todayIsGreat);

            // split last favorite item from the other 2
            IWebElement todayCutter = todayIsGreat.FindElement(By.ClassName("favorite-items-cutter"));
            todayCutter.Click();

            // verify new order of the favorite items
            IWebElement[] splittedFavoritesList = dadsStory.FindElement(By.XPath("./.."))
                                                           .FindElements(By.ClassName("favorite-item"))
                                                           .ToArray();
            Assert.AreEqual(splittedFavoritesList[0], institution);
            Assert.AreEqual(splittedFavoritesList[1], dadsStory);
            Assert.AreEqual(splittedFavoritesList[2].GetAttribute("class"), "favorite-item splitter");
            Assert.AreEqual(splittedFavoritesList[3], todayIsGreat);

            // unsplit last favorite item
            IWebElement todayCombiner = splittedFavoritesList[2].FindElement(By.ClassName("combine-favorite-items"));
            todayCombiner.Click();

            // verify new order of the favorite items
            IWebElement[] combinedFavoritesList = dadsStory.FindElement(By.XPath("./.."))
                                                           .FindElements(By.ClassName("favorite-item"))
                                                           .ToArray();
            Assert.AreEqual(combinedFavoritesList[0], institution);
            Assert.AreEqual(combinedFavoritesList[1], dadsStory);
            Assert.AreEqual(combinedFavoritesList[2], todayIsGreat);
*/
        }


        [TestMethod]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        [DataRow(typeof(FirefoxConfig), "")]
        [DataRow(typeof(FirefoxConfig), "phabrico")]
        public void CreateNewSubdocumentByMeansOfNewLink(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);
            Logon();

            // click on 'Phriction' in the menu navigator
            IWebElement navigatorPhriction = WebBrowser.FindElement(By.LinkText("Phriction"));
            navigatorPhriction.Click();

            // wait a while
            WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

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
            wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

            // edit content
            IWebElement textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.End);
            textarea.SendKeys("\n\n[[ ./new-link | This is a new link ]]\n");

            // click Save button
            IWebElement btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

            // get newly created anchor link
            IWebElement anchor = WebBrowser.FindElement(By.LinkText("This is a new link"));
            Assert.IsTrue(anchor.GetAttribute("href").EndsWith(":" + HttpServer.TcpPortNr + Http.Server.RootPath + "w/daddy/new-link?title=This%20is%20a%20new%20link"));

            // click on anchor
            anchor.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

            // validate if Phriction was opened
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            phrictionDocumentTitle = phrictionDocument.Text.Split('\r', '\n')[0];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Page Not Found"), "Unable to open Phriction");

            // click on Create this Document button
            IWebElement btnCreateThisDocument = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Create this Document')]"));
            btnCreateThisDocument.Click();
            wait.Until(condition => condition.FindElements(By.Id("title")).Any());

            // enter data for first Phriction document
            IWebElement inputTitle = WebBrowser.FindElement(By.Id("title"));
            Assert.IsTrue(inputTitle.GetAttribute("value").Equals("This is a new link"), "Title of new document not correctly set");

            // Enter some text
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys("Bird is the word");

            // click save button
            btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();
            wait.Until(condition => condition.FindElements(By.ClassName("phriction")).Any());

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

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