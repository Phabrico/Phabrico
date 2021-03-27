using System.Collections.Generic;

namespace Phabrico.Storage
{
    /// <summary>
    /// Super class for all Storage classes
    /// </summary>
    public class PhabricatorObject
    {
        /// <summary>
        /// protected constructor
        /// </summary>
        protected PhabricatorObject()
        {
        }

        /// <summary>
        /// This synchronization lock object prevents some methods from being run simultaneously.
        /// The next invocation of a method will only be executed when the previous one has left the lock(ReentrancyLock) statement
        /// </summary>
        protected static object ReentrancyLock = new object();
    }

    /// <summary>
    /// Abstract class for all Storage classes
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class PhabricatorObject<T> : PhabricatorObject
    {
        /// <summary>
        /// Adds or modifies a new database record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="phabricatorObject"></param>
        public abstract void Add(Database database, T phabricatorObject);

        /// <summary>
        /// Returns all records
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public abstract IEnumerable<T> Get(Database database);

        /// <summary>
        /// Returns a specific record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="key"></param>
        /// <param name="ignoreStageData"></param>
        /// <returns></returns>
        public abstract T Get(Database database, string key, bool ignoreStageData);
    }
}
