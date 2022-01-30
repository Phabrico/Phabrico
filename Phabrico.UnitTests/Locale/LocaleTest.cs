using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phabrico.Miscellaneous;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Phabrico.UnitTests.Locale
{
    [TestClass]
    public class LocaleTest : UnitTests.PhabricoUnitTest
    {
        /// <summary>
        /// Checks if the locale file contains errors.
        /// This method is only used for unit-testing
        /// </summary>
        /// <param name="locale">Lanuage code of locale file to be validated</param>
        /// <param name="poContent">Content of .po file to be validated</param>
        /// <returns>True if no errors found</returns>
        public static bool ValidateLocaleContent(string poContent)
        {
            string[] duplicatedMsgIDs = RegexSafe.Matches(poContent, "^msgid +\"([^\"]*)\"\r?\nmsgstr +\"([^\"]*)", RegexOptions.Multiline)
                                                    .OfType<Match>()
                                                    .GroupBy(g => g.Groups[1].Value)
                                                    .Where(g => g.Count() > 1)
                                                    .Select(g => g.Key)
                                                    .ToArray();
            return duplicatedMsgIDs.Any() == false;
        }

        /// <summary>
        /// This test will read the content of all the Views and tries to translate them by means of the 'en' locale file.
        /// </summary>
        [TestMethod]
        public void TestCheckIfEverythingIsTranslated()
        {
            Initialize();

            Assembly phabrico = Assembly.LoadFrom("Phabrico.exe");
            Type locale = phabrico.GetType("Phabrico.Miscellaneous.Locale");
            MethodInfo translateHTML = locale.GetMethod("TranslateHTML", new Type[] { typeof(string), typeof(Language), typeof(List<string>).MakeByRefType() });

            Dictionary<string, string> views = phabrico.GetManifestResourceNames()
                                                       .Where(resource => resource.StartsWith("Phabrico.View.", System.StringComparison.OrdinalIgnoreCase))
                                                       .ToDictionary( key => key,
                                                                      value =>
                                                                      {
                                                                          using (Stream stream = phabrico.GetManifestResourceStream(value))
                                                                          {
                                                                              using (StreamReader reader = new StreamReader(stream))
                                                                              {
                                                                                  return reader.ReadToEnd();
                                                                              }
                                                                          }
                                                                      }
                                                                    );

            foreach (string pluginDll in System.IO.Directory.EnumerateFiles(".", "Phabrico.Plugin.*.dll"))
            {
                Assembly pluginAssembly = Assembly.LoadFrom(pluginDll);
                List<string> pluginViews = pluginAssembly.GetManifestResourceNames()
                                                             .Where(resource => resource.IndexOf(".View.", 0, System.StringComparison.OrdinalIgnoreCase) != -1)
                                                             .ToList();
                foreach (string view in pluginViews)
                {
                    using (Stream stream = pluginAssembly.GetManifestResourceStream(view))
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            views[view] = reader.ReadToEnd();
                        }
                    }
                }
            }

            foreach (KeyValuePair<string, string> view in views)
            {
                object[] parametersTranslateHTML = new object[] { view.Value, (Language)"en", null };
                translateHTML.Invoke(null, parametersTranslateHTML);

                List<string> missingTranslations = parametersTranslateHTML[2] as List<string>;
                Assert.IsFalse(missingTranslations?.Any(),
                              string.Format("No translation found for '{0}' in '{1}'",
                                             missingTranslations.FirstOrDefault(),
                                             view.Key
                                           )
                );
            }
        }

        /// <summary>
        /// This test will compare the different po locale files and check if they all have the same msgid's in it
        /// </summary>
        [TestMethod]
        public void TestCheckIfAllLocaleFilesAreSynchronized()
        {
            Initialize();

            Assembly phabrico = Assembly.LoadFrom("Phabrico.exe");
            Type locale = phabrico.GetType("Phabrico.Miscellaneous.Locale");

            // retrieve language codes from Phabrico
            string[] phabricoLocaleResourceNames = phabrico.GetManifestResourceNames()
                                                           .Where(resource => resource.StartsWith("Phabrico.Locale.Phabrico_", StringComparison.OrdinalIgnoreCase))
                                                           .ToArray();
            List<string> languageCodes = phabricoLocaleResourceNames.Where(resource => resource.StartsWith("Phabrico.Locale.Phabrico_", StringComparison.OrdinalIgnoreCase))
                                                                    .Select(resource => resource.Substring("Phabrico.Locale.Phabrico_".Length))
                                                                    .Select(resource => resource.Substring(0, resource.Length - ".po".Length))
                                                                    .ToList();

            // retrieve plugin assemblies
            Assembly[] pluginAssemblies = System.IO.Directory.EnumerateFiles(".", "Phabrico.Plugin.*.dll")
                                                             .Select(assemblyName => Assembly.LoadFrom(assemblyName))
                                                             .ToArray();

            // retrieve language codes from plugins
            foreach (Assembly pluginAssembly in pluginAssemblies)
            {
                languageCodes.AddRange( pluginAssembly.GetManifestResourceNames()
                                                      .Where(resource => resource.IndexOf(".Locale.Phabrico_", 0, StringComparison.OrdinalIgnoreCase) != -1)
                                                      .Select(resource => resource.Substring("Phabrico.Plugin.Locale.phabrico_".Length))
                                                      .Select(resource => resource.Substring(0, resource.Length - ".po".Length))
                                      );
            }

            languageCodes = languageCodes.Distinct().ToList();

            // == start verifying Phabrico assembly ================================================================================================================
            // validate the first locale file (the other locale files will be compared with the 1st locale files)
            string poContent;
            using (Stream stream = phabrico.GetManifestResourceStream(phabricoLocaleResourceNames[0]))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    poContent = reader.ReadToEnd();
                }
            }

            Assert.IsTrue( ValidateLocaleContent(poContent),
                           string.Format("The locale file for language '{0}' contains some errors",
                                           languageCodes.FirstOrDefault()
                                       )
            );

            // == start verifying plugin assemblies ================================================================================================================
            foreach (Assembly pluginAssembly in pluginAssemblies)
            {
                foreach (string poFileName in pluginAssembly.GetManifestResourceNames()
                                                            .Where(resource => resource.IndexOf(".Locale.Phabrico_", 0, StringComparison.OrdinalIgnoreCase) != -1))
                {

                    using (Stream stream = pluginAssembly.GetManifestResourceStream(poFileName))
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            poContent = reader.ReadToEnd();
                        }
                    }

                    Assert.IsTrue(ValidateLocaleContent(poContent),
                                   string.Format("The locale file for language '{0}' contains some errors",
                                                   languageCodes.FirstOrDefault()
                                               )
                    );
                }
            }

            MethodInfo readLocaleFile = locale.GetMethod("ReadLocaleFile");
            DictionarySafe<string,string> firstTranslations = readLocaleFile.Invoke(null, new object[] { (Language)languageCodes.FirstOrDefault() }) as DictionarySafe<string,string>;

            foreach (string otherLanguageCode in languageCodes.Skip(1))
            {
                DictionarySafe<string,string> otherTranslations = readLocaleFile.Invoke(null, new object[] { (Language)otherLanguageCode }) as DictionarySafe<string,string>;

                foreach (string msgid in firstTranslations?.Keys)
                {
                    Assert.IsTrue(otherTranslations?.ContainsKey(msgid),
                                  string.Format("'{0}' is translated in '{1}' but not in '{2}'", 
                                                 msgid, languageCodes.FirstOrDefault(), otherLanguageCode
                                               )
                    );
                }

                foreach (string msgid in otherTranslations?.Keys)
                {
                    Assert.IsTrue(firstTranslations.ContainsKey(msgid),
                                  string.Format("'{0}' is translated in '{1}' but not in '{2}'", 
                                                 msgid, otherLanguageCode, languageCodes.FirstOrDefault()
                                               )
                    );
                }
            }
        }

        /// <summary>
        /// This test will compare the different po locale files and check if they all have the same msgid's in it
        /// </summary>
        [TestMethod]
        public void TestValidateReferencedParameters()
        {
            Initialize();

            Assembly phabrico = Assembly.LoadFrom("Phabrico.exe");
            Type locale = phabrico.GetType("Phabrico.Miscellaneous.Locale");

            // retrieve language codes from Phabrico
            string[] phabricoLocaleResourceNames = phabrico.GetManifestResourceNames()
                                                           .Where(resource => resource.StartsWith("Phabrico.Locale.Phabrico_", StringComparison.OrdinalIgnoreCase))
                                                           .ToArray();
            List<string> languageCodes = phabricoLocaleResourceNames.Where(resource => resource.StartsWith("Phabrico.Locale.Phabrico_", StringComparison.OrdinalIgnoreCase))
                                                                    .Select(resource => resource.Substring("Phabrico.Locale.Phabrico_".Length))
                                                                    .Select(resource => resource.Substring(0, resource.Length - ".po".Length))
                                                                    .ToList();

            // retrieve plugin assemblies
            Assembly[] pluginAssemblies = System.IO.Directory.EnumerateFiles(".", "Phabrico.Plugin.*.dll")
                                                             .Select(assemblyName => Assembly.LoadFrom(assemblyName))
                                                             .ToArray();

            // retrieve language codes from plugins
            foreach (Assembly pluginAssembly in pluginAssemblies)
            {
                languageCodes.AddRange( pluginAssembly.GetManifestResourceNames()
                                                      .Where(resource => resource.IndexOf(".Locale.Phabrico_", 0, StringComparison.OrdinalIgnoreCase) != -1)
                                                      .Select(resource => resource.Substring("Phabrico.Plugin.Locale.phabrico_".Length))
                                                      .Select(resource => resource.Substring(0, resource.Length - ".po".Length))
                                      );
            }

            languageCodes = languageCodes.Distinct().ToList();


            MethodInfo readLocaleFile = locale.GetMethod("ReadLocaleFile");
            DictionarySafe<string,string> englishTranslations = readLocaleFile.Invoke(null, new object[] { (Language)"en" }) as DictionarySafe<string,string>;

            foreach (string languageCode in languageCodes.Where(lang => lang.Equals("en") == false))
            {
                DictionarySafe<string,string> otherTranslations = readLocaleFile.Invoke(null, new object[] { (Language)languageCode }) as DictionarySafe<string,string>;

                foreach (string msgid in englishTranslations?.Keys)
                {
                    string[] parametersInEnglish = Regex.Matches(englishTranslations[msgid], "@@[^@]+@@")
                                                      .OfType<Match>()
                                                      .Select(m => m.Value)
                                                      .OrderBy(p => p)
                                                      .ToArray();
                    string[] parametersInOther = Regex.Matches(otherTranslations[msgid], "@@[^@]+@@")
                                                       .OfType<Match>()
                                                       .Select(m => m.Value)
                                                       .OrderBy(p => p)
                                                       .ToArray();
                    Assert.IsTrue(parametersInEnglish.SequenceEqual(parametersInOther), msgid + " (" + languageCode + "): not all parameters are translated");
                    Assert.IsTrue(englishTranslations[msgid].Count(ch => ch == '@').Equals(otherTranslations[msgid].Count(ch => ch == '@')), msgid + " (" + languageCode + "): one or more parameters are incorrectly formatted");
                }
            }
        }
    }
}
