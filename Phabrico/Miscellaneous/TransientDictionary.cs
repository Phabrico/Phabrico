using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Phabrico.Miscellaneous
{
    /// <summary>
    /// Class of a TransientDictionary element
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TransientDictionaryElement<T>
    {
        public DateTime ExpirationTimestamp { get; set; }
        public T Value { get; set; }
    }

    /// <summary>
    /// Dictionary class whose elements are only temporarily held
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the dictionary</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary</typeparam>
    public class TransientDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey,TransientDictionaryElement<TValue>>>
    {
        private TimeSpan expiration;
        private Dictionary<TKey, TransientDictionaryElement<TValue>> internalDictionary;
        private bool isTouchy;

        /// <summary>
        /// Returns the number of dictionary elements
        /// </summary>
        public int Count
        {
            get
            {
                return Values.Count();
            }
        }

        /// <summary>
        /// Collection containing the keys of the dictionary
        /// </summary>
        public IEnumerable<TKey> Keys
        {
            get
            {
                if (isTouchy)
                {
                    foreach (TransientDictionaryElement<TValue> element in internalDictionary.Values)
                    {
                        element.ExpirationTimestamp = DateTime.UtcNow.Add(expiration);
                    }
                }

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
                TransientDictionaryElement<TValue>[] elements = internalDictionary.Values
                                                                                  .Where(element => element.ExpirationTimestamp > DateTime.UtcNow)
                                                                                  .ToArray();
                if (isTouchy)
                {
                    foreach (TransientDictionaryElement<TValue> element in elements)
                    {
                        element.ExpirationTimestamp = DateTime.UtcNow.Add(expiration);
                    }
                }

                return elements.Select(element => element.Value);
            }
        }

        /// <summary>
        ///  Gets or sets the value associated with the specified key.
        /// </summary>
        /// <param name="key">Name of the key to be retrieved</param>
        /// <returns></returns>
        public TValue this[TKey key]
        {
            get
            {
                RemoveExpiredElements();

                TransientDictionaryElement<TValue> element;
                if (internalDictionary.TryGetValue(key, out element))
                {
                    if (element.ExpirationTimestamp > DateTime.UtcNow)
                    {
                        if (isTouchy)
                        {
                            element.ExpirationTimestamp = DateTime.UtcNow.Add(expiration);
                        }

                        return element.Value;
                    }
                }

                throw new KeyNotFoundException();
            }

            set
            {
                RemoveExpiredElements();

                TransientDictionaryElement<TValue> element = new TransientDictionaryElement<TValue>();
                element.Value = value;
                element.ExpirationTimestamp = DateTime.UtcNow.Add(expiration);
                internalDictionary[key] = element;
            }
        }

        /// <summary>
        /// Initializes a new instance of TransientDictionary
        /// </summary>
        /// <param name="lifetime">How long the elements in the dictionary should remain</param>
        /// <param name="touchy">If true, the lifetime will start over as soon as the element is queried (i.e. ContainsKey, operation[], ...)</param>
        public TransientDictionary(TimeSpan lifetime, bool touchy)
        {
            expiration = lifetime;
            internalDictionary = new Dictionary<TKey, TransientDictionaryElement<TValue>>();
            isTouchy = touchy;
        }

        /// <summary>
        /// Clones an existing dictionary into a transient dictionary
        /// </summary>
        /// <param name="originalSafeDictionary"></param>
        public TransientDictionary(TransientDictionary<TKey, TValue> originalSafeDictionary)
        {
            internalDictionary = new Dictionary<TKey, TransientDictionaryElement<TValue>>();

            foreach (KeyValuePair<TKey, TransientDictionaryElement<TValue>> keyValuePair in originalSafeDictionary)
            {
                internalDictionary.Add(keyValuePair.Key, keyValuePair.Value);
            }
        }

        /// <summary>
        /// Determines whether the dictionary contains the specified key
        /// </summary>
        /// <param name="key">The key to locate in the dictionary</param>
        /// <returns>True if the dictionary contains an element with the specified key</returns>
        public bool ContainsKey(TKey key)
        {
            RemoveExpiredElements();

            TransientDictionaryElement<TValue> element;
            if (internalDictionary.TryGetValue(key, out element))
            {
                if (isTouchy)
                {
                    element.ExpirationTimestamp = DateTime.UtcNow.Add(expiration);
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the dictionary
        /// </summary>
        /// <returns>A enumerator structure for the dictionary</returns>
        public IEnumerator<KeyValuePair<TKey,TransientDictionaryElement<TValue>>> GetEnumerator()
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
        /// Removes the value with the specified key
        /// </summary>
        /// <param name="key">The key to locate in the dictionary</param>
        /// <returns>True if the element is successfully found and removed</returns>
        public bool Remove(TKey key)
        {
            return internalDictionary.Remove(key);
        }

        /// <summary>
        /// Deletes old elements from internal dictionary
        /// </summary>
        private void RemoveExpiredElements()
        {
            foreach (TKey expiredKey in internalDictionary.Where(kvp => kvp.Value.ExpirationTimestamp < DateTime.UtcNow)
                                                          .Select(kvp => kvp.Key)
                                                          .ToArray())
            {
                internalDictionary.Remove(expiredKey);
            }
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
            RemoveExpiredElements();

            TransientDictionaryElement<TValue> element;
            if (internalDictionary.TryGetValue(key, out element))
            {
                if (element.ExpirationTimestamp > DateTime.UtcNow)
                {
                    value = element.Value;

                    if (isTouchy)
                    {
                        element.ExpirationTimestamp = DateTime.UtcNow.Add(expiration);
                    }

                    return true;
                }
            }

            value = default(TValue);
            return false;
        }
    }
}
