/* This pseudo T4 script will generate a BuildVersion.cs file.
   This BuildVersion.cs file contains:
   - the auto-generated version number of this application
   - the UTC build timestamp
   - the MD5sum value of the whole project source directory

   If the calculated MD5 sum is different from the previously generated MD5 sum, a new version number will be calculated 
   based on the UTC modification timestamp of the last modified file.
   The version number is formatted as YY.MM.DDHH.MMSS
   The year is subtracted with 2000 because according to https://docs.microsoft.com/en-us/windows/win32/msi/productversion the 
   major version number has a maximum limit of 255
   The same rules applies to the minor version number, which contains the month number.
   If the calculated MD5 sum is the same as the previously generated MD5 sum, no new version number will be calculated
   The UTC build timestamp will however be updated each time the project is compiled
*/ 

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Phabrico
{
    class Program
    {
        static void Main(string[] args)
        {
            // read previously generated BuildVersion.cs first and get MD5 sum ofdirectory
            string hostPath = args[0];
            string originalMD5sum = "";
            string originalBuildVersionContent = "";
            try
            {
                originalBuildVersionContent = System.IO.File.ReadAllText(hostPath + "\\BuildVersion.cs");
                Match matchOriginalMD5 = Regex.Match(originalBuildVersionContent, "MD5 of solution directory is [<]([^>]+)[>]");
                if (matchOriginalMD5.Success)
                {
                    originalMD5sum = matchOriginalMD5.Groups[1].Value;
                }
            }
            catch
            {
            }

            // collect all file names from which we can calculate a new version number
            var allSolutionFiles = new System.IO.DirectoryInfo(hostPath + "\\..")
                                                .EnumerateFiles("*.*", System.IO.SearchOption.AllDirectories)
                                                .Where(file => file.Name.Equals("BuildVersion.tt", StringComparison.OrdinalIgnoreCase) == false &&
                                                               file.Name.Equals("BuildVersion.cs", StringComparison.OrdinalIgnoreCase) == false &&
                                                               file.Extension.ToLower().Contains(".suo") == false &&
                                                               file.Extension.ToLower().Contains(".lock") == false &&
                                                               file.Extension.ToLower().StartsWith(".ide") == false &&
                                                               file.FullName.ToLower().Contains(".csproj.user") == false &&
                                                               file.FullName.ToLower().Contains("\\generated\\") == false &&
                                                               file.FullName.Contains("\\.git\\") == false &&
                                                               file.FullName.Contains("\\.vs\\") == false &&
                                                               file.FullName.Contains("\\packages\\") == false &&
                                                               file.FullName.Contains("\\obj\\") == false &&
                                                               file.FullName.Contains("\\bin\\") == false
                                                        )
                                                .OrderByDescending(file => file.LastWriteTimeUtc);

            // calculate for each source file an MD5 sum
            var md5AllSolutionFiles = allSolutionFiles.OrderBy(fileInfo => fileInfo.FullName)
                                                    .SelectMany(fileInfo => {
                                                        using (var md5 = MD5.Create())
                                                        {
                                                            using (var stream = System.IO.File.OpenRead(fileInfo.FullName))
                                                            {
                                                                return md5.ComputeHash(stream);
                                                            }
                                                        }
                                                    })
                                                    .OfType<byte>()
                                                    .ToArray();

            // concatenate all files' MD5 sums and calculate a new total MD5 sum (which will be used to see if the solution directory content has been changed)
            string md5sum;
            using (var md5 = MD5.Create())
            {
                md5sum = BitConverter.ToString(md5.ComputeHash(md5AllSolutionFiles)).Replace("-", "");
            }

            // compare newly calculated MD5 sum with the previously calculated one
            if (md5sum != originalMD5sum)
            {
                // MD5's are different => generate new version number
                DateTime lastWriteTimeUtc = allSolutionFiles.First().LastWriteTimeUtc;
                var version = string.Format("{0}.{1}.{2}.{3}", 
                                            lastWriteTimeUtc.Year - 2000,
                                            (lastWriteTimeUtc.Month * 100 + lastWriteTimeUtc.Day) / 5,
                                            ((lastWriteTimeUtc.Month * 100 + lastWriteTimeUtc.Day) % 5) * 10000 
                                               + (lastWriteTimeUtc.Hour * 1000 + lastWriteTimeUtc.Minute * 10 + lastWriteTimeUtc.Second / 10) / 3,
                                            lastWriteTimeUtc.Minute * 100 + lastWriteTimeUtc.Second);

                Console.WriteLine("using System;");
                Console.WriteLine("using System.Reflection;");
                Console.WriteLine();
                Console.WriteLine("// AssemblyFileVersion and AssemblyVersion are based on the last UTC write time of the source file which was last modified");
                Console.WriteLine("// Last modified file is " + allSolutionFiles.First().FullName + "");
                Console.WriteLine("// MD5 of solution directory is <" + md5sum + ">");
                Console.WriteLine();
                Console.WriteLine("[assembly: AssemblyVersion(\"" + version + "\")]");
                Console.WriteLine("[assembly: AssemblyFileVersion(\"" + version + "\")]");
                Console.WriteLine("");
                Console.WriteLine("namespace Phabrico");
                Console.WriteLine("{");
                Console.WriteLine("    /// <summary>");
                Console.WriteLine("    /// Represents the application version information");
                Console.WriteLine("    /// </summary>");
                Console.WriteLine("    public static partial class VersionInfo");
                Console.WriteLine("    {");
                Console.WriteLine("        /// <summary>");
                Console.WriteLine("        /// Returns the timestamp that the application was built");
                Console.WriteLine("        /// </summary>");
                Console.WriteLine("        public static DateTime BuildDateTimeUtc");
                Console.WriteLine("        {");
                Console.WriteLine("            get");
                Console.WriteLine("            {");
                Console.WriteLine("                return new DateTime(" + DateTime.UtcNow.Ticks.ToString() + "L, DateTimeKind.Utc);");
                Console.WriteLine("            }");
                Console.WriteLine("        }");
                Console.WriteLine();
                Console.WriteLine("        /// <summary");
                Console.WriteLine("        /// Returns the application's version number");
                Console.WriteLine("        /// </summary>");
                Console.WriteLine("        public static string Version");
                Console.WriteLine("        {");
                Console.WriteLine("            get");
                Console.WriteLine("            {");
                Console.WriteLine("                return \"" + version + "\";");
                Console.WriteLine("            }");
                Console.WriteLine("        }");
                Console.WriteLine("    }");
                Console.WriteLine("}");
            }
            else
            {
                // MD5's are the same => keep original BuildVersion.cs content but change the DateTime value of the BuildDateTimeUtc property
                string newBuildVersionContent = Regex.Replace(originalBuildVersionContent, 
                                                              "return new DateTime[(][0-9]+L, DateTimeKind[.]Utc[)];", 
                                                              "return new DateTime(" + DateTime.UtcNow.Ticks.ToString() + "L, DateTimeKind.Utc);");

                Console.Write(newBuildVersionContent);
            }
        }
    }
}
