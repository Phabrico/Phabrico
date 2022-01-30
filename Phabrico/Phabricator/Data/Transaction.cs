using System;

namespace Phabrico.Phabricator.Data
{
    /// <summary>
    /// Represents an TransactionInfo record from the SQLite Phabrico database
    /// This class is part of Phabrico.Phabricator.Data.Maniphest and represents its metadata, which is shown
    /// at the bottom of a Maniphest task (e.g. comments, state changes, ...)
    /// </summary>
    public class Transaction : PhabricatorObject
    {
        /// <summary>
        /// Token prefix to identify transaction objects (metadata) in the Phabrico database
        /// </summary>
        public const string Prefix = "PHID-TRAN-";

        /// <summary>
        /// Token of user who created the transaction
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Timestamp when the transaction was created
        /// </summary>
        public DateTimeOffset DateModified { get; set; }

        /// <summary>
        /// Transaction identifier
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// True if the transaction record wasn't uploaded to the Phabricator server
        /// </summary>
        public bool IsStaged { get; set; }

        /// <summary>
        /// Previous value of the transaction
        /// </summary>
        public string OldValue { get; set; }

        /// <summary>
        /// New value of the transaction
        /// </summary>
        public string NewValue { get; set; }

        /// <summary>
        /// Type of transaction (e.g. Priority)
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Initializes a new instance of a TransactionInfo record
        /// </summary>
        public Transaction()
        {
            TokenPrefix = Prefix;

            Author = "";
            ID = "";
            IsStaged = false;
            DateModified = DateTimeOffset.MinValue;
            OldValue = "";
            NewValue = "";
            Type = "";
        }

        /// <summary>
        /// Clones a new instance of a TransactionInfo record
        /// </summary>
        /// <param name="original"></param>
        public Transaction(Transaction original)
                : base(original)
        {
            TokenPrefix = Prefix;

            Author = original.Author;
            DateModified = original.DateModified;
            ID = original.ID;
            IsStaged = original.IsStaged;
            OldValue = original.OldValue;
            NewValue = original.NewValue;
            Type = original.Type;
        }

        /// <summary>
        /// Compares a Transaction with another Transaction
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public bool Equals(Transaction otherTransaction)
        {
            if (otherTransaction == null) return false;

            return base.Equals(otherTransaction);
        }
    }
}