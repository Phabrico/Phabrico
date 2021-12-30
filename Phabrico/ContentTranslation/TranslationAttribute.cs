using System;

namespace Phabrico.ContentTranslation
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TranslationAttribute : Attribute
    {
        public string Name { get; set; }
    }
}
