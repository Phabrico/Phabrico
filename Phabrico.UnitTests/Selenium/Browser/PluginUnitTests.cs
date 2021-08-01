using System;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
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

                // click on 'Gitanos' tab
                IWebElement tabGitanos = WebBrowser.FindElement(By.XPath("//button[contains(text(), 'Gitanos')]"));
                tabGitanos.Click();

                // click on 'Add root directory'
                IWebElement btnAddRootDirectory = WebBrowser.FindElement(By.Id("btnGitanosAddRootDirectory"));
                btnAddRootDirectory.Click();
                Thread.Sleep(1000);  // wait a while to make sure all the javascript functionality has been finished

                // enter new 'Gitanos root directory'
                Actions action = new Actions(WebBrowser);
                action.SendKeys(gitanosRootDirectory + Keys.Enter).Perform();

                // click on logo to go back to the homepage
                IWebElement logo = WebBrowser.FindElement(By.XPath("//a[contains(@href, '')]"));
                logo.Click();

                // wait a while to make sure the save-config AJAX call is finished
                Thread.Sleep(1000);

                // Open 'Gitanos' screen
                IWebElement navigatorGitanos = WebBrowser.FindElement(By.PartialLinkText("Gitanos"));
                navigatorGitanos.Click();

                // wait until Gitanos screen is loaded AND DirectoryMonitor is finished
                Thread.Sleep(3000);

                // verify content of Gitanos screen
                Assert.IsFalse(WebBrowser.FindElements(By.XPath("//a[contains(text(), 'gitRepository')]")).Any(elem => elem.Displayed));

                // click on 'Show clean repositories'
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.XPath("//button[contains(text(), 'Show clean repositories')]")).Any(elem => elem.Displayed));
                IWebElement btnShowCleanRepositories = WebBrowser.FindElement(By.XPath("//button[contains(text(), 'Show clean repositories')]"));
                btnShowCleanRepositories.Click();

                // verify new content of Gitanos screen
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.XPath("//a[contains(text(), 'GitanosRemote')]")).Any(elem => elem.Displayed));

                // click on GitanosRemote
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.XPath("//a[contains(text(), 'GitanosRemote')]")).Any(elem => elem.Displayed));
                IWebElement gitanosRemote = WebBrowser.FindElement(By.XPath("//a[contains(text(), 'GitanosRemote')]"));
                gitanosRemote.Click();

                // verify that wehave a clean repository
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.XPath("//div[contains(text(), 'This git repository is clean')]")).Any(elem => elem.Displayed));

                // modify dummy file
                System.IO.File.WriteAllText(gitanosRootDirectory + "\\GitanosRemote\\dummy.txt", "dummy file.");

                // Open 'Gitanos' screen again
                navigatorGitanos = WebBrowser.FindElement(By.PartialLinkText("Gitanos"));
                navigatorGitanos.Click();


                // click on 'dummy.txt'
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.XPath("//a[contains(text(), 'GitanosRemote')]")).Any(elem => elem.Displayed));
                IWebElement gitRepository = WebBrowser.FindElement(By.XPath("//a[contains(text(), 'GitanosRemote')]"));
                gitRepository.Click();


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

                // confirm error
                IWebElement btnOK = dlgGitanosError.FindElement(By.XPath("//a[contains(text(), 'OK')]"));
                btnOK.Click();


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

                // verify if 'dummy.txt' is back in the list of unpushed commits
                wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
                wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                wait.Until(condition => condition.FindElements(By.XPath("//a[contains(text(), 'dummy.txt')]")).Any(elem => elem.Displayed));
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
        // [DataRow(typeof(FirefoxConfig), "")]           // disabled: unable to hide 'Save download as' dialog
        // [DataRow(typeof(FirefoxConfig), "phabrico")]   // disabled: unable to hide 'Save download as' dialog
        public void PhrictionToPDFExport(Type browser, string httpRootPath)
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

            // click on 'Export to PDF'
            IWebElement exportToPDF = WebBrowser.FindElement(By.LinkText("Export to PDF"));
            exportToPDF.Click();

            // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.XPath("//*[contains(text(), \"There are 2 underlying documents. Would you like to export these as well ?\")]")).Any());

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

                        return true;
                    }
                }
                catch
                {
                }

                return false;
            });
        }
    }
}