using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phabrico.Miscellaneous;
using Phabrico.Parsers.Remarkup;
using System.IO;
using System.Linq;

namespace Phabrico.UnitTests.Remarkup
{
    /// <summary>
    /// This UnitTest will read all the files in the TestData folder.
    /// Each file consists of 2 parts which are separated by "~~~~~~~~~~" line.
    /// The first part contains the Remarkup syntax that will be tested.
    /// The second part contains the expected HTML for the given Remarkup syntax.
    /// </summary>
    [TestClass]
    public class RemarkupUnitTest : UnitTests.PhabricoUnitTest
    { 
        Phabrico.Controllers.Remarkup remarkupController;

        protected override void Initialize(string httpRootPath)
        {
            base.Initialize(httpRootPath);

            remarkupController = new Phabrico.Controllers.Remarkup();
            remarkupController.browser = new Http.Browser(HttpServer, HttpListenerContext);
            remarkupController.EncryptionKey = EncryptionKey;

            Http.SessionManager.Token token = remarkupController.browser.HttpServer.Session.CreateToken(accountWhoAmI.Token, remarkupController.browser);
            remarkupController.browser.SetCookie("token", token.ID);
            token.EncryptionKey = Encryption.XorString(EncryptionKey, PublicXorCipher);
            token.PrivateEncryptionKey = Encryption.XorString(PrivateEncryptionKey, PrivateXorCipher);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("phabrico")]
        public void TestRemarkupEngine(string httpRootPath)
        {
            Initialize(httpRootPath);

            string[] testFileNames = Directory.EnumerateFiles(@".\Remarkup\TestData", "*.*", SearchOption.TopDirectoryOnly)
                                              .OrderBy(fileName => fileName)
                                              .ToArray();
            foreach (string testFileName in testFileNames)
            {
                // read test file and prepare test content and expected result
                string testFileContent = File.ReadAllText(testFileName);
                string[] testData = RegexSafe.Split(testFileContent, "\r?\n~~~~~~~~+\r?\n");
                if (testData.Length != 3) throw new System.Exception(string.Format("Invalid test data in '{0}'", testFileName));

                string inputRemarkupData = testData[0];
                string expectedHtmlData = testData[1].Replace("\r\n", "\n");
                expectedHtmlData = expectedHtmlData.Replace("\r", "\n");

                // execute test
                RemarkupParserOutput remarkupParserOutput;
                string testResultHtmlData = remarkupController.ConvertRemarkupToHTML(Database, "/", inputRemarkupData, out remarkupParserOutput, false);
                testResultHtmlData = testResultHtmlData.Replace("\r\n", "\n");
                testResultHtmlData = testResultHtmlData.Replace("\r", "\n");
                testResultHtmlData = testResultHtmlData.TrimEnd('\n');

                if (testResultHtmlData.Equals(expectedHtmlData) == false)
                {
                    Assert.AreEqual(expectedHtmlData, testResultHtmlData, string.Format("Remarkup Unit Test '{0}' failed!!", System.IO.Path.GetFileName(testFileName)));
                }
            }
        }
    }
}
