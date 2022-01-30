using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phabrico.Miscellaneous;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Phabrico.UnitTests.Nuget
{
    [TestClass]
    public class NugetTest : UnitTests.PhabricoUnitTest
    {
        /// <summary>
        /// This test will read all the nuspec files in the solution and verifies if the version numbers
        /// of the mentioned dependencies are the same as the actual version numbers of the DLLs
        /// </summary>
        [TestMethod]
        public void TestVersionNumberOfDependencies()
        {
            string[] nuspecFiles = Directory.EnumerateFiles(Path.GetFullPath(@"..\..\.."), "*.nuspec", SearchOption.AllDirectories)
                                            .Where(path => path.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) < 0)
                                            .ToArray();
            foreach (string nuspecFile in nuspecFiles)
            {
                string content = File.ReadAllText(nuspecFile);
                Dictionary<string, string> nugetVersionsPerDependency;

                using (MemoryStream memoryStream = new MemoryStream(UTF8Encoding.UTF8.GetBytes(content)))
                {
                    XDocument xDocument = XDocument.Load(memoryStream);
                    nugetVersionsPerDependency = xDocument.XPathSelectElements("./package/metadata/dependencies/dependency")
                                                     .Where(key => key.Attribute("id").Value
                                                                      .Equals("Phabrico", StringComparison.OrdinalIgnoreCase) == false
                                                           )
                                                     .ToDictionary(key => key.Attribute("id").Value.ToUpper(),
                                                                   value => value.Attribute("version").Value
                                                                  );
                    if (nugetVersionsPerDependency.Any() == false) continue;
                }

                string packagesConfigFile = Directory.EnumerateFiles(Path.GetDirectoryName(nuspecFile), "packages.config", SearchOption.TopDirectoryOnly).FirstOrDefault();
                XDocument projDefinition = XDocument.Load(packagesConfigFile);
                Dictionary<string,string> packagesConfigVersionsPerDependency = projDefinition
                    .Element("packages")
                    .Elements("package")
                    .ToDictionary(key => key.Attribute("id").Value.ToUpper(),
                                value => value.Attribute("version").Value
                                );

                foreach (string packagesConfigReference in packagesConfigVersionsPerDependency.Keys)
                {
                    string nuspecVersion;
                    if (nugetVersionsPerDependency.TryGetValue(packagesConfigReference, out nuspecVersion) == false)
                    {
                        Assert.Fail("{0} has a reference to {1} which is missing in {2}",
                            Path.GetFileName(packagesConfigFile),
                            packagesConfigReference,
                            Path.GetFileName(nuspecFile));
                    }
                    
                    Assert.AreEqual(packagesConfigVersionsPerDependency[packagesConfigReference],
                                    nuspecVersion,
                                    string.Format("Mismatch version numbers for {0} in {1} and {2}",
                                        packagesConfigReference,
                                        Path.GetFileName(packagesConfigFile),
                                        Path.GetFileName(nuspecFile))
                                    );
                }
            }
        }
    }
}