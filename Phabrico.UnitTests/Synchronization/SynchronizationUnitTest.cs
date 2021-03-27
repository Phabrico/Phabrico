using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Phabrico.Controllers.Synchronization;

namespace Phabrico.UnitTests.Synchronization
{
    /// <summary>
    /// This UnitTest will read all the files in the TestData folder.
    /// Each test file contains a JSON formatted test which will test the Phabricator API calls
    /// Check out <see cref="Phabrico.UnitTests.JsonConfiguration.UnitTest"/> for more information about this JSON test files
    /// </summary>
    [TestClass]
    public class SynchronizationUnitTest : PhabricoUnitTest
    {
        Phabrico.Controllers.Synchronization synchronizationController;
        DummyPhabricatorWebServer phabricatorWebServer;

        public SynchronizationUnitTest()
        {
            Miscellaneous.HttpListenerContext httpListenerContext = new Miscellaneous.HttpListenerContext();

            synchronizationController = new Phabrico.Controllers.Synchronization();
            synchronizationController.browser = new Http.Browser(HttpServer, httpListenerContext);
            synchronizationController.EncryptionKey = EncryptionKey;
            synchronizationController.TokenId = HttpServer.Session.ClientSessions.LastOrDefault().Key;
            
            Http.SessionManager.Token token = synchronizationController.browser.HttpServer.Session.CreateToken(EncryptionKey, synchronizationController.browser);
            synchronizationController.browser.SetCookie("token", token.ID);
            token.PrivateEncryptionKey = EncryptionKey;

            phabricatorWebServer = new DummyPhabricatorWebServer();
        }

        public override void Dispose()
        {
            phabricatorWebServer.Stop();

            base.Dispose();
        }

        [TestMethod]
        public void TestSynchronizationConduitAPI()
        {
            string[] testFileNames = Directory.EnumerateFiles(@".\Synchronization\TestData", "*.*", SearchOption.TopDirectoryOnly)
                                              .OrderBy(fileName => fileName)
                                              .ToArray();

            foreach (string testFileName in testFileNames)
            {
                try
                {
                    // read test file and prepare test content and expected result
                    JsonConfiguration.UnitTest unitTest = ParseTestConfiguration(testFileName);
                    if (unitTest == null || unitTest.execute == null) throw new System.Exception(string.Format("Invalid test data in '{0}'", testFileName));

                    // initialize unit test
                    unitTest.Database = Database;

                    // prepare test
                    Miscellaneous.HttpListenerContext httpListenerContext = new Miscellaneous.HttpListenerContext();
                    SynchronizationParameters synchronizationParameters = new SynchronizationParameters();
                    synchronizationParameters.browser = new Http.Browser(HttpServer, httpListenerContext);
                    synchronizationParameters.browser.Conduit = new Phabricator.API.Conduit(HttpServer, synchronizationParameters.browser);
                    synchronizationParameters.browser.Conduit.PhabricatorUrl = "http://127.0.0.2:46975";
                    synchronizationParameters.database = Database;
                    synchronizationParameters.existingAccount = accountWhoAmI;
                    synchronizationParameters.projectSelected[Phabricator.Data.Project.None] = Phabricator.Data.Project.Selection.Selected;
                    synchronizationParameters.remotelyModifiedObjects = new System.Collections.Generic.List<Phabricator.Data.PhabricatorObject>();

                    // execute test
                    foreach (string methodName in unitTest.execute.method)
                    {
                        MethodInfo progressMethod = synchronizationController.GetType().GetMethod(methodName);
                        progressMethod.Invoke(synchronizationController, new object[] { synchronizationParameters, 1, 10 });
                    }

                    if (unitTest.Success == false)
                    {
                        Assert.Fail(string.Format("Synchronization Unit Test '{0}' failed!!", System.IO.Path.GetFileName(testFileName)));
                    }
                }
                catch (System.Exception exception)
                {
                    Assert.Fail("Test {0} FAILED: {1}", System.IO.Path.GetFileNameWithoutExtension(testFileName), exception.Message);
                }
            }
        }
    }
}
