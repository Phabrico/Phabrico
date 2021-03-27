using System;
using System.Linq;

using Newtonsoft.Json.Linq;

namespace Phabrico.Phabricator.API
{
    /// <summary>
    /// Represents an Constraint filter used in the Phabricator Conduit API
    /// </summary>
    public class Constraint
    {
        /// <summary>
        /// Name of the constraint
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Value of the constraint
        /// </summary>
        public object Value { get; private set; }

        /// <summary>
        /// Initializes a new instance of a Constraint
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public Constraint(string name, object value)
        {
            Name = name;
            Value = value;
        }

        /// <summary>
        /// Converts one or more constraints to a JSON object
        /// </summary>
        /// <param name="constraints"></param>
        /// <returns></returns>
        public static JObject ToObject(Constraint[] constraints)
        {
            JObject result = new JObject();

            foreach (Constraint constraint in constraints)
            {
                if (constraint.Value is Array)
                {
                    object[] objectArray = constraint.Value as object[];

                    if (objectArray != null)
                    {
                        result[constraint.Name] = new JArray(objectArray.Select(item => item.ToString()));
                    }
                    else
                    {
                        result[constraint.Name] = new JArray(constraint.Value as Array);
                    }
                }
                else
                {
                    if (constraint.Value is Int32)
                    {
                        result[constraint.Name] = (JToken)(Int32)constraint.Value;
                    }
                    else
                    if (constraint.Value is Int64)
                    {
                        result[constraint.Name] = (JToken)(Int64)constraint.Value;
                    }
                    else
                    if (constraint.Value is double)
                    {
                        result[constraint.Name] = (JToken)(double)constraint.Value;
                    }
                    else
                    {
                        result[constraint.Name] = (JToken)(string)constraint.Value;
                    }
                }
            }

            return result;
        }
    }
}
