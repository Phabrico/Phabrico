using Phabrico.Miscellaneous;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Phabrico.Interpreters
{
    /// <summary>
    /// Interpreter to show text in a banner format
    /// See also https://en.wikipedia.org/wiki/FIGlet
    /// </summary>
    public class Figlet : Interpreter
    {
        private static readonly object synchronizationDictionaries = new object();

        /// <summary>
        /// Dictionaries of fonts in use (key=font name, values=font character dictionary).
        /// The font character dictionary is layouted as follows:
        /// - key = character
        /// - values = list of ASCII lines which represent the given character as a whole
        /// These dictionaries are generated via the Parse method using the embedded FLF font files in Interpreters\FigletFonts directory
        /// </summary>
        private static readonly Dictionary<string, Dictionary<char, List<string>>> _fontCharacterDictionary = new Dictionary<string, Dictionary<char, List<string>>>();

        /// <summary>
        /// FLF layout setting per font
        /// The FLF layout setting is the 5th field of the FLF header (1st line of file).
        /// It identifies if a character can be moved closer to the previous one (layout setting >= 0) or not
        /// </summary>
        private static readonly Dictionary<string, int> _fontLayoutDictionary = new Dictionary<string, int>();

        /// <summary>
        /// The name of the interpreter (as used in the Remarkup syntax)
        /// </summary>
        public override string Name
        {
            get
            {
                return "figlet";
            }
        }

        /// <summary>
        /// Translates the Figlet interpreter code into HTML
        /// </summary>
        /// <param name="parameterList">Available parameters: font</param>
        /// <param name="content">The inner content of the interpreter code</param>
        /// <returns>HTML code</returns>
        public override string Parse(string parameterList, string content)
        {
            Dictionary<string, string> parameters = RegexSafe.Split(parameterList, ", *")
                                                             .ToDictionary(key => key.Split('=')[0].Trim(),
                                                                           value => value.Contains('=') ? value.Split('=')[1] : null);
            string font;
            int layoutSetting;
            if (parameters.TryGetValue("font", out font) == false)
            {
                font = "standard";
            }

            Dictionary<char, List<string>> characterTranslations;
            lock (synchronizationDictionaries)
            {
                if (_fontCharacterDictionary.TryGetValue(font, out characterTranslations) == false)
                {
                    characterTranslations = new Dictionary<char, List<string>>();
                    _fontCharacterDictionary[font] = characterTranslations;

                    string fontTemplate;
                    Assembly assembly = Assembly.GetExecutingAssembly();
                    string resourceName = string.Format("Phabrico.Interpreters.FigletFonts.{0}.flf", font.ToLower());
                    Stream stream = assembly.GetManifestResourceStream(resourceName);
                    try
                    {
                        if (stream == null)
                        {
                            resourceName = "Phabrico.Interpreters.FigletFonts.standard.flf";
                            stream = assembly.GetManifestResourceStream(resourceName);
                        }

                        using (StreamReader reader = new StreamReader(stream, System.Text.Encoding.Default))
                        {
                            fontTemplate = reader.ReadToEnd();
                        }
                    }
                    finally
                    {
                        if (stream != null)
                        {
                            stream.Dispose();
                        }
                    }

                    string[] lines = fontTemplate.Split('\n');
                    if (lines.Any() == false)
                    {
                        return "<pre>Invalid font</pre>";
                    }

                    string[] headerFields = lines[0].Split();
                    string signature = headerFields[0];
                    if (signature.StartsWith("flf2a") == false)
                    {
                        return "<pre>Invalid font</pre>";
                    }

                    int height = Int32.Parse(headerFields[1]);
                    layoutSetting = Int32.Parse(headerFields[4]);

                    _fontLayoutDictionary[font] = layoutSetting;

                    for (int c = 32; c <= 255; c++)
                    {
                        characterTranslations[(char)c] = new List<string>();
                    }

                    // skip comments
                    lines = lines.SkipWhile(line => line.EndsWith("@") == false)
                                 .ToArray();

                    // process standard ASCII codes
                    string[] asciiCharLines = lines.TakeWhile(line => line.EndsWith("@") == true || line.EndsWith("@#") == true)
                                                   .ToArray();
                    for (int lineNumber = 0; lineNumber < asciiCharLines.Length; lineNumber++)
                    {
                        char character = (char)(32 + (lineNumber / height));
                        characterTranslations[character].Add(string.Join("", asciiCharLines[lineNumber].TakeWhile(c => c != '@')
                                                                        ));
                    }

                    // process non-standard ASCII codes
                    lines = lines.Skip(asciiCharLines.Length)
                                 .ToArray();

                    for (int lineNumber = 0; lineNumber < lines.Length; lineNumber += height + 1)
                    {
                        try
                        {
                            char selectedNonStandardCharacter;
                            string charDefinition = lines[lineNumber].Split().First();
                            if (string.IsNullOrEmpty(charDefinition)) break;

                            if (charDefinition.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                            {
                                selectedNonStandardCharacter = (char)Convert.ToUInt16(charDefinition.Substring(2), 16);
                            }
                            else
                            {
                                selectedNonStandardCharacter = (char)Convert.ToUInt16(charDefinition);
                            }

                            characterTranslations[selectedNonStandardCharacter] = new List<string>();
                            for (int charLine = 1; charLine <= height; charLine++)
                            {
                                characterTranslations[selectedNonStandardCharacter].Add(string.Join("", lines[charLine + lineNumber].TakeWhile(c => c != '@')
                                                                                                   ));
                            }
                        }
                        catch
                        {
                            break;
                        }
                    }

                    foreach (char character in characterTranslations.Keys)
                    {
                        if (characterTranslations[character].All(charLine => charLine.StartsWith(" ")) &&
                            characterTranslations[character].Any()
                           )
                        {
                            for (int charLineIndex = 0; charLineIndex < height; charLineIndex++)
                            {
                                characterTranslations[character][charLineIndex] = characterTranslations[character][charLineIndex].Substring(1);
                            }
                        }
                    }
                }
            }

            // newlines are not processed: remove them
            content = content.Replace("\r", "").Replace("\n", "");

            string result = " ";
            int fontHeight = characterTranslations[' '].Count;
            layoutSetting = _fontLayoutDictionary[font];
            for (int line = 0; line < fontHeight; line++)
            {
                int charIndex = -1;
                foreach (char character in content)
                {
                    charIndex++;

                    string translation = characterTranslations[character][line];

                    // identify end of character line with a character-specific separator
                    translation = translation.Replace("$$", "  ");
                    translation = translation.Replace("$", string.Format("${0:X4}", charIndex));

                    result = result + System.Web.HttpUtility.HtmlEncode(translation);
                }

                result += "<br> ";
            }

            // remove character-specific separators and move next character closer to previous character (if allowed)
            foreach (string characterEndSeparator in RegexSafe.Matches(result, "[$][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]").OfType<Match>().Select(m => m.Value).Distinct().ToList())
            {
                if (layoutSetting >= 0)
                {
                    int nbrCharacterSeparators = result.Split(new string[] { characterEndSeparator }, StringSplitOptions.None).Count() - 1;
                    while (result.Split(new string[] { characterEndSeparator + " " }, StringSplitOptions.None).Count() - 1 == nbrCharacterSeparators)
                    {
                        result = result.Replace(characterEndSeparator + " ", characterEndSeparator);
                    }
                }

                result = result.Replace(characterEndSeparator, " ");
            }

            return "<pre>" + result + "</pre>";
        }
    }
}
