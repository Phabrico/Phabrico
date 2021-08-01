using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using WebDriverManager.DriverConfigs.Impl;

namespace Phabrico.UnitTests.Selenium.Browser
{
    [TestClass]
    public class DiagramsNetUnitTest : BrowserUnitTest
    {
        [TestMethod]
        [DataRow(typeof(ChromeConfig), "")]
        [DataRow(typeof(ChromeConfig), "phabrico")]
        [DataRow(typeof(EdgeConfig), "")]
        [DataRow(typeof(EdgeConfig), "phabrico")]
        // [DataRow(typeof(FirefoxConfig), "")]         // disabled: https://bugzilla.mozilla.org/show_bug.cgi?id=1722021
        // [DataRow(typeof(FirefoxConfig), "phabrico")] // disabled: https://bugzilla.mozilla.org/show_bug.cgi?id=1722021
        public void OpenPhrictionAndAddDiagram(Type browser, string httpRootPath)
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

            // add new line  in textarea
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
            IWebElement diagramsIFrame = WebBrowser.FindElement(By.TagName("IFrame"));
            WebBrowser.SwitchTo().Frame(diagramsIFrame);

            WebDriverWait webDriverWait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(10));
            webDriverWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(WebDriverException));
            webDriverWait.Until(condition => condition.FindElements(By.XPath("//*[contains(@title, 'Shapes')]")).Any());

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
            action.SendKeys("Test").Perform();

            // click on save button
            IWebElement save = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Save')]"));
            save.Click();

            // verify if the page is reloaded with the new file name
            wait.Until(condition => condition.FindElements(By.XPath("//*[contains(text(), 'F-1')]")).Any());
            
            // wait until DiagramsNet IFrame content is fully loaded
            wait.Until(condition => condition.FindElements(By.TagName("IFrame")).Any());
            diagramsIFrame = WebBrowser.FindElement(By.TagName("IFrame"));
            WebBrowser.SwitchTo().Frame(diagramsIFrame);

            webDriverWait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(10));
            webDriverWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(WebDriverException));
            webDriverWait.Until(condition => condition.FindElements(By.XPath("//*[contains(@title, 'Shapes')]")).Any());

            // click on exit button
            IWebElement exit = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Exit')]"));
            exit.Click();

             // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElements(By.ClassName("phriction-edit")).Any());

            // verify if diagram-reference is added to Phriction's textarea content
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            Assert.AreEqual("Once upon a time, I was reading this story over and over again\n{F-1, size=full}", textarea.GetAttribute("value").Replace("\r", ""));
            wait.Until(condition => condition.FindElements(By.ClassName("diagram")).Any());

            // verify if image is presented on the right side
            IWebElement image = WebBrowser.FindElement(By.ClassName("diagram"));
            wait.Until(condition => condition.FindElement(By.ClassName("diagram")).Displayed);
            Assert.AreNotEqual(image.Size.Width, 0);
            Assert.AreNotEqual(image.Size.Height, 0);
        }
    }
}