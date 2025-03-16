using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
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
            // Collect all errors in a list
            List<string> errors = new List<string>();

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
                if (packagesConfigFile == null)
                {
                    errors.Add($"{Path.GetFileName(nuspecFile)}: No corresponding packages.config found");
                    continue;
                }

                XDocument projDefinition = XDocument.Load(packagesConfigFile);
                Dictionary<string, string> packagesConfigVersionsPerDependency = projDefinition
                    .Element("packages")
                    ?.Elements("package")
                    .ToDictionary(key => key.Attribute("id").Value.ToUpper(),
                                  value => value.Attribute("version").Value
                                 ) ?? new Dictionary<string, string>();

                foreach (string packagesConfigReference in packagesConfigVersionsPerDependency.Keys)
                {
                    string nuspecVersion;
                    if (!nugetVersionsPerDependency.TryGetValue(packagesConfigReference, out nuspecVersion))
                    {
                        errors.Add($"{nuspecFile}: Reference to {packagesConfigReference} (version {packagesConfigVersionsPerDependency[packagesConfigReference]}) is missing");
                    }
                    else if (packagesConfigVersionsPerDependency[packagesConfigReference] != nuspecVersion)
                    {
                        errors.Add($"{nuspecFile}: Mismatch for {packagesConfigReference} - Old version: {nuspecVersion}, Expected version: {packagesConfigVersionsPerDependency[packagesConfigReference]}");
                    }
                }
            }

            // Assert all errors at once
            if (errors.Any())
            {
                string errorMessage = "Found version mismatches or missing dependencies:\n" + string.Join("\n", errors);
                Assert.Fail(errorMessage);
            }
        }
    }
}