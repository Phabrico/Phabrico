using System;

namespace Phabrico.Miscellaneous
{
    public class BooleanVector<T>
    {
        private Func<T, bool> booleanMethod { get; set;} = null;

        internal T Data = default(T);

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="booleanVectorMethod"></param>
        /// <exception cref="ArgumentException"></exception>
        public BooleanVector(Func<T, bool> booleanVectorMethod)
        {
            if (booleanVectorMethod == null) throw new ArgumentException("booleanVectorMethod can not be null");

            booleanMethod = booleanVectorMethod;
        }

        /// <summary>
        /// Convert boolean value to BooleanVector object
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        static public implicit operator BooleanVector<T>(bool value)
        {
            return new BooleanVector<T>(bv => value);
        }


        /// <summary>
        /// Convert BooleanVector object to bool value
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        static public implicit operator bool(BooleanVector<T> value)
        {
            if (value == null) return false;

            return value.booleanMethod(value.Data);
        }

        /// <summary>
        /// Compares a BooleanVector with a boolean value
        /// </summary>
        /// <param name="booleanValue"></param>
        /// <returns></returns>
        public bool Equals(bool booleanValue)
        {
            return booleanMethod(Data) == booleanValue;
        }

        /// <summary>
        /// Converts the value of this instance to its equivalent string representation (either "True" or "False").
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return booleanMethod(Data).ToString();
        }
    }
}