using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Phabrico.Miscellaneous
{
    [JsonConverter(typeof(LanguageToStringConverter))]
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
        /// <param name="otherLanguage"></param>
        /// <returns></returns>
        public bool Equals(Language otherLanguage)
        {
            return language.Equals(otherLanguage.language);
        }

        /// <summary>
        /// Compares 2 languages
        /// </summary>
        /// <param name="otherString"></param>
        /// <returns></returns>
        public bool Equals(string otherString)
        {
            Language otherLanguage = otherString;
            return language.Equals(otherLanguage.language);
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

    /// <summary>
    /// Json converter class for converting the Language object into a string while serializing
    /// </summary>
    public class LanguageToStringConverter : JsonConverter
    {
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JToken jt = JValue.ReadFrom(reader);

            if (jt.Type == JTokenType.String)
            {
                return (Language)jt.Value<string>();
            }
            else
            {
                return Language.NotApplicable;
            }
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(Language).Equals(objectType);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value.ToString());
        }
    }
}
