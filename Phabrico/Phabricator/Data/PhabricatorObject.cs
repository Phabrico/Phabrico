using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System;

namespace Phabrico.Phabricator.Data
{
    /// <summary>
    /// Acts as a base class for all Phabricator.Data classes
    /// </summary>
    public class PhabricatorObject : IComparable, IDisposable
    {
        /// <summary>
        /// Identifier for PhabricatorObject
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Contains the first characters of the token
        /// By means of these characters, Phabrico is able to detect what object type a serialized string represents
        /// </summary>
        public string TokenPrefix { get; protected set; }

        /// <summary>
        /// In case content translation is performed, this property contains the language that is worked in
        /// </summary>
        public Language Language { get; set; } = Language.NotApplicable;

        /// <summary>
        /// Initializes a new PhabricatorObject instance
        /// </summary>
        public PhabricatorObject()
        {
        }

        /// <summary>
        /// Clones a new PhabricatorObject instance
        /// </summary>
        public PhabricatorObject(PhabricatorObject original)
        {
            this.Token = original.Token;
        }

        /// <summary>
        /// Compares the current PhabricatorObject object with another PhabricatorObject object
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public virtual int CompareTo(object obj)
        {
            PhabricatorObject other = obj as PhabricatorObject;
            return Token.CompareTo(other.Token);
        }

        /// <summary>
        /// Releases all resources
        /// </summary>
        public virtual void Dispose()
        {
        }

        /// <summary>
        /// Compares a PhabricatorObject with a token
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual bool Equals(string token)
        {
            return Token != null &&
                   token != null &&
                   Token.Equals(token);
        }

        /// <summary>
        /// Compares a PhabricatorObject with another PhabricatorObject
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            PhabricatorObject otherPhabricatorObject = obj as PhabricatorObject;
            if (otherPhabricatorObject == null) return false;

            return Token.Equals(otherPhabricatorObject.Token);
        }

        /// <summary>
        /// Returns the hash code for this PhabricatorObject
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return Token.GetHashCode();
        }

        /// <summary>
        /// Incorporates some stage data into the current PhabricatorObject
        /// </summary>
        /// <param name="stageData"></param>
        /// <returns></returns>
        public virtual bool MergeStageData(Stage.Data stageData)
        {
            return false;
        }

        /// <summary>
        /// Compares a PhabricatorObject with another PhabricatorObject
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator ==(PhabricatorObject left, PhabricatorObject right)
        {
            if (left is null)
            {
                return right is null;
            }
            return left.Equals(right);
        }

        /// <summary>
        /// Compares a PhabricatorObject with another PhabricatorObject
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator !=(PhabricatorObject left, PhabricatorObject right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Compares a PhabricatorObject with another PhabricatorObject
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator <(PhabricatorObject left, PhabricatorObject right)
        {
            return left.Token.CompareTo(right.Token) < 0;
        }

        /// <summary>
        /// Compares a PhabricatorObject with another PhabricatorObject
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator >(PhabricatorObject left, PhabricatorObject right)
        {
            return left.Token.CompareTo(right.Token) > 0;
        }

        /// <summary>
        /// Returns a descriptive name
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Token;
        }
    }
}