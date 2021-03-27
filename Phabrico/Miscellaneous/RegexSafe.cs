using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Phabrico.Miscellaneous
{
    /// <summary>
    /// The standard static .NET Regex methods leak some memory.
    /// This class will prevent this.
    /// </summary>
    public class RegexSafe
    {
        private static RegexSafe  _instance = null;

        /// <summary>
        /// Instance object for thread safety
        /// </summary>
        private static RegexSafe Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new RegexSafe();
                }

                return _instance;
            }
        }

        /// <summary>
        /// private collection of all used regular expressions
        /// </summary>
        private Dictionary<string, Regex> regularExpressions = new Dictionary<string, Regex>();

        /// <summary>
        /// Indicates whether the specified regular expression finds a match in the specified input string.
        /// </summary>
        /// <param name="input">The string to search for a match.</param>
        /// <param name="pattern">The regular expression pattern to match.</param>
        /// <param name="options">A bitwise combination of the enumeration values that provide options for matching.</param>
        /// <returns>true if the regular expression finds a match; otherwise, false.</returns>
        public static bool IsMatch(string input, string pattern, RegexOptions options)
        {
            Regex regex;

            if (Instance.regularExpressions.TryGetValue(pattern + options, out regex) == false)
            {
                regex = new Regex(pattern, options);
                Instance.regularExpressions[pattern + options] = regex;
            }

            return regex.IsMatch(input);
        }

        /// <summary>
        /// Searches the input string for the first occurrence of the specified regular expression, using the specified matching options.
        /// </summary>
        /// <param name="input">The string to search for a match.</param>
        /// <param name="pattern">The regular expression pattern to match.</param>
        /// <param name="options">A bitwise combination of the enumeration values that provide options for matching.</param>
        /// <returns>An object that contains information about the match.</returns>
        public static Match Match(string input, string pattern, RegexOptions options)
        {
            Regex regex;

            if (Instance.regularExpressions.TryGetValue(pattern + options, out regex) == false)
            {
                regex = new Regex(pattern, options);
                Instance.regularExpressions[pattern + options] = regex;
            }

            return regex.Match(input);
        }

        /// <summary>
        /// Searches the specified input string for all occurrences of a specified regular expression.
        /// </summary>
        /// <param name="input">The string to search for a match.</param>
        /// <param name="pattern">The regular expression pattern to match.</param>
        /// <param name="regexOptions">A bitwise combination of the enumeration values that specify options for matching.</param>
        /// <returns>
        /// A collection of the System.Text.RegularExpressions.Match objects found by the search.
        /// If no matches are found, the method returns an empty collection object.
        /// </returns>
        public static MatchCollection Matches(string input, string pattern, RegexOptions regexOptions = RegexOptions.None)
        {
            Regex regex;

            if (Instance.regularExpressions.TryGetValue(pattern + ((int)regexOptions).ToString(), out regex) == false)
            {
                regex = new Regex(pattern, regexOptions);
                Instance.regularExpressions[pattern + ((int)regexOptions).ToString()] = regex;
            }

            return regex.Matches(input);
        }

        /// <summary>
        /// In a specified input string, replaces all strings that match a specified regular expression 
        /// with a specified replacement string.
        /// </summary>
        /// <param name="input">The string to search for a match.</param>
        /// <param name="pattern">The regular expression pattern to match.</param>
        /// <param name="replacement">The replacement string.</param>
        /// <returns>
        /// A new string that is identical to the input string, except that the replacement string takes the place 
        /// of each matched string. If pattern is not matched in the current instance, the method returns the current instance 
        /// unchanged.
        /// </returns>
        public static string Replace(string input, string pattern, string replacement)
        {
            Regex regex;

            if (Instance.regularExpressions.TryGetValue(pattern, out regex) == false)
            {
                regex = new Regex(pattern);
                Instance.regularExpressions[pattern] = regex;
            }

            return regex.Replace(input, replacement);
        }

        /// <summary>
        /// In a specified input string, replaces all strings that match a specified regular expression 
        /// with a specified replacement string. Specified options modify the matching operation.
        /// </summary>
        /// <param name="input">The string to search for a match.</param>
        /// <param name="pattern">The regular expression pattern to match.</param>
        /// <param name="replacement">The replacement string.</param>
        /// <param name="options">A bitwise combination of the enumeration values that provide options for matching.</param>
        /// <returns>
        /// A new string that is identical to the input string, except that the replacement string takes the place 
        /// of each matched string. If pattern is not matched in the current instance, the method returns the current instance 
        /// unchanged.
        /// </returns>
        public static string Replace(string input, string pattern, string replacement, RegexOptions options)
        {
            Regex regex;

            if (Instance.regularExpressions.TryGetValue(pattern + options, out regex) == false)
            {
                regex = new Regex(pattern, options);
                Instance.regularExpressions[pattern + options] = regex;
            }

            return regex.Replace(input, replacement);
        }

        /// <summary>
        /// In a specified input string, replaces all substrings that match a specified regular expression with a string returned by a 
        /// System.Text.RegularExpressions.MatchEvaluator delegate. Additional parameters specify options that modify the matching operation and a 
        /// time-out interval if no match is found.
        /// </summary>
        /// <param name="input">The string to search for a match.</param>
        /// <param name="pattern">The regular expression pattern to match.</param>
        /// <param name="evaluator">A custom method that examines each match and returns either the original matched string or a replacement string.</param>
        /// <param name="options">A bitwise combination of enumeration values that provide options for matching.</param>
        /// <returns>A new string that is identical to the input string, except that the replacement string takes the place of each matched string. If pattern is not matched in the current instance, the method returns the current instance unchanged.</returns>
        public static string Replace(string input, string pattern, MatchEvaluator evaluator, RegexOptions options = RegexOptions.None)
        {
            Regex regex;

            if (Instance.regularExpressions.TryGetValue(pattern + options, out regex) == false)
            {
                regex = new Regex(pattern, options);
                Instance.regularExpressions[pattern + options] = regex;
            }
            
            return regex.Replace(input, evaluator);
        }

        /// <summary>
        /// Splits an input string into an array of substrings at the positions defined by
        /// a specified regular expression pattern. Additional parameters specify options
        /// that modify the matching operation.
        /// </summary>
        /// <param name="input">The string to split.</param>
        /// <param name="pattern">The regular expression pattern to match.</param>
        /// <param name="options">A bitwise combination of the enumeration values that provide options for matching.</param>
        /// <returns>A string array</returns>
        public static string[] Split(string input, string pattern, RegexOptions options = RegexOptions.None)
        {
            Regex regex;

            if (Instance.regularExpressions.TryGetValue(pattern + options, out regex) == false)
            {
                regex = new Regex(pattern, options);
                Instance.regularExpressions[pattern + options] = regex;
            }

            return regex.Split(input);
        }
    }
}
