using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using Phabrico.ContentTranslation.Engines;
using Phabrico.Miscellaneous;
using Phabrico.UnitTests.Synchronization;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using WebDriverManager.DriverConfigs.Impl;

namespace Phabrico.UnitTests.Selenium.Browser
{
    [TestClass]
    public class PluginUnitTests : BrowserUnitTest
    {
        [TestMethod]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        // [DataRow(typeof(FirefoxConfig), "")]          // disabled because of Windows Authentication
        // [DataRow(typeof(FirefoxConfig), "phabrico")]  // disabled because of Windows Authentication
        public void Gitanos(Type browser, string httpRootPath)
        {
            try
            {
                Initialize(browser, httpRootPath);
                Logon();

                // ## Step 1: prepare "remote" git repository ########################################################################
                // (re)create directory for "remote" git repository
                string gitanosRemoteRepository = DownloadDirectory + "\\GitanosRemote";
                if (System.IO.Directory.Exists(gitanosRemoteRepository))
                {
                    System.IO.DirectoryInfo directoryInfo = new System.IO.DirectoryInfo(gitanosRemoteRepository);
                    foreach (System.IO.FileInfo file in directoryInfo.EnumerateFiles("*.*", System.IO.SearchOption.AllDirectories).ToArray())
                    {
                        file.Attributes = System.IO.FileAttributes.Archive;
                        file.Delete();
                    }

                    System.IO.Directory.Delete(gitanosRemoteRepository, true);
                }
                System.IO.Directory.CreateDirectory(gitanosRemoteRepository);

                // initialize git repo
                ExecuteCMD(gitanosRemoteRepository, "git init");
                System.IO.File.WriteAllText(gitanosRemoteRepository + "\\dummy.txt", "dummy file");
                ExecuteCMD(gitanosRemoteRepository, "git add dummy.txt");
                ExecuteCMD(gitanosRemoteRepository, "git commit -m \"inital version\"");

                // ## Step 2: prepare "local" git repository ########################################################################
                // (re)create directory where git repositories should be stored in
                string gitanosRootDirectory = DownloadDirectory + "\\Gitanos";
                if (System.IO.Directory.Exists(gitanosRootDirectory))
                {
                    System.IO.DirectoryInfo directoryInfo = new System.IO.DirectoryInfo(gitanosRootDirectory);
                    directoryInfo.Delete(true);
                }
                System.IO.Directory.CreateDirectory(gitanosRootDirectory);

                // clone git repo
                string remoteUrl = string.Format("file:///{0}", gitanosRemoteRepository.Replace('\\', '/'));
                ExecuteCMD(gitanosRootDirectory, "git clone " + remoteUrl);


                // ## Step 3: start testing #########################################################################################
                // click on 'Config' in the menu navigator
                IWebElement navigatorConfig = WebBrowser.FindElement(By.PartialLinkText("Config"));
                navigatorConfig.Click();

                // wait a while
                WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElements(By.Id("autoLogon")).Any());

                // hide first-time help
                IWebElement overlay = WebBrowser.FindElement(By.ClassName("overlay"));
                overlay.Click();

                AssertNoJavascriptErrors();

                // click on 'Gitanos' tab
                IWebElement tabGitanos = WebBrowser.FindElement(By.XPath("//button[contains(text(), 'Gitanos')]"));
                tabGitanos.Click();

                // click on 'Add root directory'
                IWebElement btnAddRootDirectory = WebBrowser.FindElement(By.Id("btnGitanosAddRootDirectory"));
                btnAddRootDirectory.Click();
                Thread.Sleep(1000);  // wait a while to make sure all the javascript functionality has been finished

                AssertNoJavascriptErrors();

                // enter new 'Gitanos root directory'
                Actions action = new Actions(WebBrowser);
                action.SendKeys(gitanosRootDirectory + Keys.Enter).Perform();

                // click on logo to go back to the homepage
                IWebElement logo = WebBrowser.FindElement(By.XPath("//a[contains(@href, '')]"));
                logo.Click();

                // wait a while to make sure the save-config AJAX call is finished
                Thread.Sleep(1000);

                AssertNoJavascriptErrors();

                // Open 'Gitanos' screen
                IWebElement navigatorGitanos = WebBrowser.FindElement(By.PartialLinkText("Gitanos"));
                navigatorGitanos.Click();

                // wait until Gitanos screen is loaded AND DirectoryMonitor is finished
                Thread.Sleep(3000);

                // verify content of Gitanos screen
                Assert.IsFalse(WebBrowser.FindElements(By.XPath("//a[contains(text(), 'gitRepository')]")).Any(elem => elem.Displayed));

                AssertNoJavascriptErrors();

                // click on 'Show clean repositories'
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.XPath("//button[contains(text(), 'Show clean repositories')]")).Any(elem => elem.Displayed));
                IWebElement btnShowCleanRepositories = WebBrowser.FindElement(By.XPath("//button[contains(text(), 'Show clean repositories')]"));
                btnShowCleanRepositories.Click();

                // verify new content of Gitanos screen
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.TagName("a"))
                                                 .Any(a => a.Displayed 
                                                        && a.Text.Contains("GitanosRemote")
                                                     )
                          );

                AssertNoJavascriptErrors();

                // click on GitanosRemote
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.TagName("a"))
                                                 .Any(a => a.Displayed 
                                                        && a.Text.Contains("GitanosRemote")
                                                     )
                          );
                IWebElement gitanosRemote = WebBrowser.FindElements(By.TagName("a"))
                                                      .FirstOrDefault(a => a.Displayed 
                                                                        && a.Text.Contains("GitanosRemote")  
                                                                     );
                gitanosRemote.Click();

                // verify that we have a clean repository
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.XPath("//div[contains(text(), 'This git repository is clean')]")).Any(elem => elem.Displayed));

                AssertNoJavascriptErrors();

                // modify dummy file
                System.IO.File.WriteAllText(gitanosRootDirectory + "\\GitanosRemote\\dummy.txt", "dummy file.");

                // Open 'Gitanos' screen again
                navigatorGitanos = WebBrowser.FindElement(By.PartialLinkText("Gitanos"));
                navigatorGitanos.Click();


                // click on 'dummy.txt'
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(60));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.TagName("a"))
                                                 .Any(a => a.Displayed 
                                                        && a.Text.Contains("GitanosRemote")
                                                     )
                          );
                IWebElement gitRepository = WebBrowser.FindElements(By.TagName("a"))
                                                      .FirstOrDefault(a => a.Displayed
                                                                        && a.Text.Contains("GitanosRemote")
                                                                     );
                gitRepository.Click();

                AssertNoJavascriptErrors();

                // click on 'dummy.txt'
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.XPath("//a[contains(text(), 'dummy.txt')]")).Any(elem => elem.Displayed));
                IWebElement dummyFile = WebBrowser.FindElement(By.XPath("//a[contains(text(), 'dummy.txt')]"));
                dummyFile.Click();


                // click on 'Edit' button
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.XPath("//a[contains(text(), 'Edit')]")).Any(elem => elem.Displayed));
                IWebElement btnEdit = WebBrowser.FindElement(By.XPath("//a[contains(text(), 'Edit')]"));
                btnEdit.Click();

                // verify file contents
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(20));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.IgnoreExceptionTypes(typeof(UnhandledAlertException));
                wait.Until(condition =>
                {
                    try
                    {
                        return condition.FindElements(By.ClassName("editor"))
                                        .Any(elem => elem.Displayed && string.IsNullOrWhiteSpace(elem.Text) == false);
                    }
                    catch (UnhandledAlertException unhandledAlertException)
                    {
                        try
                        {
                            WebBrowser.SwitchTo().Alert().Accept();
                        }
                        catch (NoAlertPresentException noAlertPresentException)
                        {
                        }
                        return false;
                    }
                });
                IWebElement editor = WebBrowser.FindElement(By.ClassName("editor"));
                Assert.AreEqual(editor.Text, "dummy file.");

                AssertNoJavascriptErrors();

                // modify file contents
                editor.SendKeys(" (modified)");


                // click on 'Save' button
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.XPath("//a[contains(text(), 'Save')]")).Any(elem => elem.Displayed));
                IWebElement btnSave = WebBrowser.FindElement(By.XPath("//a[contains(text(), 'Save')]"));
                btnSave.Click();

                // wait a while to make sure the save-config AJAX call is finished
                Thread.Sleep(1000);

                // verify if content was modified
                Assert.IsTrue(WebBrowser.FindElements(By.XPath("//td[contains(text(), '+dummy file. (modified)')]")).Any());

                AssertNoJavascriptErrors();

                // click on 'Select' button
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.XPath("//a[contains(text(), 'Select')]")).Any(elem => elem.Displayed));
                IWebElement btnSelect = WebBrowser.FindElement(By.XPath("//a[contains(text(), 'Select')]"));
                btnSelect.Click();


                // click on 'COMMIT' button
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.XPath("//a[contains(text(), 'COMMIT')]")).Any(elem => elem.Displayed));
                IWebElement btnCommit = WebBrowser.FindElement(By.XPath("//a[contains(text(), 'COMMIT')]"));
                btnCommit.Click();

                // Enter commit message
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.Id("txtCommitMessage")).Any(elem => elem.Displayed));
                IWebElement txtCommitMessage = WebBrowser.FindElement(By.Id("txtCommitMessage"));
                txtCommitMessage.SendKeys("My first commit");

                // click on 'COMMIT' button
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.XPath("//button[contains(text(), 'COMMIT')]")).Any(elem => elem.Displayed));
                IWebElement btnCommitMessageConfirm = WebBrowser.FindElement(By.XPath("//button[contains(text(), 'COMMIT')]"));
                btnCommitMessageConfirm.Click();

                AssertNoJavascriptErrors();

                // verify content of unpushed messages
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.XPath("//td[contains(text(), 'My first commit')]")).Any(elem => elem.Displayed));

                // click on 'PUSH' button
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.XPath("//a[contains(text(), 'PUSH')]")).Any(elem => elem.Displayed));
                IWebElement btnPush = WebBrowser.FindElement(By.XPath("//a[contains(text(), 'PUSH')]"));
                btnPush.Click();

                AssertNoJavascriptErrors();

                // verify that an error was shown
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.ClassName("aphront-dialog-view")).Any(elem => elem.Displayed));
                IWebElement dlgGitanosError = WebBrowser.FindElements(By.ClassName("aphront-dialog-view"))
                                                        .Where(dialog => dialog.Displayed
                                                                        && dialog.GetAttribute("class").Split(' ').Contains("modalview")
                                                                )
                                                        .FirstOrDefault();
                IWebElement errorMessage = dlgGitanosError.FindElement(By.XPath("//p[contains(text(), \"local push doesn't (yet) support pushing to non-bare repos.\")]"));
                Assert.IsNotNull(errorMessage);

                AssertNoJavascriptErrors();

                // confirm error
                IWebElement btnOK = dlgGitanosError.FindElement(By.XPath("//a[contains(text(), 'OK')]"));
                btnOK.Click();
                Thread.Sleep(500);


                // undo push
                IWebElement btnUndoPush = WebBrowser.FindElement(By.XPath("//a[contains(text(), 'Undo')]"));
                btnUndoPush.Click();

                // confirm undo push
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.ClassName("aphront-dialog-view")).Any(elem => elem.Displayed));
                IWebElement dlgConfirmUndoPush = WebBrowser.FindElements(By.ClassName("aphront-dialog-view"))
                                                           .Where(dialog => dialog.Displayed
                                                                           && dialog.GetAttribute("class").Split(' ').Contains("modalview")
                                                                   )
                                                           .FirstOrDefault();
                errorMessage = dlgConfirmUndoPush.FindElement(By.XPath("//p[contains(text(), \"local push doesn't (yet) support pushing to non-bare repos.\")]"));
                Assert.IsNotNull(errorMessage);
                IWebElement btnYes = dlgConfirmUndoPush.FindElement(By.XPath("//a[contains(text(), 'Yes')]"));
                btnYes.Click();

                AssertNoJavascriptErrors();

                // verify if 'dummy.txt' is back in the list of unpushed commits
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.XPath("//a[contains(text(), 'dummy.txt')]")).Any(elem => elem.Displayed));

                // undo local commit
                IWebElement btnUndoCommit = WebBrowser.FindElement(By.XPath("//a[contains(text(), 'Undo')]"));
                btnUndoCommit.Click();

                AssertNoJavascriptErrors();

                // confirm undo push
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.ClassName("aphront-dialog-view")).Any(elem => elem.Displayed));
                IWebElement dlgConfirmUndoCommit = WebBrowser.FindElements(By.ClassName("aphront-dialog-view"))
                                                           .Where(dialog => dialog.Displayed
                                                                           && dialog.GetAttribute("class").Split(' ').Contains("modalview")
                                                                   )
                                                           .FirstOrDefault();
                errorMessage = dlgConfirmUndoCommit.FindElement(By.XPath("//p[contains(text(), \"Are you sure you want to discard your local changes for\")]"));
                Assert.IsNotNull(errorMessage);
                btnYes = dlgConfirmUndoCommit.FindElement(By.XPath("//a[contains(text(), 'Yes')]"));
                btnYes.Click();

                AssertNoJavascriptErrors();

                // verify if 'dummy.txt' is back in the list of unpushed commits
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.XPath("//a[contains(text(), 'dummy.txt')]")).Any() == false);
            }
            finally
            {
                Plugin.DirectoryMonitor.Stop();
            }
        }

        [TestMethod]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        [DataRow(typeof(FirefoxConfig), "")]
        [DataRow(typeof(FirefoxConfig), "phabrico")]
        public void PhrictionToPDFExportWithoutTemplateParameters(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);
            Logon();

            // click on 'Phriction' in the menu navigator
            IWebElement navigatorPhriction = WebBrowser.FindElement(By.LinkText("Phriction"));
            navigatorPhriction.Click();

            // wait a while
            WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

            AssertNoJavascriptErrors();

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

            // click on 'Export to PDF'
            IWebElement exportToPDF = WebBrowser.FindElement(By.LinkText("Export to PDF"));
            exportToPDF.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.XPath("//*[contains(text(), \"There are 3 underlying documents. Would you like to export these as well ?\")]")).Any());

            AssertNoJavascriptErrors();

            // confirm exporting all underlying documents
            IWebElement dlgExportToPDF = WebBrowser.FindElements(By.ClassName("aphront-dialog-view"))
                                                   .Where(dialog => dialog.Displayed
                                                                   && dialog.GetAttribute("class").Split(' ').Contains("modalview")
                                                           )
                                                   .FirstOrDefault();
            IWebElement btnYes = dlgExportToPDF.FindElement(By.XPath("//a[contains(text(), 'Yes')]"));
            btnYes.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition =>
            {
                try
                {
                    // verify if file was downloaded
                    string downloadedFile = DownloadDirectory + "\\phriction.pdf";
                    if (System.IO.File.Exists(downloadedFile))
                    {
                        System.IO.FileInfo fileInfo = new System.IO.FileInfo(downloadedFile);
                        if (fileInfo.Length > 0)
                        {
                            // verify file content (first bytes should be "%PDF")
                            byte[] fileHeader = System.IO.File.ReadAllBytes(downloadedFile)
                                                              .Take(4)
                                                              .ToArray();
                            return fileHeader[0] == '%'
                                && fileHeader[1] == 'P'
                                && fileHeader[2] == 'D'
                                && fileHeader[3] == 'F';
                        }
                    }
                }
                catch
                {
                }

                return false;
            });
        }


        [TestMethod]
        // [DataRow(typeof(ChromeConfig), "")]          // disabled because javascript prompt() not working
        // [DataRow(typeof(ChromeConfig), "phabrico")]  // disabled because javascript prompt() not working
        // [DataRow(typeof(EdgeConfig), "")]            // disabled because javascript prompt() not working
        // [DataRow(typeof(EdgeConfig), "phabrico")]    // disabled because javascript prompt() not working
        [DataRow(typeof(FirefoxConfig), "")]
        [DataRow(typeof(FirefoxConfig), "phabrico")]
        public void PhrictionToPDFExportWithTemplateParameters(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);
            Logon();

            // click on 'Config' in the menu navigator
            IWebElement navigatorConfig = WebBrowser.FindElement(By.PartialLinkText("Config"));
            navigatorConfig.Click();

            // wait a while
            WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.Id("autoLogon")).Any());

            // hide first-time help
            IWebElement overlay = WebBrowser.FindElement(By.ClassName("overlay"));
            overlay.Click();

            AssertNoJavascriptErrors();

            // click on 'Export to PDF' tab
            IWebElement tabExportToPDF = WebBrowser.FindElement(By.XPath("//button[contains(text(), 'Export to PDF')]"));
            tabExportToPDF.Click();

            // right click on furst header column
            IWebElement firstHeaderColumn = WebBrowser.FindElement(By.CssSelector("td.headerFooterColumn.headerColumn1"));
            Actions builder = new Actions(WebBrowser);
            builder.ContextClick(firstHeaderColumn).Perform();

            // click on 'Prompt field' menu item
            IWebElement mnuPromptField = WebBrowser.FindElement(By.XPath("//li[contains(text(), 'Prompt field')]"));
            mnuPromptField.Click();

            // fill in prompt dialog and press OK
            IAlert promptDialog = WebBrowser.SwitchTo().Alert();
            promptDialog.SendKeys("My Parameter");
            promptDialog.Accept();

            // click on 'Phriction' in the menu navigator
            IWebElement navigatorPhriction = WebBrowser.FindElement(By.LinkText("Phriction"));
            navigatorPhriction.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

            AssertNoJavascriptErrors();

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

            // click on 'Export to PDF'
            IWebElement exportToPDF = WebBrowser.FindElement(By.LinkText("Export to PDF"));
            exportToPDF.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.Id("input-1")).Any(input => input.Displayed));

            // Enter value for 'My Parameter'
            IWebElement myParameter = WebBrowser.FindElement(By.Id("input-1"));
            myParameter.SendKeys("My fabulous header text");

            // click OK button
            IWebElement btnOK = WebBrowser.FindElement(By.ClassName("preparationParameters"))
                                          .FindElements(By.XPath("//button[text()=\"OK\"]"))
                                          .FirstOrDefault(button => button.Displayed);
            btnOK.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.XPath("//*[contains(text(), \"There are 3 underlying documents. Would you like to export these as well ?\")]")).Any());

            AssertNoJavascriptErrors();

            // confirm exporting all underlying documents
            IWebElement dlgExportToPDF = WebBrowser.FindElements(By.ClassName("aphront-dialog-view"))
                                                   .Where(dialog => dialog.Displayed
                                                                   && dialog.GetAttribute("class").Split(' ').Contains("modalview")
                                                           )
                                                   .FirstOrDefault();
            IWebElement btnYes = dlgExportToPDF.FindElement(By.XPath("//a[contains(text(), 'Yes')]"));
            btnYes.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition =>
            {
                try
                {
                    // verify if file was downloaded
                    string downloadedFile = DownloadDirectory + "\\phriction.pdf";
                    if (System.IO.File.Exists(downloadedFile))
                    {
                        System.IO.FileInfo fileInfo = new System.IO.FileInfo(downloadedFile);
                        if (fileInfo.Length > 0)
                        {
                            // verify file content (first bytes should be "%PDF")
                            byte[] fileHeader = System.IO.File.ReadAllBytes(downloadedFile)
                                                              .Take(4)
                                                              .ToArray();
                            return fileHeader[0] == '%'
                                && fileHeader[1] == 'P'
                                && fileHeader[2] == 'D'
                                && fileHeader[3] == 'F';
                        }
                    }
                }
                catch
                {
                }

                return false;
            });
        }


        [TestMethod]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        // [DataRow(typeof(FirefoxConfig), "")]          // disabled because of WebBrowser.SwitchTo().Alert().Accept();
        // [DataRow(typeof(FirefoxConfig), "phabrico")]  // disabled because of WebBrowser.SwitchTo().Alert().Accept();
        public void PhrictionTranslatorDiagrams(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);

            // read translations.po and convert it to a dictionary (which we'll use in the dummy translator)
            string translated = File.ReadAllText(@"ContentTranslation\translations.po");
            Dictionary<string, string> translations = RegexSafe.Matches(translated, "^msgid +\"([^\"]*)\"\r?\nmsgstr +\"([^\"]*)", RegexOptions.Multiline)
                                                              .OfType<Match>()
                                                              .GroupBy(g => g.Groups[1].Value)
                                                              .Select(g => g.FirstOrDefault())
                                                              .ToDictionary(key => key.Groups[1].Value,
                                                                              value => value.Groups[2].Value
                                                                          );
            DummyTranslationEngine.Translations = translations;

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

            AssertNoJavascriptErrors();

            // open subdocument
            IWebElement diagramDocumentLink = WebBrowser.FindElement(By.LinkText("Diagrams"));
            diagramDocumentLink.Click();

            // if action pane is collapsed -> expand it
            bool actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                                 .GetAttribute("class")
                                                 .Contains("right-collapsed");
                
            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // click on Translate Document
            IWebElement translateDocument = WebBrowser.FindElement(By.LinkText("Translate document"));
            translateDocument.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.Name("translationEngine")).Any());

            AssertNoJavascriptErrors();

            // select Dummy translation engine
            IWebElement translationEngine = WebBrowser.FindElement(By.Name("translationEngine"));
            translationEngine.Click();
            translationEngine.FindElements(By.TagName("option"))
                    .Single(option => option.Text == "Dummy")
                    .Click();
            translationEngine.Click();

            // select target language
            IWebElement targetLanguage = WebBrowser.FindElement(By.Name("targetLanguage"));
            targetLanguage.Click();
            targetLanguage.FindElements(By.TagName("option"))
                    .Single(option => option.Text == "Dutch")
                    .Click();
            targetLanguage.Click();

            // click OK button
            IWebElement btnOK = WebBrowser.FindElement(By.ClassName("preparationParameters"))
                                          .FindElements(By.XPath("//button[text()=\"Translate\"]"))
                                          .FirstOrDefault(button => button.Displayed);
            btnOK.Click();

            // wait a while
            Thread.Sleep(500);

            // wait until "one moment please" is gone
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(10));
            wait.Until(condition => condition.FindElements(By.ClassName("wait")).Any(message => message.Displayed == false));
            
            AssertNoJavascriptErrors();

            // Document is translated: click OK
            btnOK = WebBrowser.FindElement(By.Id("dlgOK"))
                              .FindElements(By.XPath("//a[text()=\"OK\"]"))
                              .FirstOrDefault(button => button.Displayed);
            btnOK.Click();

            AssertNoJavascriptErrors();

            ChangeLanguageFromEnglishToDutch();

            // validate if document is translated
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            string documentContent = phrictionDocument.Text.Replace("\r", "");
            Assert.IsTrue(documentContent.StartsWith("Diagrammen\nDit is een diagram:"), "Document not translated");

            // verify if image is shown
            IWebElement image = WebBrowser.FindElement(By.ClassName("diagram"));
            wait.Until(condition => condition.FindElement(By.ClassName("diagram")).Displayed);
            Assert.AreNotEqual(image.Size.Width, 0);
            Assert.AreNotEqual(image.Size.Height, 0);

            // remember image content for comparison later on
            byte[] originalImageData;
            using (MemoryStream imageStream = new MemoryStream(WebBrowser.GetScreenshot().AsByteArray))
            {
                using (Bitmap screenshot = new Bitmap(imageStream))
                {
                    Rectangle croppedImage = new System.Drawing.Rectangle(image.Location.X, image.Location.Y, image.Size.Width, image.Size.Height);
                    using (Bitmap clonedScreenshot = screenshot.Clone(croppedImage, screenshot.PixelFormat))
                    {
                        using (MemoryStream outputStream = new MemoryStream())
                        {
                            clonedScreenshot.Save(outputStream, ImageFormat.Bmp);
                            outputStream.Position = 0;
                            originalImageData = outputStream.ToArray();
                        }
                    }
                }
            }

            // hover over image
            Actions builder = new Actions(WebBrowser);
            builder.MoveToElement(image).Build().Perform();

            // click on blue diagram button
            IWebElement btnDiagram = WebBrowser.FindElement(By.ClassName("image-locator"))
                                               .FindElements(By.TagName("a"))
                                               .FirstOrDefault();
            btnDiagram.Click();

            // wait until DiagramsNet IFrame content is fully loaded
            wait.Until(condition => condition.FindElements(By.TagName("IFrame")).Any());
            IWebElement diagramsIFrame = WebBrowser.FindElement(By.TagName("IFrame"));
            WebBrowser.SwitchTo().Frame(diagramsIFrame);

            WebDriverWait webDriverWait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(10));
            webDriverWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(WebDriverException));
            webDriverWait.Until(condition => condition.FindElements(By.XPath("//div[text()=\"This is a rectangle\"]")).Any());

            // verify if 'Approve Translation' button is disabled
            IWebElement btnApproveTranslation = WebBrowser.FindElement(By.XPath("//a[text()=\"Vertaling goedkeuren\"]"));
            Assert.AreEqual(btnApproveTranslation.GetCssValue("pointer-events"), "none");

            // double click on 'English'
            IWebElement diagramRectangle = WebBrowser.FindElement(By.XPath("//div[text()=\"This is a rectangle\"]"));
            builder = new Actions(WebBrowser);
            builder.DoubleClick(diagramRectangle).Build().Perform();

            // change 'English' to 'Nederlands'
            IWebElement rectangleText = WebBrowser.SwitchTo().ActiveElement();
            rectangleText.SendKeys(Keys.Control + "A");
            rectangleText.SendKeys("Dit is een rechthoek");

            // click on background, so rectangle is not focused anymore
            IWebElement background = WebBrowser.FindElement(By.ClassName("geDiagramContainer"));
            builder = new Actions(WebBrowser);
            builder.MoveToElement(background).MoveByOffset(100,100).Click().Release().Build().Perform();

            // wait a while, so background javascript can be finished
            Thread.Sleep(500);

            // click on save button
            IWebElement save = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Opslaan')]"));
            save.Click();

            AssertNoJavascriptErrors();

            // wait until green 'Approve Translation' button is enabled
            webDriverWait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(3));
            webDriverWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(WebDriverException));
            webDriverWait.Until(condition => condition.FindElements(By.XPath("//a[text()=\"Vertaling goedkeuren\"]"))
                                                      .Any(btn => btn.GetCssValue("pointer-events").Equals("auto"))
                               );

            // click on exit button
            IWebElement exit = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Afsluiten')]"));
            exit.Click();

            // wait a while , so image can be loaded
            Thread.Sleep(1500);

            AssertNoJavascriptErrors();

            // verify if image content has been changed
            image = WebBrowser.FindElement(By.ClassName("diagram"));
            byte[] imageData;
            using (MemoryStream imageStream = new MemoryStream(WebBrowser.GetScreenshot().AsByteArray))
            {
                using (Bitmap screenshot = new Bitmap(imageStream))
                {
                    Rectangle croppedImage = new System.Drawing.Rectangle(image.Location.X, image.Location.Y, image.Size.Width, image.Size.Height);
                    using (Bitmap clonedScreenshot = screenshot.Clone(croppedImage, screenshot.PixelFormat))
                    {
                        using (MemoryStream outputStream = new MemoryStream())
                        {
                            clonedScreenshot.Save(outputStream, ImageFormat.Bmp);
                            outputStream.Position = 0;
                            imageData = outputStream.ToArray();
                        }
                    }
                }
            }

            Assert.IsFalse(Enumerable.SequenceEqual(imageData, originalImageData), "Diagram image is still the same");

            // change to english
            ChangeLanguageFromDutchToEnglish();

            // verify if image content has been changed
            image = WebBrowser.FindElement(By.ClassName("diagram"));
            using (MemoryStream imageStream = new MemoryStream(WebBrowser.GetScreenshot().AsByteArray))
            {
                using (Bitmap screenshot = new Bitmap(imageStream))
                {
                    Rectangle croppedImage = new System.Drawing.Rectangle(image.Location.X, image.Location.Y, image.Size.Width, image.Size.Height);
                    using (Bitmap clonedScreenshot = screenshot.Clone(croppedImage, screenshot.PixelFormat))
                    {
                        using (MemoryStream outputStream = new MemoryStream())
                        {
                            clonedScreenshot.Save(outputStream, ImageFormat.Bmp);
                            outputStream.Position = 0;
                            imageData = outputStream.ToArray();
                        }
                    }
                }
            }

            Assert.IsTrue(Enumerable.SequenceEqual(imageData, originalImageData), "Diagram image is still the same");

            // go to Phriction root page
            IWebElement rootPhriction = WebBrowser.FindElements(By.XPath("//a"))
                                                  .FirstOrDefault(a => a.Displayed && a.GetAttribute("href").EndsWith("/w/"));
            rootPhriction.Click();

            // validate if Phriction was opened
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            documentContent = phrictionDocument.Text.Replace("\r", "");
            Assert.IsTrue(documentContent.StartsWith("Story of my life"), "Wrong document is shown");

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
            wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

            AssertNoJavascriptErrors();

            // edit content
            IWebElement textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.End);
            textarea.SendKeys(OpenQA.Selenium.Keys.Enter);

            // click on Diagrams button in Remarkup editor toolbar
            IWebElement diagramsButton = WebBrowser.FindElement(By.ClassName("fa-sitemap"));
            diagramsButton.Click();

            // confirm "Leave site?" dialog
            WebBrowser.SwitchTo().Alert().Accept();

            // wait until DiagramsNet IFrame content is fully loaded
            wait.Until(condition => condition.FindElements(By.TagName("IFrame")).Any());
            diagramsIFrame = WebBrowser.FindElement(By.TagName("IFrame"));
            WebBrowser.SwitchTo().Frame(diagramsIFrame);

            webDriverWait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(10));
            webDriverWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(WebDriverException));
            webDriverWait.Until(condition => condition.FindElements(By.XPath("//*[contains(@title, 'Shapes')]")).Any());

            AssertNoJavascriptErrors();

            // show Shapes toolbox if invisible
            IWebElement shapes = null;
            if (WebBrowser.FindElements(By.XPath("//*[contains(text(), 'Shapes')]")).Any(shape => shape.Displayed) == false)
            {
                IWebElement toolbarButtonShapes = WebBrowser.FindElement(By.XPath("//*[contains(@title, 'Shapes')]"));
                toolbarButtonShapes.Click();
            }

            shapes = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Shapes')]"));
            shapes = shapes.FindElement(By.XPath("../../../.."));

            // click on rectangle shape in Shapes toolbox
            IWebElement rectangle = shapes.FindElement(By.ClassName("geItem"));
            rectangle.Click();

            // enter some text in the newly created rectangle
            Actions action = new Actions(WebBrowser);
            action.SendKeys("English").Perform();

            AssertNoJavascriptErrors();

            // click on save button
            save = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Save')]"));
            save.Click();

            // verify if the page is reloaded with the new file name
            wait.Until(condition => condition.FindElements(By.XPath("//*[contains(text(), 'F-2')]")).Any());
            
            // wait until DiagramsNet IFrame content is fully loaded
            wait.Until(condition => condition.FindElements(By.TagName("IFrame")).Any());
            diagramsIFrame = WebBrowser.FindElement(By.TagName("IFrame"));
            WebBrowser.SwitchTo().Frame(diagramsIFrame);

            webDriverWait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(10));
            webDriverWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(WebDriverException));
            webDriverWait.Until(condition => condition.FindElements(By.XPath("//*[contains(@title, 'Shapes')]")).Any());

            AssertNoJavascriptErrors();

            // click on exit button
            exit = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Exit')]"));
            exit.Click();

             // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

            // verify if diagram-reference is added to Phriction's textarea content
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            Assert.AreEqual("Once upon a time, I was reading this story over and over again\n{F-2, size=full}", textarea.GetAttribute("value").Replace("\r", ""));
            wait.Until(condition => condition.FindElements(By.ClassName("diagram")).Any());

            AssertNoJavascriptErrors();

            // verify if image is presented on the right side
            image = WebBrowser.FindElement(By.ClassName("diagram"));
            wait.Until(condition => condition.FindElement(By.ClassName("diagram")).Displayed);
            Assert.AreNotEqual(image.Size.Width, 0);
            Assert.AreNotEqual(image.Size.Height, 0);

            // remember image content for comparison later on
            using (MemoryStream imageStream = new MemoryStream(WebBrowser.GetScreenshot().AsByteArray))
            {
                using (Bitmap screenshot = new Bitmap(imageStream))
                {
                    Rectangle croppedImage = new System.Drawing.Rectangle(image.Location.X, image.Location.Y, image.Size.Width, image.Size.Height);
                    using (Bitmap clonedScreenshot = screenshot.Clone(croppedImage, screenshot.PixelFormat))
                    {
                        using (MemoryStream outputStream = new MemoryStream())
                        {
                            clonedScreenshot.Save(outputStream, ImageFormat.Bmp);
                            outputStream.Position = 0;
                            originalImageData = outputStream.ToArray();
                        }
                    }
                }
            }

            // click Save button
            IWebElement btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();
            Thread.Sleep(500);

            AssertNoJavascriptErrors();

            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");

            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // click on Translate Document
            translateDocument = WebBrowser.FindElement(By.LinkText("Translate document"));
            translateDocument.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.Name("translationEngine")).Any());

            AssertNoJavascriptErrors();

            // select Dummy translation engine
            translationEngine = WebBrowser.FindElement(By.Name("translationEngine"));
            translationEngine.Click();
            translationEngine.FindElements(By.TagName("option"))
                    .Single(option => option.Text == "Dummy")
                    .Click();
            translationEngine.Click();

            // select target language
            targetLanguage = WebBrowser.FindElement(By.Name("targetLanguage"));
            targetLanguage.Click();
            targetLanguage.FindElements(By.TagName("option"))
                    .Single(option => option.Text == "Dutch")
                    .Click();
            targetLanguage.Click();

            // click OK button
            btnOK = WebBrowser.FindElement(By.ClassName("preparationParameters"))
                              .FindElements(By.XPath("//button[text()=\"Translate\"]"))
                              .FirstOrDefault(button => button.Displayed);
            btnOK.Click();

            // wait a while
            Thread.Sleep(500);

            // wait until "one moment please" is gone
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(10));
            wait.Until(condition => condition.FindElements(By.ClassName("wait")).Any(message => message.Displayed == false));

            // do not translate underlying documents: click No
            IWebElement btnNo = WebBrowser.FindElement(By.Id("dlgYesNoCancel"))
                                          .FindElements(By.XPath("//a[text()=\"No\"]"))
                                          .FirstOrDefault(button => button.Displayed);
            btnNo.Click();

            // wait a while
            Thread.Sleep(500);

            // wait until "one moment please" is gone
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(10));
            wait.Until(condition => condition.FindElements(By.ClassName("wait")).Any(message => message.Displayed == false));

            AssertNoJavascriptErrors();

            // Document is translated: click OK
            btnOK = WebBrowser.FindElement(By.Id("dlgOK"))
                              .FindElements(By.XPath("//a[text()=\"OK\"]"))
                              .FirstOrDefault(button => button.Displayed);
            btnOK.Click();

            AssertNoJavascriptErrors();

            ChangeLanguageFromEnglishToDutch();

            // validate if document is translated
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            documentContent = phrictionDocument.Text.Replace("\r", "");
            Assert.IsTrue(documentContent.StartsWith("Mijn levensverhaal\nEr was eens een tijd dat ik dit verhaal steeds opnieuw aan het lezen was"), "Document not translated");

            // verify if original image is shown in translation
            image = WebBrowser.FindElement(By.ClassName("diagram"));
            wait.Until(condition => condition.FindElement(By.ClassName("diagram")).Displayed);
            Assert.AreNotEqual(image.Size.Width, 0);
            Assert.AreNotEqual(image.Size.Height, 0);

            // verify if image content is still the same
            using (MemoryStream imageStream = new MemoryStream(WebBrowser.GetScreenshot().AsByteArray))
            {
                using (Bitmap screenshot = new Bitmap(imageStream))
                {
                    Rectangle croppedImage = new System.Drawing.Rectangle(image.Location.X, image.Location.Y, image.Size.Width, image.Size.Height);
                    using (Bitmap clonedScreenshot = screenshot.Clone(croppedImage, screenshot.PixelFormat))
                    {
                        using (MemoryStream outputStream = new MemoryStream())
                        {
                            clonedScreenshot.Save(outputStream, ImageFormat.Bmp);
                            outputStream.Position = 0;
                            imageData = outputStream.ToArray();
                        }
                    }
                }
            }

            Assert.IsTrue(Enumerable.SequenceEqual(imageData, originalImageData), "Diagram image is different");

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
            edit = WebBrowser.FindElement(By.LinkText("Vertaling bewerken"));
            edit.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

            AssertNoJavascriptErrors();

            // verify edit content
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            Assert.IsTrue(textarea.Text.Replace("\r", "").Equals("Er was eens een tijd dat ik dit verhaal steeds opnieuw aan het lezen was\n{F-3, size=full}"));

            // click Cancel button
            IWebElement btnCancel = WebBrowser.FindElement(By.Id("btnCancel"));
            btnCancel.Click();
            Thread.Sleep(500);

            // hover over image
            image = WebBrowser.FindElement(By.ClassName("diagram"));
            builder = new Actions(WebBrowser);
            builder.MoveToElement(image).Build().Perform();

            // click on blue diagram button
            btnDiagram = WebBrowser.FindElement(By.ClassName("image-locator"))
                                   .FindElements(By.TagName("a"))
                                   .FirstOrDefault();
            btnDiagram.Click();

            // wait until DiagramsNet IFrame content is fully loaded
            wait.Until(condition => condition.FindElements(By.TagName("IFrame")).Any());
            diagramsIFrame = WebBrowser.FindElement(By.TagName("IFrame"));
            WebBrowser.SwitchTo().Frame(diagramsIFrame);

            webDriverWait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(10));
            webDriverWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(WebDriverException));
            webDriverWait.Until(condition => condition.FindElements(By.XPath("//div[text()=\"English\"]")).Any());

            // verify if 'Approve Translation' button is disabled
            btnApproveTranslation = WebBrowser.FindElement(By.XPath("//a[text()=\"Vertaling goedkeuren\"]"));
            Assert.AreEqual(btnApproveTranslation.GetCssValue("pointer-events"), "none");

            // double click on 'English'
            IWebElement englishRectangle = WebBrowser.FindElement(By.XPath("//div[text()=\"English\"]"));
            builder = new Actions(WebBrowser);
            builder.DoubleClick(englishRectangle).Build().Perform();

            // change 'English' to 'Nederlands'
            rectangleText = WebBrowser.SwitchTo().ActiveElement();
            rectangleText.SendKeys(Keys.Control + "A");
            rectangleText.SendKeys("Nederlands");

            // click on background, so rectangle is not focused anymore
            background = WebBrowser.FindElement(By.ClassName("geDiagramContainer"));
            builder = new Actions(WebBrowser);
            builder.MoveToElement(background).MoveByOffset(100,100).Click().Release().Build().Perform();

            // wait a while, so background javascript can be finished
            Thread.Sleep(500);

            // click on save button
            save = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Opslaan')]"));
            save.Click();

            AssertNoJavascriptErrors();

            // wait until green 'Approve Translation' button is enabled
            webDriverWait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(3));
            webDriverWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(WebDriverException));
            webDriverWait.Until(condition => condition.FindElements(By.XPath("//a[text()=\"Vertaling goedkeuren\"]"))
                                                      .Any(btn => btn.GetCssValue("pointer-events").Equals("auto"))
                               );

            // click on exit button
            exit = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Afsluiten')]"));
            exit.Click();

            // wait a while , so image can be loaded
            Thread.Sleep(1500);

            AssertNoJavascriptErrors();

            // verify if image content has been changed
            image = WebBrowser.FindElement(By.ClassName("diagram"));
            using (MemoryStream imageStream = new MemoryStream(WebBrowser.GetScreenshot().AsByteArray))
            {
                using (Bitmap screenshot = new Bitmap(imageStream))
                {
                    Rectangle croppedImage = new System.Drawing.Rectangle(image.Location.X, image.Location.Y, image.Size.Width, image.Size.Height);
                    using (Bitmap clonedScreenshot = screenshot.Clone(croppedImage, screenshot.PixelFormat))
                    {
                        using (MemoryStream outputStream = new MemoryStream())
                        {
                            clonedScreenshot.Save(outputStream, ImageFormat.Bmp);
                            outputStream.Position = 0;
                            imageData = outputStream.ToArray();
                        }
                    }
                }
            }

            Assert.IsFalse(Enumerable.SequenceEqual(imageData, originalImageData), "Diagram image is still the same");

            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");

            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // click on 'Unreviewed translations'
            IWebElement unreviewedTranslations = WebBrowser.FindElement(By.PartialLinkText("Niet gereviseerde vertalingen"));
            unreviewedTranslations.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.LinkText("F-3")).Any());

            // click on diagram link
            IWebElement diagram = WebBrowser.FindElement(By.LinkText("F-3"));
            diagram.Click();

            // wait until DiagramsNet IFrame content is fully loaded
            wait.Until(condition => condition.FindElements(By.TagName("IFrame")).Any());
            diagramsIFrame = WebBrowser.FindElement(By.TagName("IFrame"));
            WebBrowser.SwitchTo().Frame(diagramsIFrame);

            webDriverWait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(10));
            webDriverWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(WebDriverException));
            webDriverWait.Until(condition => condition.FindElements(By.XPath("//div[text()=\"Nederlands\"]")).Any());

            AssertNoJavascriptErrors();

            // verify if 'Approve Translation' button is disabled
            btnApproveTranslation = WebBrowser.FindElement(By.XPath("//a[text()=\"Vertaling goedkeuren\"]"));
            Assert.AreEqual(btnApproveTranslation.GetCssValue("pointer-events"), "auto");

            // click on 'Approve Translation'
            btnApproveTranslation.Click();

            // confirm approval
            WebBrowser.SwitchTo().DefaultContent();
            IWebElement btnYes = WebBrowser.FindElement(By.Id("dlgYesNoCancel"))
                                           .FindElements(By.XPath("//a[text()=\"Ja\"]"))
                                           .FirstOrDefault(button => button.Displayed);
            btnYes.Click();

            // wait a while
            Thread.Sleep(1000);
            
            // wait until DiagramsNet IFrame content is fully loaded
            wait.Until(condition => condition.FindElements(By.TagName("IFrame")).Any());
            diagramsIFrame = WebBrowser.FindElement(By.TagName("IFrame"));
            WebBrowser.SwitchTo().Frame(diagramsIFrame);

            webDriverWait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(10));
            webDriverWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(WebDriverException));
            webDriverWait.Until(condition => condition.FindElements(By.XPath("//div[text()=\"Nederlands\"]")).Any());

            AssertNoJavascriptErrors();

            // verify that 'Approve Translation' button is gone
            btnApproveTranslation = WebBrowser.FindElements(By.XPath("//a[text()=\"Vertaling goedkeuren\"]")).FirstOrDefault();
            Assert.AreEqual(null, btnApproveTranslation);
        }

        [TestMethod]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        // [DataRow(typeof(FirefoxConfig), "")]          // disabled because of WebBrowser.SwitchTo().Alert().Accept();
        // [DataRow(typeof(FirefoxConfig), "phabrico")]  // disabled because of WebBrowser.SwitchTo().Alert().Accept();
        public void PhrictionTranslatorDiagramsUndo(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);

            // read translations.po and convert it to a dictionary (which we'll use in the dummy translator)
            string translated = File.ReadAllText(@"ContentTranslation\translations.po");
            Dictionary<string, string> translations = RegexSafe.Matches(translated, "^msgid +\"([^\"]*)\"\r?\nmsgstr +\"([^\"]*)", RegexOptions.Multiline)
                                                              .OfType<Match>()
                                                              .GroupBy(g => g.Groups[1].Value)
                                                              .Select(g => g.FirstOrDefault())
                                                              .ToDictionary(key => key.Groups[1].Value,
                                                                              value => value.Groups[2].Value
                                                                          );
            DummyTranslationEngine.Translations = translations;

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

            AssertNoJavascriptErrors();

            // open subdocument
            IWebElement diagramDocumentLink = WebBrowser.FindElement(By.LinkText("Diagrams"));
            diagramDocumentLink.Click();

            // if action pane is collapsed -> expand it
            bool actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                                 .GetAttribute("class")
                                                 .Contains("right-collapsed");

            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // click on Translate Document
            IWebElement translateDocument = WebBrowser.FindElement(By.LinkText("Translate document"));
            translateDocument.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.Name("translationEngine")).Any());

            AssertNoJavascriptErrors();

            // select Dummy translation engine
            IWebElement translationEngine = WebBrowser.FindElement(By.Name("translationEngine"));
            translationEngine.Click();
            translationEngine.FindElements(By.TagName("option"))
                    .Single(option => option.Text == "Dummy")
                    .Click();
            translationEngine.Click();

            // select target language
            IWebElement targetLanguage = WebBrowser.FindElement(By.Name("targetLanguage"));
            targetLanguage.Click();
            targetLanguage.FindElements(By.TagName("option"))
                    .Single(option => option.Text == "Dutch")
                    .Click();
            targetLanguage.Click();

            // click OK button
            IWebElement btnOK = WebBrowser.FindElement(By.ClassName("preparationParameters"))
                                          .FindElements(By.XPath("//button[text()=\"Translate\"]"))
                                          .FirstOrDefault(button => button.Displayed);
            btnOK.Click();

            // wait a while
            Thread.Sleep(500);

            // wait until "one moment please" is gone
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(10));
            wait.Until(condition => condition.FindElements(By.ClassName("wait")).Any(message => message.Displayed == false));

            AssertNoJavascriptErrors();

            // Document is translated: click OK
            btnOK = WebBrowser.FindElement(By.Id("dlgOK"))
                              .FindElements(By.XPath("//a[text()=\"OK\"]"))
                              .FirstOrDefault(button => button.Displayed);
            btnOK.Click();

            AssertNoJavascriptErrors();

            ChangeLanguageFromEnglishToDutch();

            // validate if document is translated
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            string documentContent = phrictionDocument.Text.Replace("\r", "");
            Assert.IsTrue(documentContent.StartsWith("Diagrammen\nDit is een diagram:"), "Document not translated");

            // verify if image is shown
            IWebElement image = WebBrowser.FindElement(By.ClassName("diagram"));
            wait.Until(condition => condition.FindElement(By.ClassName("diagram")).Displayed);
            Assert.AreNotEqual(image.Size.Width, 0);
            Assert.AreNotEqual(image.Size.Height, 0);

            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");
            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // click on Edit Translation
            IWebElement edit = WebBrowser.FindElement(By.LinkText("Vertaling bewerken"));
            edit.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

            AssertNoJavascriptErrors();

            // verify content
            IWebElement textarea = WebBrowser.FindElement(By.Id("textarea"));
            Assert.AreEqual("Dit is een diagram: {F-1}", textarea.Text);

            // click on Cancel button in Remarkup editor toolbar
            IWebElement btnCancel = WebBrowser.FindElement(By.Id("btnCancel"));
            btnCancel.Click();
            Thread.Sleep(500);

            // click on logo to go back to the homepage
            IWebElement logo = WebBrowser.FindElement(By.XPath("//a[contains(@href, '')]"));
            logo.Click();

            // wait a while to make sure the save-config AJAX call is finished
            Thread.Sleep(1000);

            AssertNoJavascriptErrors();

            // Open 'Offline changes' screen
            IWebElement navigatorOfflineChanges = WebBrowser.FindElement(By.PartialLinkText("Lokale wijzigingen"));
            navigatorOfflineChanges.Click();

            // wait a while
            Thread.Sleep(1000);

            // verify content of Offline Changes screen
            Assert.IsTrue(WebBrowser.FindElements(By.XPath("//a[contains(text(), 'F-1')]")).Count(elem => elem.Displayed) == 1);

            // click on undo button
            IWebElement btnUndo = WebBrowser.FindElements(By.XPath("//a[contains(text(), 'Ongedaan maken')]")).FirstOrDefault(elem => elem.Displayed);
            btnUndo.Click();

            // confirm Undo
            IWebElement btnYes = WebBrowser.FindElement(By.Id("dlgConfirmUndo"))
                               .FindElements(By.XPath("//button[text()=\"Ja\"]"))
                               .FirstOrDefault(button => button.Displayed);
            btnYes.Click();

            // wait until javascript is finished
            Thread.Sleep(1000);

            // verify content of Offline Changes screen
            Assert.IsTrue(WebBrowser.FindElements(By.XPath("//a[contains(text(), ' F-1')]")).Any(elem => elem.Displayed) == false);


            // click on 'Phriction' in the menu navigator
            navigatorPhriction = WebBrowser.FindElement(By.LinkText("Phriction"));
            navigatorPhriction.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

            // validate if Phriction was opened
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            phrictionDocumentTitle = phrictionDocument.Text.Split('\r', '\n')[0];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Story of my life"), "Unable to open Phriction");

            AssertNoJavascriptErrors();

            // open subdocument
            diagramDocumentLink = WebBrowser.FindElement(By.LinkText("Diagrammen"));
            diagramDocumentLink.Click();


            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");
            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // click on Edit Translation
            edit = WebBrowser.FindElement(By.LinkText("Vertaling bewerken"));
            edit.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

            AssertNoJavascriptErrors();

            // verify if content contains the original (untranslated) diagram
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            Assert.AreEqual("Dit is een diagram: {F1235}", textarea.Text);
        }

        [TestMethod]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        [DataRow(typeof(FirefoxConfig), "")]
        [DataRow(typeof(FirefoxConfig), "phabrico")]
        public void PhrictionTranslatorStageStates(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);

            // read translations.po and convert it to a dictionary (which we'll use in the dummy translator)
            string translated = File.ReadAllText(@"ContentTranslation\translations.po");
            Dictionary<string,string> translations = RegexSafe.Matches(translated, "^msgid +\"([^\"]*)\"\r?\nmsgstr +\"([^\"]*)",  RegexOptions.Multiline)
                                                              .OfType<Match>()
                                                              .GroupBy(g => g.Groups[1].Value)
                                                              .Select(g => g.FirstOrDefault())
                                                              .ToDictionary( key => key.Groups[1].Value, 
                                                                              value => value.Groups[2].Value
                                                                          );
            DummyTranslationEngine.Translations = translations;

            Logon();

            // == Start test 1: master=not staged and translation=not staged ======================================================================================
            ChangeLanguageFromEnglishToDutch();

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

            // click on Translate Document
            IWebElement translateDocument = WebBrowser.FindElement(By.LinkText("Document vertalen"));
            translateDocument.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.Name("translationEngine")).Any());

            // select Dummy translation engine
            IWebElement translationEngine = WebBrowser.FindElement(By.Name("translationEngine"));
            translationEngine.Click();
            translationEngine.FindElements(By.TagName("option"))
                    .Single(option => option.Text == "Dummy")
                    .Click();
            translationEngine.Click();

            // select target language
            IWebElement targetLanguage = WebBrowser.FindElement(By.Name("targetLanguage"));
            targetLanguage.Click();
            targetLanguage.FindElements(By.TagName("option"))
                    .Single(option => option.Text == "Nederlands")
                    .Click();
            targetLanguage.Click();

            // click OK button
            IWebElement btnOK = WebBrowser.FindElement(By.ClassName("preparationParameters"))
                                          .FindElements(By.XPath("//button[text()=\"Translate\"]"))
                                          .FirstOrDefault(button => button.Displayed);
            btnOK.Click();

            // wait a while
            Thread.Sleep(500);

            // wait until "one moment please" is gone
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(10));
            wait.Until(condition => condition.FindElements(By.ClassName("wait")).Any(message => message.Displayed == false));

            // do not translate underlying documents: click No
            IWebElement btnNo = WebBrowser.FindElement(By.Id("dlgYesNoCancel"))
                                          .FindElements(By.XPath("//a[text()=\"Nee\"]"))
                                          .FirstOrDefault(button => button.Displayed);
            btnNo.Click();

            // wait a while
            Thread.Sleep(500);

            // wait until "one moment please" is gone
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(10));
            wait.Until(condition => condition.FindElements(By.ClassName("wait")).Any(message => message.Displayed == false));

            // Document is translated: click OK
            btnOK = WebBrowser.FindElement(By.Id("dlgOK"))
                              .FindElements(By.XPath("//a[text()=\"OK\"]"))
                              .FirstOrDefault(button => button.Displayed);
            btnOK.Click();

            AssertNoJavascriptErrors();

            // validate if document is translated
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            string documentContent = phrictionDocument.Text.Replace("\r", "");
            Assert.IsTrue(documentContent.StartsWith("Mijn levensverhaal\nEr was eens een tijd dat ik dit verhaal steeds opnieuw aan het lezen was"), "Document not translated");

            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");

            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // click on Approve translation
            IWebElement approveTranslation = WebBrowser.FindElement(By.LinkText("Vertaling goedkeuren"));
            approveTranslation.Click();

            // confirm approval
            IWebElement btnYes = WebBrowser.FindElement(By.Id("dlgYesNoCancel"))
                                           .FindElements(By.XPath("//a[text()=\"Ja\"]"))
                                           .FirstOrDefault(button => button.Displayed);
            btnYes.Click();

            // wait a while, so the javascript can refresh the page
            Thread.Sleep(1500);

            AssertNoJavascriptErrors();

            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");

            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // verify that the Approve translation button is one
            approveTranslation = WebBrowser.FindElements(By.LinkText("Vertaling goedkeuren")).FirstOrDefault();
            Assert.IsTrue(approveTranslation == null, "Translation was not approved");

            // == Start test 2: master=staged and translation=not staged ==========================================================================================
            ChangeLanguageFromDutchToEnglish();

            // verify if master document is shown
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            documentContent = phrictionDocument.Text.Replace("\r", "");
            Assert.IsTrue(documentContent.StartsWith("Story of my life\nOnce upon a time, I was reading this story over and over again"), "Document still translated");

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
            wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

            // edit content
            IWebElement textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.End);
            textarea.SendKeys("...");

            // click Save button
            IWebElement btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();
            Thread.Sleep(500);

            // validate if correct document is shown
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            documentContent = phrictionDocument.Text.Replace("\r", "");
            Assert.IsTrue(documentContent.StartsWith("Story of my life\nOnce upon a time, I was reading this story over and over again..."), "Master document not modified");

            ChangeLanguageFromEnglishToDutch();

            // validate if correct document is shown
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            documentContent = phrictionDocument.Text.Replace("\r", "");
            Assert.IsTrue(documentContent.StartsWith("Mijn levensverhaal\nEr was eens een tijd dat ik dit verhaal steeds opnieuw aan het lezen was"), "Translation not shown");

            // == Start test 3: master=staged and translation=staged ==============================================================================================
            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");

            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // click on Edit Translation
            edit = WebBrowser.FindElement(By.LinkText("Vertaling bewerken"));
            edit.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

            // edit content
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.End);
            textarea.SendKeys("...");

            // click Save button
            btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();
            Thread.Sleep(500);

            // validate if correct document is shown
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            documentContent = phrictionDocument.Text.Replace("\r", "");
            Assert.IsTrue(documentContent.StartsWith("Mijn levensverhaal\nEr was eens een tijd dat ik dit verhaal steeds opnieuw aan het lezen was..."), "Translation not modified");

            ChangeLanguageFromDutchToEnglish();

            // verify if master document is shown
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            documentContent = phrictionDocument.Text.Replace("\r", "");
            Assert.IsTrue(documentContent.StartsWith("Story of my life\nOnce upon a time, I was reading this story over and over again..."), "Modified master document not shown");

            // == Start test 4: master=not staged and translation=staged ==========================================================================================
            // click on logo to go back to the homepage
            IWebElement logo = WebBrowser.FindElement(By.XPath("//a[contains(@href, '')]"));
            logo.Click();

            // wait a while to make sure the save-config AJAX call is finished
            Thread.Sleep(1000);

            // Open 'Offline changes' screen
            IWebElement navigatorOfflineChanges = WebBrowser.FindElement(By.PartialLinkText("Offline changes"));
            navigatorOfflineChanges.Click();

            // wait a while
            Thread.Sleep(1000);

            // verify content of Offline Changes screen
            Assert.IsTrue(WebBrowser.FindElements(By.XPath("//a[contains(text(), 'Story of my life')]")).Count(elem => elem.Displayed) == 1);

            // click on undo button
            IWebElement btnUndo = WebBrowser.FindElements(By.XPath("//a[contains(text(), 'Undo')]")).FirstOrDefault(elem => elem.Displayed);
            btnUndo.Click();

            // confirm Undo
            btnYes = WebBrowser.FindElement(By.Id("dlgConfirmUndo"))
                               .FindElements(By.XPath("//button[text()=\"Yes\"]"))
                               .FirstOrDefault(button => button.Displayed);
            btnYes.Click();

            // wait until javascript is finished
            Thread.Sleep(1000);

            // verify content of Offline Changes screen
            Assert.IsTrue(WebBrowser.FindElements(By.XPath("//a[contains(text(), 'Story of my life')]")).Any(elem => elem.Displayed) == false);

            // click on 'Phriction' in the menu navigator
            navigatorPhriction = WebBrowser.FindElement(By.LinkText("Phriction"));
            navigatorPhriction.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

            // validate if Phriction was opened
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            documentContent = phrictionDocument.Text.Replace("\r", "");
            Assert.IsFalse(documentContent.StartsWith("Story of my life\nOnce upon a time, I was reading this story over and over again..."), "Master document not shown");
            Assert.IsTrue(documentContent.StartsWith("Story of my life\nOnce upon a time, I was reading this story over and over again"), "Master document not shown");

            ChangeLanguageFromEnglishToDutch();

            // validate if Phriction was opened
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            documentContent = phrictionDocument.Text.Replace("\r", "");
            Assert.IsTrue(documentContent.StartsWith("Mijn levensverhaal\nEr was eens een tijd dat ik dit verhaal steeds opnieuw aan het lezen was..."), "Modified translation not shown");
        }

        [TestMethod]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        [DataRow(typeof(FirefoxConfig), "")]
        [DataRow(typeof(FirefoxConfig), "phabrico")]
        public void PhrictionTranslatorSynchronization(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);

            DummyPhabricatorWebServer dummyPhabricatorWebServer = new DummyPhabricatorWebServer();

            // read translations.po and convert it to a dictionary (which we'll use in the dummy translator)
            string translated = File.ReadAllText(@"ContentTranslation\translations.po");
            Dictionary<string, string> translations = RegexSafe.Matches(translated, "^msgid +\"([^\"]*)\"\r?\nmsgstr +\"([^\"]*)", RegexOptions.Multiline)
                                                              .OfType<Match>()
                                                              .GroupBy(g => g.Groups[1].Value)
                                                              .Select(g => g.FirstOrDefault())
                                                              .ToDictionary(key => key.Groups[1].Value,
                                                                              value => value.Groups[2].Value
                                                                          );
            DummyTranslationEngine.Translations = translations;

            try
            {
                Logon();

                // == Prepare tests ===================================================================================================================================
                // click on 'Synchronize' in the menu navigator
                IWebElement navigatorSynchronize = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Synchronize')]"));
                navigatorSynchronize.Click();

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
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElement(By.Id("dlgSynchronizing")).Displayed);
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(30));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.Id("dlgSynchronizing")).Any(dlg => dlg != null && dlg.Displayed) == false);

                dummyPhabricatorWebServer.ReceivedRequests.Clear();

                // == Start test 1: master=not staged and translation=not staged ======================================================================================
                ChangeLanguageFromEnglishToDutch();

                // click on 'Phriction' in the menu navigator
                IWebElement navigatorPhriction = WebBrowser.FindElement(By.LinkText("Phriction"));
                navigatorPhriction.Click();

                // wait a while
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
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

                // click on Translate Document
                IWebElement translateDocument = WebBrowser.FindElement(By.LinkText("Document vertalen"));
                translateDocument.Click();

                // wait a while
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElements(By.Name("translationEngine")).Any());

                // select Dummy translation engine
                IWebElement translationEngine = WebBrowser.FindElement(By.Name("translationEngine"));
                translationEngine.Click();
                translationEngine.FindElements(By.TagName("option"))
                        .Single(option => option.Text == "Dummy")
                        .Click();
                translationEngine.Click();

                // select target language
                IWebElement targetLanguage = WebBrowser.FindElement(By.Name("targetLanguage"));
                targetLanguage.Click();
                targetLanguage.FindElements(By.TagName("option"))
                        .Single(option => option.Text == "Nederlands")
                        .Click();
                targetLanguage.Click();

                // click OK button
                IWebElement btnOK = WebBrowser.FindElement(By.ClassName("preparationParameters"))
                                              .FindElements(By.XPath("//button[text()=\"Translate\"]"))
                                              .FirstOrDefault(button => button.Displayed);
                btnOK.Click();

                // wait a while
                Thread.Sleep(500);

                // wait until "one moment please" is gone
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(10));
                wait.Until(condition => condition.FindElements(By.ClassName("wait")).Any(message => message.Displayed == false));

                // do not translate underlying documents: click No
                IWebElement btnNo = WebBrowser.FindElement(By.Id("dlgYesNoCancel"))
                                              .FindElements(By.XPath("//a[text()=\"Nee\"]"))
                                              .FirstOrDefault(button => button.Displayed);
                btnNo.Click();

                // wait a while
                Thread.Sleep(500);

                // wait until "one moment please" is gone
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(10));
                wait.Until(condition => condition.FindElements(By.ClassName("wait")).Any(message => message.Displayed == false));

                // Document is translated: click OK
                btnOK = WebBrowser.FindElement(By.Id("dlgOK"))
                                  .FindElements(By.XPath("//a[text()=\"OK\"]"))
                                  .FirstOrDefault(button => button.Displayed);
                btnOK.Click();

                AssertNoJavascriptErrors();

                // validate if document is translated
                phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
                string documentContent = phrictionDocument.Text.Replace("\r", "");
                Assert.IsTrue(documentContent.StartsWith("Mijn levensverhaal\nEr was eens een tijd dat ik dit verhaal steeds opnieuw aan het lezen was"), "Document not translated");

                // if action pane is collapsed -> expand it
                actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                                .GetAttribute("class")
                                                .Contains("right-collapsed");

                if (actionPaneCollapsed)
                {
                    IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                    expandActionPane.Click();
                }

                // click on Approve translation
                IWebElement approveTranslation = WebBrowser.FindElement(By.LinkText("Vertaling goedkeuren"));
                approveTranslation.Click();

                // confirm approval
                btnYes = WebBrowser.FindElement(By.Id("dlgYesNoCancel"))
                                   .FindElements(By.XPath("//a[text()=\"Ja\"]"))
                                   .FirstOrDefault(button => button.Displayed);
                btnYes.Click();

                // wait a while, so the javascript can refresh the page
                Thread.Sleep(1500);

                AssertNoJavascriptErrors();

                // if action pane is collapsed -> expand it
                actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                                .GetAttribute("class")
                                                .Contains("right-collapsed");

                if (actionPaneCollapsed)
                {
                    IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                    expandActionPane.Click();
                }

                // verify that the Approve translation button is one
                approveTranslation = WebBrowser.FindElements(By.LinkText("Vertaling goedkeuren")).FirstOrDefault();
                Assert.IsTrue(approveTranslation == null, "Translation was not approved");

                // == Start test 2: master=staged and translation=not staged ==========================================================================================
                ChangeLanguageFromDutchToEnglish();

                // verify if master document is shown
                phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
                documentContent = phrictionDocument.Text.Replace("\r", "");
                Assert.IsTrue(documentContent.StartsWith("Story of my life\nOnce upon a time, I was reading this story over and over again"), "Document still translated");

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
                wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

                // edit content
                IWebElement textarea = WebBrowser.FindElement(By.Id("textarea"));
                textarea.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.End);
                textarea.SendKeys("...");

                // click Save button
                IWebElement btnSave = WebBrowser.FindElement(By.Id("btnSave"));
                btnSave.Click();
                Thread.Sleep(500);

                // validate if correct document is shown
                phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
                documentContent = phrictionDocument.Text.Replace("\r", "");
                Assert.IsTrue(documentContent.StartsWith("Story of my life\nOnce upon a time, I was reading this story over and over again..."), "Master document not modified");

                ChangeLanguageFromEnglishToDutch();

                // validate if correct document is shown
                phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
                documentContent = phrictionDocument.Text.Replace("\r", "");
                Assert.IsTrue(documentContent.StartsWith("Mijn levensverhaal\nEr was eens een tijd dat ik dit verhaal steeds opnieuw aan het lezen was"), "Translation not shown");

                // == Start test 3: master=staged and translation=staged ==============================================================================================
                // if action pane is collapsed -> expand it
                actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                                .GetAttribute("class")
                                                .Contains("right-collapsed");

                if (actionPaneCollapsed)
                {
                    IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                    expandActionPane.Click();
                }

                // click on Edit Translation
                edit = WebBrowser.FindElement(By.LinkText("Vertaling bewerken"));
                edit.Click();

                // wait a while
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

                // edit content
                textarea = WebBrowser.FindElement(By.Id("textarea"));
                textarea.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.End);
                textarea.SendKeys("...");

                // click Save button
                btnSave = WebBrowser.FindElement(By.Id("btnSave"));
                btnSave.Click();
                Thread.Sleep(500);

                // validate if correct document is shown
                phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
                documentContent = phrictionDocument.Text.Replace("\r", "");
                Assert.IsTrue(documentContent.StartsWith("Mijn levensverhaal\nEr was eens een tijd dat ik dit verhaal steeds opnieuw aan het lezen was..."), "Translation not modified");

                ChangeLanguageFromDutchToEnglish();

                // verify if master document is shown
                phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
                documentContent = phrictionDocument.Text.Replace("\r", "");
                Assert.IsTrue(documentContent.StartsWith("Story of my life\nOnce upon a time, I was reading this story over and over again..."), "Modified master document not shown");

                // == Start test 4: make sure only non-translated changes are uploaded to Phabricator =================================================================
                IWebElement logo = WebBrowser.FindElement(By.XPath("//a[contains(@href, '')]"));
                logo.Click();

                // wait a while to make sure the save-config AJAX call is finished
                Thread.Sleep(1000);

                ChangeLanguageFromEnglishToDutch();

                // click on 'Synchronize' in the menu navigator
                navigatorSynchronize = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Synchronize')]"));
                navigatorSynchronize.Click();

                // verify if sync-confirmation dialog is visible
                IWebElement confirmDialog = WebBrowser.FindElement(By.Id("dlgRequestSynchronizeDetail"));
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(2));
                wait.Until(condition => confirmDialog.Text.Equals("Er is 1 lokale wijziging klaar om geupload te worden naar de Phabricator server."));

                // click on Yes button
                IWebElement btnConfirmSynchronization = WebBrowser.FindElement(By.Id("dlgRequestSynchronize"))
                                                                  .FindElement(By.XPath(".//*[contains(text(), 'Ja')]"));
                btnConfirmSynchronization.Click();

                // wait until synchronization process is finished
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElement(By.Id("dlgSynchronizing")).Displayed);
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(30));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.Id("dlgSynchronizing")).Any(dlg => dlg != null && dlg.Displayed) == false);

                // verify that only 1 document was uploaded
                Assert.AreEqual(1, dummyPhabricatorWebServer.ReceivedRequests.Count(url => url.Equals("phriction.edit") || url.Equals("phriction.create")));

                // verify if we don't have any local offline changes anymore
                Thread.Sleep(500);  // wait some milliseconds to make sure the page has been reloaded
                IWebElement hdrNumberOfUncommittedObjects = WebBrowser.FindElement(By.XPath("//label[contains(text(), 'Aantal niet-gecommitte objecten:')]"));
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

        [TestMethod]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        [DataRow(typeof(FirefoxConfig), "")]
        [DataRow(typeof(FirefoxConfig), "phabrico")]
        public void PhrictionTranslatorExportToExcel(Type browser, string httpRootPath)
        {
            Initialize(browser, httpRootPath);

            DummyPhabricatorWebServer dummyPhabricatorWebServer = new DummyPhabricatorWebServer();

            // read translations.po and convert it to a dictionary (which we'll use in the dummy translator)
            string translated = File.ReadAllText(@"ContentTranslation\translations.po");
            Dictionary<string, string> translations = RegexSafe.Matches(translated, "^msgid +\"([^\"]*)\"\r?\nmsgstr +\"([^\"]*)", RegexOptions.Multiline)
                                                              .OfType<Match>()
                                                              .GroupBy(g => g.Groups[1].Value)
                                                              .Select(g => g.FirstOrDefault())
                                                              .ToDictionary(key => key.Groups[1].Value,
                                                                              value => value.Groups[2].Value
                                                                          );
            DummyTranslationEngine.Translations = translations;

            try
            {
                Logon();

                // == Prepare tests ===================================================================================================================================
                // click on 'Synchronize' in the menu navigator
                IWebElement navigatorSynchronize = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Synchronize')]"));
                navigatorSynchronize.Click();

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
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElement(By.Id("dlgSynchronizing")).Displayed);
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(30));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.Id("dlgSynchronizing")).Any(dlg => dlg != null && dlg.Displayed) == false);

                dummyPhabricatorWebServer.ReceivedRequests.Clear();

                // == Start test 1: master=not staged and translation=not staged ======================================================================================
                ChangeLanguageFromEnglishToDutch();

                // click on 'Phriction' in the menu navigator
                IWebElement navigatorPhriction = WebBrowser.FindElement(By.LinkText("Phriction"));
                navigatorPhriction.Click();

                // wait a while
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
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

                // click on Translate Document
                IWebElement translateDocument = WebBrowser.FindElement(By.LinkText("Document vertalen"));
                translateDocument.Click();

                // wait a while
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.Until(condition => condition.FindElements(By.Name("translationEngine")).Any());

                // select Dummy translation engine
                IWebElement translationEngine = WebBrowser.FindElement(By.Name("translationEngine"));
                translationEngine.Click();
                translationEngine.FindElements(By.TagName("option"))
                        .Single(option => option.Text == "Excel")
                        .Click();
                translationEngine.Click();

                // select target language
                IWebElement targetLanguage = WebBrowser.FindElement(By.Name("targetLanguage"));
                targetLanguage.Click();
                targetLanguage.FindElements(By.TagName("option"))
                        .Single(option => option.Text == "Nederlands")
                        .Click();
                targetLanguage.Click();

                // click OK button
                IWebElement btnExportFile = WebBrowser.FindElement(By.ClassName("preparationParameters"))
                                                      .FindElements(By.XPath("//button[text()=\"Export File\"]"))
                                                      .FirstOrDefault(button => button.Displayed);
                btnExportFile.Click();

                // wait a while
                Thread.Sleep(500);

                // wait until "one moment please" is gone
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(10));
                wait.Until(condition => condition.FindElements(By.ClassName("wait")).Any(message => message.Displayed == false));

                // do not translate underlying documents: click No
                IWebElement btnNo = WebBrowser.FindElement(By.Id("dlgYesNoCancel"))
                                              .FindElements(By.XPath("//a[text()=\"Nee\"]"))
                                              .FirstOrDefault(button => button.Displayed);
                btnNo.Click();

                // wait a while
                Thread.Sleep(500);

                // wait until "one moment please" is gone
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(10));
                wait.Until(condition => condition.FindElements(By.ClassName("wait")).Any(message => message.Displayed == false));

                // Document is translated: click OK
                IWebElement btnOK = WebBrowser.FindElement(By.Id("dlgOK"))
                                              .FindElements(By.XPath("//a[text()=\"OK\"]"))
                                              .FirstOrDefault(button => button.Displayed);
                btnOK.Click();

                AssertNoJavascriptErrors();

                // validate if excel is generated
                string expectedExcelFilePath = DownloadDirectory + "\\Translation.xlsx";
                int checkIfExcelFileIsDownloaded = 100;  // wait max 10 seconds
                for (; checkIfExcelFileIsDownloaded > 0; checkIfExcelFileIsDownloaded--)
                {
                    if (File.Exists(expectedExcelFilePath)) break;
                    Thread.Sleep(100);
                }

                if (checkIfExcelFileIsDownloaded <= 0)
                {
                    Assert.Fail("Excel file was not generated");
                }

                FileInfo excelFileInfo = new FileInfo(expectedExcelFilePath);
                if (excelFileInfo.Length == 0)
                {
                    Assert.Fail("Empty Excel file generated");
                }
            }
            finally
            {
                dummyPhabricatorWebServer.Stop();
            }
        }

        private void ChangeLanguageFromEnglishToDutch()
        {
            WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("user-menu")).Any());

            IWebElement userMenu = null;
            for (int tryOut = 0; tryOut < 5; tryOut++)
            {
                // open user menu
                userMenu = WebBrowser.FindElement(By.ClassName("user-menu"));
                userMenu.Click();

                if (WebBrowser.FindElements(By.PartialLinkText("Change language")).Any(elem => elem.Displayed)) break;
                Thread.Sleep(250);  // in Firefox-Selenium, the Click-action happens sometimes twice -> try again
            }

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
        }

        private void ChangeLanguageFromDutchToEnglish()
        {
            WebDriverWait wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("user-menu")).Any());

            // open user menu
            IWebElement userMenu = WebBrowser.FindElement(By.ClassName("user-menu"));
            userMenu.Click();

            // click 'Change language'
            IWebElement mnuChangeLanguage = userMenu.FindElement(By.PartialLinkText("Taal wijzigen"))
                                                    .FindElement(By.TagName("span"));  // otherwise firefox webdriver won't work
            mnuChangeLanguage.Click();

            // change language to English
            IWebElement language = WebBrowser.FindElement(By.Id("newLanguage"));
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
            IWebElement btnChangeLanguage = WebBrowser.FindElement(By.XPath("//button[contains(text(), 'Taal wijzigen')]"));
            btnChangeLanguage.Click();
            Thread.Sleep(500);  // wait a while to make sure the redirect call is finished

            // verify new language change
            IWebElement search = WebBrowser.FindElement(By.Id("searchPhabrico"));
            Assert.AreEqual(search.GetAttribute("placeholder"), "Search");
        }

        [TestMethod]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        [DataRow(typeof(FirefoxConfig), "")]
        [DataRow(typeof(FirefoxConfig), "phabrico")]
        public void PhrictionValidator(Type browser, string httpRootPath)
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
            textarea.SendKeys(" {F31415927}  [[ ./inexistant ]]");

            // click Save button
            IWebElement btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phui-document")).Any());

            // validate document content
            string documentContent = WebBrowser.FindElement(By.Id("remarkupContent"))
                                               .Text;
            Assert.AreEqual("Once upon a time, I was reading this story over and over again inexistant", documentContent);

             // navigate to "Story of my grandfather's life" document
            IWebElement linkStoryGrandfathersLife = WebBrowser.FindElement(By.XPath("//*[contains(text(), \"Story of my grandfather's life\")]"));
            linkStoryGrandfathersLife.Click();

            // validate if correct document is shown
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            phrictionDocumentTitle = phrictionDocument.Text.Split('\r', '\n')[0];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Story of my grandfather's life"), "Unable to open dad's life story");

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
            textarea.SendKeys(" {F27182818}  [[ ./inexistant2 ]]");

            // click Save button
            btnSave = WebBrowser.FindElement(By.Id("btnSave"));
            btnSave.Click();
            Thread.Sleep(500);

            // validate if correct document is shown
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            phrictionDocumentTitle = phrictionDocument.Text.Split('\r', '\n')[0];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Story of my grandfather's life"), "Unable to open grandaddy's life story");

            // validate document content
            documentContent = WebBrowser.FindElement(By.Id("remarkupContent"))
                                        .Text;
            Assert.AreEqual("Once upon a time, I was reading my grandfather's story over and over again inexistant2", documentContent);

            // go to Phriction root page
            IWebElement rootPhriction = WebBrowser.FindElements(By.XPath("//a"))
                                                  .FirstOrDefault(a => a.Displayed && a.GetAttribute("href").EndsWith("/w/"));
            rootPhriction.Click();

            // validate if correct document is shown
            phrictionDocument = WebBrowser.FindElement(By.ClassName("phui-document"));
            phrictionDocumentTitle = phrictionDocument.Text.Split('\r', '\n')[0];
            Assert.IsTrue(phrictionDocumentTitle.Equals("Story of my life"), "Unable to open my life story");

            // if action pane is collapsed -> expand it
            actionPaneCollapsed = WebBrowser.FindElement(By.ClassName("phabrico-page-content"))
                                            .GetAttribute("class")
                                            .Contains("right-collapsed");

            if (actionPaneCollapsed)
            {
                IWebElement expandActionPane = WebBrowser.FindElement(By.ClassName("fa-chevron-left"));
                expandActionPane.Click();
            }

            // click on Validate
            IWebElement validate = WebBrowser.FindElement(By.LinkText("Validate document"));
            validate.Click();
            Thread.Sleep(500); // wait for javascript to be finished

            // verify if messagebox is shown
            IWebElement msgValidateUnderlyingDocuments = WebBrowser.FindElement(By.XPath("//*[contains(text(), \"There are 3 underlying documents. Would you like to validate these as well ?\")]"));
            IWebElement btnValidateUnderlyingDocumentsYes = WebBrowser.FindElement(By.XPath("//*[contains(text(), \"Yes\")]"));
            btnValidateUnderlyingDocumentsYes.Click();
            Thread.Sleep(500); // wait for javascript to be finished

            // validate validation result
            IWebElement[] invalidFileReferences = WebBrowser.FindElements(By.XPath("//*[contains(text(), \"Invalid file reference\")]")).ToArray();
            Assert.IsTrue(invalidFileReferences.Any(invalidFileReference => invalidFileReference.GetAttribute("textContent").Equals("Invalid file reference 31415927")));
            Assert.IsTrue(invalidFileReferences.Any(invalidFileReference => invalidFileReference.GetAttribute("textContent").Equals("Invalid file reference 27182818")));
            IWebElement[] invalidHyperlinks = WebBrowser.FindElements(By.XPath("//*[contains(text(), \"Invalid hyperlink\")]")).ToArray();
            Assert.IsTrue(invalidHyperlinks.Any(invalidHyperlink => invalidHyperlink.GetAttribute("textContent").Equals("Invalid hyperlink [[ ./inexistant ]]")));
            Assert.IsTrue(invalidHyperlinks.Any(invalidHyperlink => invalidHyperlink.GetAttribute("textContent").Equals("Invalid hyperlink [[ ./inexistant2 ]]")));
        }
    }
}