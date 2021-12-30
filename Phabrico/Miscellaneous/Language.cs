using System.Collections.Generic;

namespace Phabrico.Miscellaneous
{
    public class Language
    {
        private const string _defaultLanguageCode = "en";
        private const string _notApplicableCode = "(Not Applicable)";

        private string language = _defaultLanguageCode;

        public static Language Default = _defaultLanguageCode;
        public static Language NotApplicable = _notApplicableCode;

        /// <summary>
        /// Convert string value to Language object
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        static public implicit operator Language(string value)
        {
            return new Language()
            {
                language = value ?? _defaultLanguageCode
            };
        }

        /// <summary>
        /// Convert Language object to string value
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        static public implicit operator string(Language value)
        {
            if (value == null) return null;

            return value.ToString();
        }

        /// <summary>
        /// Compares 2 languages
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public override bool Equals(object other)
        {
            if (other is Language)
            {
                return language.Equals(((Language)other).language);
            }
            else
            if (other is string)
            {
                return language.Equals(((Language)(string)other).language);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Serves as the default hash function
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return 1021829811 + EqualityComparer<string>.Default.GetHashCode(language);
        }

        /// <summary>
        /// Convert Language object to string value
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return language;
        }
    }
}
