using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Phabrico.Miscellaneous;

namespace Phabrico.Interpreters
{
    /// <summary>
    /// Interpreter to show text in a speech-bubble of a cow (or other creature)
    /// See also https://en.wikipedia.org/wiki/Cowsay
    /// </summary>
    public class Cowsay : Interpreter
    {
        /// <summary>
        /// The name of the interpreter (as used in the Remarkup syntax)
        /// </summary>
        public override string Name
        {
            get
            {
                return "cowsay";
            }
        }

        /// <summary>
        /// Translates the Cowsay interpreter code into HTML
        /// </summary>
        /// <param name="parameterList">Available parameters: cow, eye, think, tongue</param>
        /// <param name="content">The inner content of the interpreter code</param>
        /// <returns>HTML code</returns>
        public override string Parse(string parameterList, string content)
        {
            Dictionary<string,string> parameters = RegexSafe.Split(parameterList, ", *")
                                                            .ToDictionary(key => key.Split('=')[0].Trim(), 
                                                                          value => value.Contains('=') ? value.Split('=')[1] : null);

            string cow;
            string eye;
            string think;
            string tongue;

            if (parameters.TryGetValue("cow", out cow) == false)
            {
                cow = "default";
            }
            if (parameters.TryGetValue("eye", out eye) == false)
            {
                eye = "oo";
            }
            if (parameters.TryGetValue("think", out think) == false)
            {
                think = "say";
            }
            if (parameters.TryGetValue("tongue", out tongue) == false)
            {
                tongue = "  ";
            }

            string cowTemplate;
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = string.Format("Phabrico.Interpreters.Cows.{0}.cow", cow.ToLower());
            Stream stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                resourceName = string.Format("Phabrico.Interpreters.Cows.default.cow", cow.ToLower());
                stream = assembly.GetManifestResourceStream(resourceName);
            }

            using (StreamReader reader = new StreamReader(stream))
            {
                cowTemplate = reader.ReadToEnd();
            }

            stream.Dispose();

            // remove comment lines
            cowTemplate = string.Join("\n", cowTemplate.Split('\n').Where(line => line.StartsWith("#") == false));

            // remove first and 2 last lines
            cowTemplate = string.Join("\n", cowTemplate.Split('\n').Take(cowTemplate.Split('\n').Length - 2).Skip(1));

            // parse escaping backslashes
            cowTemplate = RegexSafe.Replace(cowTemplate, "\\\\(.)", "$1");

            // fill in parameters
            cowTemplate = cowTemplate.Replace("$eyes", eye.PadRight(2).Substring(0, 2));
            cowTemplate = cowTemplate.Replace("${eyes}", eye.PadRight(2).Substring(0, 2));
            cowTemplate = cowTemplate.Replace("$tongue", tongue.PadRight(2).Substring(0, 2));
            cowTemplate = cowTemplate.Replace("${tongue}", tongue.PadRight(2).Substring(0, 2));
            cowTemplate = cowTemplate.Replace("$thoughts", think == "think" ? "o" : "\\");
            cowTemplate = cowTemplate.Replace("${thoughts}", think == "think" ? "o" : "\\");

            // parse content to create speech-bubble
            string speechBubble = "";
            string[] textLines = content.Trim().Split(new string[] { "\r\n" }, System.StringSplitOptions.None );
            int widthSpeechBubble = textLines.Max(line => line.Length);
            speechBubble = " " + new string('_', widthSpeechBubble + 2) + "<br>";

            // first line
            if (think == "think")
            {
                speechBubble += "( ";
            }
            else
            {
                if (textLines.Length < 2)
                {
                    speechBubble += "&lt; ";
                }
                else
                {
                    speechBubble += "/ ";
                }
            }

            if (textLines.Any())
            {
                speechBubble += textLines[0].PadRight(widthSpeechBubble);
            }

            if (think == "think")
            {
                speechBubble += " )<br>";
            }
            else
            {
                if (textLines.Length < 2)
                {
                    speechBubble += " &gt;<br>";
                }
                else
                {
                    speechBubble += " \\<br>";
                }
            }

            // middle lines
            if (textLines.Length > 2)
            {
                foreach (string line in textLines.Take(textLines.Length - 1).Skip(1))
                {
                    if (think == "think")
                    {
                        speechBubble += "( " + line.PadRight(widthSpeechBubble) + " )<br>";
                    }
                    else
                    {
                        speechBubble += "| " + line.PadRight(widthSpeechBubble) + " |<br>";
                    }
                }
            }

            // last line
            if (textLines.Length > 1)
            {
                if (think == "think")
                {
                    speechBubble += "( " + textLines.Last().PadRight(widthSpeechBubble) + " )<br>";
                }
                else
                {
                    speechBubble += "\\ " + textLines.Last().PadRight(widthSpeechBubble) + " /<br>";
                }
            }

            speechBubble += " " + new string('-', widthSpeechBubble + 2) + "<br>";

            return "<pre>" + speechBubble + cowTemplate + "</pre>";
        }
    }
}
