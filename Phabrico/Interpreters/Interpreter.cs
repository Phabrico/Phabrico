using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Phabrico.Interpreters
{
    /// <summary>
    /// Abstract class for Phabricator interpreter blocks
    /// </summary>
    public abstract class Interpreter
    {
        private static Dictionary<string,Interpreter> _knownInterpreters = null;

        /// <summary>
        /// Returns a dictionary of Phabricator interpreters known by Phabrico
        /// </summary>
        public static Dictionary<string, Interpreter> KnownInterpreters
        {
            get
            {
                if (_knownInterpreters == null)
                {
                    _knownInterpreters = new Dictionary<string, Interpreter>();
                    foreach (Type interpreterType in Assembly.GetExecutingAssembly().GetExportedTypes().Where(type => typeof(Phabrico.Interpreters.Interpreter).IsAssignableFrom(type)))
                    {
                        if (interpreterType == typeof(Interpreter)) continue;

                        Interpreter interpreter = interpreterType.GetConstructor(new Type[0]).Invoke(null) as Interpreter;
                        _knownInterpreters[interpreter.Name.ToLower()] = interpreter;
                    }
                }

                return _knownInterpreters;
            }
        }

        /// <summary>
        /// Name of the interpreter code
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Parses the interpreter code
        /// </summary>
        /// <param name="parameterList"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public abstract string Parse(string parameterList, string content);
    }
}
