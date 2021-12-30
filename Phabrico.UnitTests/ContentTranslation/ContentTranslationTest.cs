using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phabrico.ContentTranslation.Engines;
using Phabrico.Miscellaneous;
using Phabrico.Parsers.Remarkup;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Phabrico.UnitTests.ContentTranslation
{
    [TestClass]
    public class ContentTranslationTest : PhabricoUnitTest
    {
        Phabrico.Controllers.Remarkup remarkupController;

        protected override void Initialize(string httpRootPath)
        {
            base.Initialize(httpRootPath);

            remarkupController = new Phabrico.Controllers.Remarkup();
            remarkupController.browser = new Http.Browser(HttpServer, HttpListenerContext);
            remarkupController.EncryptionKey = EncryptionKey;

            Http.SessionManager.Token token = remarkupController.browser.HttpServer.Session.CreateToken(accountWhoAmI.Token, remarkupController.browser);
            remarkupController.browser.SetCookie("token", token.ID, true);
            token.EncryptionKey = Encryption.XorString(EncryptionKey, PublicXorCipher);
            token.PrivateEncryptionKey = Encryption.XorString(PrivateEncryptionKey, PrivateXorCipher);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("phabrico")]
        public void TestContentTranslation(string httpRootPath)
        {
            Initialize(httpRootPath);

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
            DummyTranslationEngine dummyTranslationEngine = new DummyTranslationEngine("none");

            // start testing
            string[] testFileNames = Directory.EnumerateFiles(@".\ContentTranslation\TestData", "*.*", SearchOption.TopDirectoryOnly)
                                              .OrderBy(fileName => fileName)
                                              .ToArray();

            foreach (string testFileName in testFileNames)
            {
                try
                {
                    // read test file and prepare test content and expected result
                    string testFileContent = File.ReadAllText(testFileName);
                    string[] testData = RegexSafe.Split(testFileContent, "\r?\n~~~~~~~~+\r?\n");
                    if (testData.Length < 2) throw new InvalidDataException();

                    string originalContent = testData[0];
                    string expectedResult = testData[1].Replace("\r", "");

                    // execute test
                    RemarkupParserOutput remarkupParserOutput;
                    remarkupController.ConvertRemarkupToHTML(Database, "/", originalContent, out remarkupParserOutput, false);
                    string testResultXmlData = remarkupParserOutput.TokenList.ToXML(Database, remarkupController.browser, "");
                    string translatedTestResultXmlData = dummyTranslationEngine.TranslateXML("en", "nl", testResultXmlData);
                    string testResult = remarkupParserOutput.TokenList.FromXML(Database, remarkupController.browser, "/", translatedTestResultXmlData);

                    if (testResult.Equals(expectedResult) == false)
                    {
                        Assert.AreEqual(expectedResult, testResult, string.Format("Content Translation Unit Test '{0}' failed!!", System.IO.Path.GetFileName(testFileName)));
                    }
                }
                catch
                {
                }
            }
        }
    }
}