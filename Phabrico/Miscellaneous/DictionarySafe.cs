using System.Collections;
using System.Collections.Generic;

namespace Phabrico.Miscellaneous
{
    /// <summary>
    /// A Dictionary which will return default values for inexistant keys instead of throwing a KeyNotFoundException
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the dictionary</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary</typeparam>
    public class DictionarySafe<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        Dictionary<TKey, TValue> internalDictionary;

        /// <summary>
        /// Collection containing the keys of the dictionary
        /// </summary>
        public IEnumerable<TKey> Keys
        {
            get
            {
                return internalDictionary.Keys;
            }
        }

        /// <summary>
        /// Collection containing the keys of the dictionary
        /// </summary>
        public IEnumerable<TValue> Values
        {
            get
            {
                return internalDictionary.Values;
            }
        }

        /// <summary>
        ///  Gets or sets the value associated with the specified key.
        ///  If a key is not found in the collection, a default value (depending on the type of TValue) will be returned
        /// </summary>
        /// <param name="key">Name of the key to be retrieved</param>
        /// <returns></returns>
        public TValue this[TKey key]
        {
            get
            {
                TValue value;
                if (internalDictionary.TryGetValue(key, out value) == false)
                {
                    if (typeof(TValue).IsGenericType && typeof(TValue).GetGenericTypeDefinition() == GetType().GetGenericTypeDefinition())
                    {
                        // value is another DictionarySafe
                        value = (TValue)typeof(TValue).GetConstructors()[0].Invoke(new object[0]);
                    }
                    else
                    {
                        value = default(TValue);
                    }
                }

                return value;
            }

            set
            {
                internalDictionary[key] = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of DictionarySafe
        /// </summary>
        public DictionarySafe()
        {
            internalDictionary = new Dictionary<TKey, TValue>();
        }

        /// <summary>
        /// Clones an existing dictionary into a safe dictionary
        /// </summary>
        /// <param name="originalSafeDictionary"></param>
        public DictionarySafe(DictionarySafe<TKey, TValue> originalSafeDictionary)
        {
            internalDictionary = new Dictionary<TKey, TValue>();

            foreach (KeyValuePair<TKey, TValue> keyValuePair in originalSafeDictionary)
            {
                internalDictionary.Add(keyValuePair.Key, keyValuePair.Value);
            }
        }

        /// <summary>
        /// Converts a System.Net.HttpListenerContext object implicitly into a Phabrico.Miscellaneous.HttpListenerContext object
        /// </summary>
        /// <param name="dictionary"></param>
        public static implicit operator DictionarySafe<TKey,TValue>(Dictionary<TKey,TValue> dictionary)
        {
            DictionarySafe<TKey, TValue> newDictionary = new DictionarySafe<TKey,TValue>();
            newDictionary.internalDictionary = dictionary;
            return newDictionary;
        }

        /// <summary>
        /// Determines whether the dictionary contains the specified key
        /// </summary>
        /// <param name="key">The key to locate in the dictionary</param>
        /// <returns>True if the dictionary contains an element with the specified key</returns>
        public bool ContainsKey(TKey key)
        {
            return internalDictionary.ContainsKey(key);
        }

        /// <summary>
        /// Removes the value with the specified key
        /// </summary>
        /// <param name="key">The key to locate in the dictionary</param>
        /// <returns>True if the element is successfully found and removed</returns>
        public bool Remove(TKey key)
        {
            return internalDictionary.Remove(key);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the dictionary
        /// </summary>
        /// <returns>A enumerator structure for the dictionary</returns>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return internalDictionary.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the dictionary
        /// </summary>
        /// <returns>A enumerator structure for the dictionary</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return internalDictionary.GetEnumerator();
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value"> When this method returns, contains the value associated with the specified key,
        /// if the key is found; otherwise, the default value for the type of the value parameter.
        /// This parameter is passed uninitialized.</param>
        /// <returns>true if the dictionary contains an element with
        ///  the specified key; otherwise, false.</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            return internalDictionary.TryGetValue(key, out value);
        }
    }
}
