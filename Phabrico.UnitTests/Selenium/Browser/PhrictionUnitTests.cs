using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Phabrico.Miscellaneous;
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
            foreach (Phabricator.Data.Phriction phrictionDocumentToBeRmoved in phrictionStorage.Get(Database, Language.NotApplicable).ToList())
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
        [DataRow(typeof(FirefoxConfig), "")]
        [DataRow(typeof(FirefoxConfig), "phabrico")]
        public void OpenPhrictionAndTranslate(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);

            // create a translated copy for all master wiki documents
            Storage.Phriction phrictionStorage = new Storage.Phriction();
            Storage.Content contentTranslationStorage = new Storage.Content(Database);
            foreach (Phabricator.Data.Phriction masterDocument in phrictionStorage.Get(Database, Language.NotApplicable).ToArray())
            {
                contentTranslationStorage.AddTranslation(masterDocument.Token, "nl", masterDocument.Name, masterDocument.Content);
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

            // verify if we are watching the master document
            IWebElement edit = WebBrowser.FindElement(By.LinkText("Edit Document"));

            // open user menu
            IWebElement userMenu = WebBrowser.FindElement(By.ClassName("user-menu"));
            userMenu.Click();

            // click 'Change language'
            IWebElement mnuChangeLanguage = userMenu.FindElement(By.PartialLinkText("Change language"))
                                                    .FindElement(By.TagName("span"));  // otherwise firefox webdriver won't work
            mnuChangeLanguage.Click();

            // change language to Dutch
            IWebElement language = WebBrowser.FindElement(By.Id("newLanguage"));
            language.Click();
            language.FindElements(By.TagName("option"))
                    .Single(option => option.Text == " Nederlands")
                    .Click();
            language.Click();

            // verify new language selection
            language = WebBrowser.FindElement(By.Id("newLanguage"));
            Assert.IsTrue( language.FindElements(By.TagName("option"))
                                   .Single(option => option.Text == " Nederlands")
                                   .Selected
                         );
            language.SendKeys(Keys.Enter);

            // click 'Change language'
            IWebElement btnChangeLanguage = WebBrowser.FindElement(By.XPath("//button[contains(text(), 'Change language')]"));
            btnChangeLanguage.Click();
            Thread.Sleep(500);  // wait a while to make sure the redirect call is finished

            // verify new language change
            IWebElement search = WebBrowser.FindElement(By.Id("searchPhabrico"));
            Assert.AreEqual(search.GetAttribute("placeholder"), "Zoeken");

            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");

            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // verify if we are watching the translated document
            edit = WebBrowser.FindElement(By.LinkText("Vertaling bewerken"));

            // click on Edit
            edit.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

            // edit title
            IWebElement title = WebBrowser.FindElement(By.Id("title"));
            title.SendKeys(OpenQA.Selenium.Keys.Control + "A");
            title.SendKeys("Verhaal van mijn leven");

            // edit content
            IWebElement textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.Control + "A");
            textarea.SendKeys("Lang geleden las ik dit verhaal steeds weer opnieuw");

            // click Save button
            IWebElement btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

            // validate if modifications were stored
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            phrictionDocumentTitle = phrictionDocument.Text.Replace("\r", "").Split('\n')[0];
            string phrictionDocumentContent = phrictionDocument.Text.Replace("\r", "").Split('\n')[1];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Verhaal van mijn leven"), "Title couldn't be saved");
            Assert.IsTrue(phrictionDocumentContent.Equals("Lang geleden las ik dit verhaal steeds weer opnieuw"), "Modifications couldn't be saved");

            // validate if unreviewed translation icon is shown before title
            IWebElement documentState = WebBrowser.FindElement(By.CssSelector("#documentState.translated.unreviewed"));

            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");

            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // approve translation
            IWebElement approveTranslation = WebBrowser.FindElement(By.LinkText("Vertaling goedkeuren"));
            approveTranslation.Click();

            // click on Ja button  (=Yes button)
            IWebElement confirmApproveTranslation = WebBrowser.FindElement(By.XPath("//a[text()='Ja']"));
            confirmApproveTranslation.Click();
            Thread.Sleep(500);  // wait a while to make sure the javascript code has been finished

            // validate if modifications were stored
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            phrictionDocumentTitle = phrictionDocument.Text.Replace("\r", "").Split('\n')[0];
            phrictionDocumentContent = phrictionDocument.Text.Replace("\r", "").Split('\n')[1];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Verhaal van mijn leven"), "Title couldn't be saved");
            Assert.IsTrue(phrictionDocumentContent.Equals("Lang geleden las ik dit verhaal steeds weer opnieuw"), "Modifications couldn't be saved");

            // validate if no unreviewed translation icon is shown before title
            documentState = WebBrowser.FindElement(By.CssSelector("#documentState.translated"));
            Assert.IsFalse(documentState.GetDomAttribute("class").Contains("unreviewed"), "Translation is still marked as 'unreviewed'");

            // open user menu
            userMenu = WebBrowser.FindElement(By.ClassName("user-menu"));
            userMenu.Click();

            // click 'Change language'
            mnuChangeLanguage = userMenu.FindElement(By.PartialLinkText("Taal wijzigen"))
                                        .FindElement(By.TagName("span"));  // otherwise firefox webdriver won't work
            mnuChangeLanguage.Click();

            // change language to Dutch
            language = WebBrowser.FindElement(By.Id("newLanguage"));
            language.Click();
            language.FindElements(By.TagName("option"))
                    .Single(option => option.Text == " English")
                    .Click();
            language.Click();

            // verify new language selection
            language = WebBrowser.FindElement(By.Id("newLanguage"));
            Assert.IsTrue( language.FindElements(By.TagName("option"))
                                   .Single(option => option.Text == " English")
                                   .Selected
                         );
            language.SendKeys(Keys.Enter);

            // click 'Change language'
            btnChangeLanguage = WebBrowser.FindElement(By.XPath("//button[contains(text(), 'Taal wijzigen')]"));
            btnChangeLanguage.Click();
            Thread.Sleep(500);  // wait a while to make sure the redirect call is finished

            // verify new language change
            search = WebBrowser.FindElement(By.Id("searchPhabrico"));
            Assert.AreEqual(search.GetAttribute("placeholder"), "Search");

            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");
            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // validate if original master content is shown
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            phrictionDocumentTitle = phrictionDocument.Text.Replace("\r", "").Split('\n')[0];
            phrictionDocumentContent = phrictionDocument.Text.Replace("\r", "").Split('\n')[1];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Story of my life"), "Title of master document is not shown");
            Assert.IsTrue(phrictionDocumentContent.Equals("Once upon a time, I was reading this story over and over again"), "Content of master document is not shown");

            // open user menu
            userMenu = WebBrowser.FindElement(By.ClassName("user-menu"));
            userMenu.Click();

            // click 'Change language'
            mnuChangeLanguage = userMenu.FindElement(By.PartialLinkText("Change language"))
                                        .FindElement(By.TagName("span"));  // otherwise firefox webdriver won't work
            mnuChangeLanguage.Click();

            // change language to Dutch
            language = WebBrowser.FindElement(By.Id("newLanguage"));
            language.Click();
            language.FindElements(By.TagName("option"))
                    .Single(option => option.Text == " Nederlands")
                    .Click();
            language.Click();

            // verify new language selection
            language = WebBrowser.FindElement(By.Id("newLanguage"));
            Assert.IsTrue( language.FindElements(By.TagName("option"))
                                   .Single(option => option.Text == " Nederlands")
                                   .Selected
                         );
            language.SendKeys(Keys.Enter);

            // click 'Change language'
            btnChangeLanguage = WebBrowser.FindElement(By.XPath("//button[contains(text(), 'Change language')]"));
            btnChangeLanguage.Click();
            Thread.Sleep(500);  // wait a while to make sure the redirect call is finished

            // verify new language change
            search = WebBrowser.FindElement(By.Id("searchPhabrico"));
            Assert.AreEqual(search.GetAttribute("placeholder"), "Zoeken");

            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");
            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // validate if translated content is shown
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            phrictionDocumentTitle = phrictionDocument.Text.Replace("\r", "").Split('\n')[0];
            phrictionDocumentContent = phrictionDocument.Text.Replace("\r", "").Split('\n')[1];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Verhaal van mijn leven"), "Translated title is not shown");
            Assert.IsTrue(phrictionDocumentContent.Equals("Lang geleden las ik dit verhaal steeds weer opnieuw"), "Translated content is not shown");

            // validate if no unreviewed translation icon is shown before title
            documentState = WebBrowser.FindElement(By.CssSelector("#documentState.translated"));
            Assert.IsFalse(documentState.GetDomAttribute("class").Contains("unreviewed"), "Translation is still marked as 'unreviewed'");

            // get list of unreviewed translations
            IWebElement btnUnreviewedTranslations = WebBrowser.FindElement(By.PartialLinkText("Niet gereviseerde vertalingen"));
            btnUnreviewedTranslations.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.CssSelector("#tblUnreviewedTranslations tbody tr")).Any());
            
            // validate if we have 2 unreviewed translations left
            IWebElement[] unreviewedTranslations = WebBrowser.FindElements(By.CssSelector("#tblUnreviewedTranslations tbody tr")).ToArray();
            Assert.IsTrue(unreviewedTranslations.Length == 3);
            Assert.IsTrue(unreviewedTranslations.Any(unreviewedTranslation => unreviewedTranslation.FindElement(By.CssSelector(".originalTitle")).Text.Equals("Diagrams")));
            Assert.IsTrue(unreviewedTranslations.Any(unreviewedTranslation => unreviewedTranslation.FindElement(By.CssSelector(".originalTitle")).Text.Equals("Story of my dad's life")));
            Assert.IsTrue(unreviewedTranslations.Any(unreviewedTranslation => unreviewedTranslation.FindElement(By.CssSelector(".originalTitle")).Text.Equals("Story of my grandfather's life")));

            // browse to unreviewed translation
            unreviewedTranslations.FirstOrDefault(unreviewedTranslation => unreviewedTranslation.FindElement(By.CssSelector(".originalTitle")).Text.Equals("Story of my dad's life"))
                                  .FindElement(By.CssSelector("td.title a"))
                                  .Click();

            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");
            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // verify if we are watching the translated document
            edit = WebBrowser.FindElement(By.LinkText("Vertaling bewerken"));

            // click on Edit
            edit.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

            // edit title
            title = WebBrowser.FindElement(By.Id("title"));
            title.SendKeys(OpenQA.Selenium.Keys.Control + "A");
            title.SendKeys("Het levensverhaal van mijn vader");

            // edit content
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.Control + "A");
            textarea.SendKeys("Vroeger las ik het verhaal van mijn vader steeds weer opnieuw");

            // click Save button
            btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

            // validate if modifications were stored
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            phrictionDocumentTitle = phrictionDocument.Text.Replace("\r", "").Split('\n')[0];
            phrictionDocumentContent = phrictionDocument.Text.Replace("\r", "").Split('\n')[1];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Het levensverhaal van mijn vader"), "Title couldn't be saved");
            Assert.IsTrue(phrictionDocumentContent.Equals("Vroeger las ik het verhaal van mijn vader steeds weer opnieuw"), "Modifications couldn't be saved");

            // validate if unreviewed translation icon is shown before title
            documentState = WebBrowser.FindElement(By.CssSelector("#documentState.translated.unreviewed"));

            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");

            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // approve translation
            approveTranslation = WebBrowser.FindElement(By.LinkText("Vertaling goedkeuren"));
            approveTranslation.Click();

            // click on Ja button  (=Yes button)
            confirmApproveTranslation = WebBrowser.FindElement(By.XPath("//a[text()='Ja']"));
            confirmApproveTranslation.Click();
            Thread.Sleep(500);  // wait a while to make sure the javascript code has been finished

            // validate if modifications were stored
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            phrictionDocumentTitle = phrictionDocument.Text.Replace("\r", "").Split('\n')[0];
            phrictionDocumentContent = phrictionDocument.Text.Replace("\r", "").Split('\n')[1];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Het levensverhaal van mijn vader"), "Title couldn't be saved");
            Assert.IsTrue(phrictionDocumentContent.Equals("Vroeger las ik het verhaal van mijn vader steeds weer opnieuw"), "Modifications couldn't be saved");

            // validate if no unreviewed translation icon is shown before title
            documentState = WebBrowser.FindElement(By.CssSelector("#documentState.translated"));
            Assert.IsFalse(documentState.GetDomAttribute("class").Contains("unreviewed"), "Translation is still marked as 'unreviewed'");

            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");
            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // get list of unreviewed translations
            btnUnreviewedTranslations = WebBrowser.FindElement(By.PartialLinkText("Niet gereviseerde vertalingen"));
            btnUnreviewedTranslations.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.CssSelector("#tblUnreviewedTranslations tbody tr")).Any());
            
            // validate if we have 2 unreviewed translations left
            unreviewedTranslations = WebBrowser.FindElements(By.CssSelector("#tblUnreviewedTranslations tbody tr")).ToArray();
            Assert.IsTrue(unreviewedTranslations.Length == 2);
            Assert.IsTrue(unreviewedTranslations.Any(unreviewedTranslation => unreviewedTranslation.FindElement(By.CssSelector(".originalTitle")).Text.Equals("Diagrams")));
            Assert.IsTrue(unreviewedTranslations.Any(unreviewedTranslation => unreviewedTranslation.FindElement(By.CssSelector(".originalTitle")).Text.Equals("Story of my grandfather's life")));

            // browse to unreviewed translation
            unreviewedTranslations.FirstOrDefault(unreviewedTranslation => unreviewedTranslation.FindElement(By.CssSelector(".originalTitle")).Text.Equals("Story of my grandfather's life"))
                                  .FindElement(By.CssSelector("td.title a"))
                                  .Click();

            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");
            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // verify if we are watching the translated document
            edit = WebBrowser.FindElement(By.LinkText("Vertaling bewerken"));

            // click on Edit
            edit.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

            // edit title
            title = WebBrowser.FindElement(By.Id("title"));
            title.SendKeys(OpenQA.Selenium.Keys.Control + "A");
            title.SendKeys("Het levensverhaal van mijn grootvader");

            // edit content
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.Control + "A");
            textarea.SendKeys("Vroeger las ik het verhaal van mijn grootvader steeds weer opnieuw");

            // click Save button
            btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

            // validate if modifications were stored
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            phrictionDocumentTitle = phrictionDocument.Text.Replace("\r", "").Split('\n')[0];
            phrictionDocumentContent = phrictionDocument.Text.Replace("\r", "").Split('\n')[1];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Het levensverhaal van mijn grootvader"), "Title couldn't be saved");
            Assert.IsTrue(phrictionDocumentContent.Equals("Vroeger las ik het verhaal van mijn grootvader steeds weer opnieuw"), "Modifications couldn't be saved");

            // validate if unreviewed translation icon is shown before title
            documentState = WebBrowser.FindElement(By.CssSelector("#documentState.translated.unreviewed"));

            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");

            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // approve translation
            approveTranslation = WebBrowser.FindElement(By.LinkText("Vertaling goedkeuren"));
            approveTranslation.Click();

            // click on Ja button  (=Yes button)
            confirmApproveTranslation = WebBrowser.FindElement(By.XPath("//a[text()='Ja']"));
            confirmApproveTranslation.Click();
            Thread.Sleep(500);  // wait a while to make sure the javascript code has been finished

            // validate if modifications were stored
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            phrictionDocumentTitle = phrictionDocument.Text.Replace("\r", "").Split('\n')[0];
            phrictionDocumentContent = phrictionDocument.Text.Replace("\r", "").Split('\n')[1];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Het levensverhaal van mijn grootvader"), "Title couldn't be saved");
            Assert.IsTrue(phrictionDocumentContent.Equals("Vroeger las ik het verhaal van mijn grootvader steeds weer opnieuw"), "Modifications couldn't be saved");

            // validate if no unreviewed translation icon is shown before title
            documentState = WebBrowser.FindElement(By.CssSelector("#documentState.translated"));
            Assert.IsFalse(documentState.GetDomAttribute("class").Contains("unreviewed"), "Translation is still marked as 'unreviewed'");


            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");
            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // get list of unreviewed translations
            btnUnreviewedTranslations = WebBrowser.FindElement(By.PartialLinkText("Niet gereviseerde vertalingen"));
            btnUnreviewedTranslations.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.CssSelector("#tblUnreviewedTranslations tbody tr")).Any());

            // validate if we have 2 unreviewed translations left
            unreviewedTranslations = WebBrowser.FindElements(By.CssSelector("#tblUnreviewedTranslations tbody tr")).ToArray();
            Assert.IsTrue(unreviewedTranslations.Length == 1);
            Assert.IsTrue(unreviewedTranslations.Any(unreviewedTranslation => unreviewedTranslation.FindElement(By.CssSelector(".originalTitle")).Text.Equals("Diagrams")));

            // browse to unreviewed translation
            unreviewedTranslations.FirstOrDefault(unreviewedTranslation => unreviewedTranslation.FindElement(By.CssSelector(".originalTitle")).Text.Equals("Diagrams"))
                                  .FindElement(By.CssSelector("td.title a"))
                                  .Click();

            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");
            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // verify if we are watching the translated document
            edit = WebBrowser.FindElement(By.LinkText("Vertaling bewerken"));

            // click on Edit
            edit.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

            // edit title
            title = WebBrowser.FindElement(By.Id("title"));
            title.SendKeys(OpenQA.Selenium.Keys.Control + "A");
            title.SendKeys("Diagrammen");

            // edit content
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.Control + "A");
            textarea.SendKeys("Dit is een diagram: {F1235}");

            // click Save button
            btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

            // validate if modifications were stored
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            phrictionDocumentTitle = phrictionDocument.Text.Replace("\r", "").Split('\n')[0];
            phrictionDocumentContent = phrictionDocument.Text.Replace("\r", "").Split('\n')[1];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Diagrammen"), "Title couldn't be saved");
            Assert.IsTrue(phrictionDocumentContent.StartsWith("Dit is een diagram:"), "Modifications couldn't be saved");

            // validate if unreviewed translation icon is shown before title
            documentState = WebBrowser.FindElement(By.CssSelector("#documentState.translated.unreviewed"));

            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");

            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // approve translation
            approveTranslation = WebBrowser.FindElement(By.LinkText("Vertaling goedkeuren"));
            approveTranslation.Click();

            // click on Ja button  (=Yes button)
            confirmApproveTranslation = WebBrowser.FindElement(By.XPath("//a[text()='Ja']"));
            confirmApproveTranslation.Click();
            Thread.Sleep(500);  // wait a while to make sure the javascript code has been finished

            // validate if modifications were stored
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            phrictionDocumentTitle = phrictionDocument.Text.Replace("\r", "").Split('\n')[0];
            phrictionDocumentContent = phrictionDocument.Text.Replace("\r", "").Split('\n')[1];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Diagrammen"), "Title couldn't be saved");
            Assert.IsTrue(phrictionDocumentContent.StartsWith("Dit is een diagram:"), "Modifications couldn't be saved");


            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");
            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            Assert.IsFalse(WebBrowser.FindElements(By.PartialLinkText("Niet gereviseerde vertalingen")).Any(), "There are still unreviewed translations");
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
            Assert.AreEqual("Story of my dad's life > Today is the greatest day I've ever known", navigationCrumbs);


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
            Assert.AreEqual("Story of my dad's life > Today is the greatest day I've ever known", navigationCrumbs);

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
            Assert.AreEqual("Story of my dad's life > Today is the greatest day I've ever known > After five years in the institution...", navigationCrumbs);


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
            Assert.AreEqual("Story of my dad's life > Today is the greatest day I've ever known > After five years in the institution...", navigationCrumbs);

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
            Assert.AreEqual("Story of my dad's life > This is a new link", navigationCrumbs);
        }


        [TestMethod]
        // [DataRow(typeof(ChromeConfig), "")]          // CTRL-V image is not working in Chrome
        // [DataRow(typeof(ChromeConfig), "phabrico")]  // CTRL-V image is not working in Chrome
        // [DataRow(typeof(EdgeConfig), "")]            // CTRL-V image is not working in Edge
        // [DataRow(typeof(EdgeConfig), "phabrico")]    // CTRL-V image is not working in Edge
        [DataRow(typeof(FirefoxConfig), "")]
        [DataRow(typeof(FirefoxConfig), "phabrico")]
        public void OpenPhrictionAndReuseFileObjects(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);
            Logon();

            // draw a red rectangle in the background and copy it to the clipboard
            using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(100, 100))
            {
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap))
                {
                    using (System.Drawing.SolidBrush brush = new System.Drawing.SolidBrush(System.Drawing.Color.Red))
                    {
                        graphics.FillRectangle(brush, new System.Drawing.Rectangle(0, 0, 100, 100));
                    }
                }
                System.Windows.Forms.Clipboard.SetImage(bitmap);
            }

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
            textarea.SendKeys(OpenQA.Selenium.Keys.Control + "A");
            textarea.SendKeys(OpenQA.Selenium.Keys.Control + "V");

            // wait a while until javascript processing is finished
            Thread.Sleep(1000);

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(1));
            wait.Until(condition => condition.FindElements(By.CssSelector("#right img")).Any());

            // click Save button
            IWebElement btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

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

            // copy content to clipboard
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.Control + "A");
            textarea.SendKeys(OpenQA.Selenium.Keys.Control + "C");
            Thread.Sleep(250);

            // click Cancel button
            IWebElement btnCancel = WebBrowser.FindElement(By.Id("btnCancel"));
            btnCancel.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

             // navigate to "Story of my dad's life" document
            IWebElement linkStoryDadsLife = WebBrowser.FindElement(By.XPath("//*[contains(text(), \"Story of my dad's life\")]"));
            linkStoryDadsLife.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

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

            // paste content from clipboard
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.Control + "A");
            textarea.SendKeys(OpenQA.Selenium.Keys.Control + "V");

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.CssSelector("#right img")).Any());

            // click Save button
            btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

             // navigate to root Phriction document
            IWebElement linkPhriction = WebBrowser.FindElement(By.XPath("//a[@href='w/']"));
            linkPhriction.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

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

            // overwrite content
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.Control + "A");
            textarea.SendKeys("The content was completely overwritten");

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.CssSelector("#right img")).Any() == false);

            // click Save button
            btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

             // navigate to "Story of my dad's life" document
            linkStoryDadsLife = WebBrowser.FindElement(By.XPath("//*[contains(text(), \"Story of my dad's life\")]"));
            linkStoryDadsLife.Click();

            // wait a while and make sure image is still visible
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.CssSelector(".phui-document img")).Any());
        }
    }
}