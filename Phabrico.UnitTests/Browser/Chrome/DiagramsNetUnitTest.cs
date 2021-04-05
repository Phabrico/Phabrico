using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.ObjectModel;
using WebDriverManager.DriverConfigs.Impl;

namespace Phabrico.UnitTests.Browser.Chrome
{
    [TestClass]
    public class DiagramsNetUnitTest : BrowserUnitTest
    {
        public DiagramsNetUnitTest() : base(new ChromeConfig())
        {
            WebBrowser = new ChromeDriver();
        }
        
        [TestMethod]
        public void OpenPhrictionAndAddDiagram()
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

            // add new line  in textarea
            IWebElement textarea = WebBrowser.FindElement(By.Id("textarea"));
            textarea.SendKeys(OpenQA.Selenium.Keys.End);
            textarea.SendKeys(OpenQA.Selenium.Keys.Enter);

            // click on Diagrams button in Remarkup editor toolbar
            IWebElement diagramsButton = WebBrowser.FindElement(By.ClassName("fa-sitemap"));
            diagramsButton.Click();

            // confirm "Leave site?" dialog
            WebBrowser.SwitchTo().Alert().Accept(); 

            // wait until DiagramsNet IFrame content is fully loaded
            wait.Until(condition => condition.FindElement(By.TagName("IFrame")));
            IWebElement diagramsIFrame = WebBrowser.FindElement(By.TagName("IFrame"));
            WebBrowser.SwitchTo().Frame(diagramsIFrame);

            WebDriverWait webDriverWait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(10));
            webDriverWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(WebDriverException));
            webDriverWait.Until(condition => condition.FindElement(By.XPath("//*[contains(@title, 'Shapes')]")));

            // show Shapes toolbox if invisible
            IWebElement shapes = null;
            try
            {
                shapes = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Shapes')]"));
            }
            catch
            {
            }

            if (shapes == null || shapes.Displayed == false)
            {
                IWebElement toolbarButtonShapes = WebBrowser.FindElement(By.XPath("//*[contains(@title, 'Shapes')]"));
                toolbarButtonShapes.Click();
                shapes = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Shapes')]"));
            }

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
            wait.Until(condition => condition.FindElement(By.XPath("//*[contains(text(), 'F-1')]")));
            
            // wait until DiagramsNet IFrame content is fully loaded
            wait.Until(condition => condition.FindElement(By.TagName("IFrame")));
            diagramsIFrame = WebBrowser.FindElement(By.TagName("IFrame"));
            WebBrowser.SwitchTo().Frame(diagramsIFrame);

            webDriverWait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(10));
            webDriverWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(WebDriverException));
            webDriverWait.Until(condition => condition.FindElement(By.XPath("//*[contains(@title, 'Shapes')]")));

            // click on exit button
            IWebElement exit = WebBrowser.FindElement(By.XPath("//*[contains(text(), 'Exit')]"));
            exit.Click();

             // wait a while
            wait = new WebDriverWait(WebBrowser, TimeSpan.FromSeconds(5));
            wait.Until(condition => condition.FindElement(By.ClassName("phriction-edit")));

            // verify if diagram-reference is added to Phriction's textarea content
            textarea = WebBrowser.FindElement(By.Id("textarea"));
            Assert.AreEqual("Once upon a time, I was reading this story over and over again\n{F-1, size=full}", textarea.GetAttribute("value").Replace("\r", ""));
            wait.Until(condition => condition.FindElement(By.ClassName("diagram")));

            // verify if image is presented on the right side
            IWebElement image = WebBrowser.FindElement(By.ClassName("diagram"));
            wait.Until(condition => condition.FindElement(By.ClassName("diagram")).Displayed);
            Assert.AreNotEqual(image.Size.Width, 0);
            Assert.AreNotEqual(image.Size.Height, 0);
        }
    }
}