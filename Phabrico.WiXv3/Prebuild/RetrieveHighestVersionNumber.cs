using System;
using System.IO;
using System.Text.RegularExpressions;

public class Program
{
    static void Main(string[] args)
    {
        string solutionDirectory = string.Join(" ", args);

        Version highestVersion = new Version("0.0.0.0");
        try
        {
            foreach (string buildVersionFile in Directory.EnumerateFiles(solutionDirectory, "BuildVersion.cs", SearchOption.AllDirectories))
            {
                string content = File.ReadAllText(buildVersionFile);

                Match matchVersionNumber = Regex.Match(content, @"\[assembly: AssemblyVersion\(""([0-9]*\.[0-9]*\.[0-9]*\.[0-9]*)""\)\]");
                if (matchVersionNumber.Success)
                {
                    Version version = new Version(matchVersionNumber.Groups[1].Value);
                    if (highestVersion < version)
                    {
                        highestVersion = version;
                    }
                }
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine("RetrieveHighestVersionNumber ( {0} ): {1}", solutionDirectory, exception.Message);
            return;
        }

        string generatedContent = "";
        generatedContent += "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n";
        generatedContent += "<Include xmlns:util=\"http://schemas.microsoft.com/wix/UtilExtension\">\r\n";
        generatedContent += "  <?define ProductVersion=\"" + highestVersion + "\" ?>\r\n";
        generatedContent += "</Include>";

        File.WriteAllText(solutionDirectory + "\\Phabrico.WiXv3\\Generated\\VersionNumber.wxi", generatedContent);
        Console.WriteLine(highestVersion);
    }
}