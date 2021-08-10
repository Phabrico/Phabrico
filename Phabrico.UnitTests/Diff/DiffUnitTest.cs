using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Phabrico.UnitTests.Diff
{
    /// <summary>
    /// This UnitTest will read all the files in the TestData folder.
    /// Each test file contains a JSON formatted test which will test offline editing functionality
    /// Check out <see cref="Phabrico.UnitTests.JsonConfiguration.UnitTest"/> for more information about this JSON test files
    /// </summary>
    [TestClass]
    public class DiffUnitTest : PhabricoUnitTest
    {
        Phabrico.Miscellaneous.Diff diff;

        public DiffUnitTest()
        {
            diff = new Phabrico.Miscellaneous.Diff();
        }

        [TestMethod]
        public void TestDiffController()
        {
            string[] testFileNames = Directory.EnumerateFiles(@".\Diff\TestData", "*.*", SearchOption.TopDirectoryOnly)
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

                    // execute test
                    foreach (string methodName in unitTest.execute.method)
                    {
                        MethodInfo progressMethod = diff.GetType().GetMethod(methodName);
                        unitTest.result = progressMethod.Invoke(diff, unitTest.execute.arguments.ToArray());
                    }

                    if (unitTest.Success == false)
                    {
                        Assert.Fail(string.Format("Diff Unit Test '{0}' failed!!", System.IO.Path.GetFileName(testFileName)));
                    }
                }
                catch
                {
                    Assert.Fail("Test {0} FAILED", System.IO.Path.GetFileNameWithoutExtension(testFileName));
                }
            }
        }
    }
}
