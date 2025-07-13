using Phabrico.Miscellaneous;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;

namespace Phabrico.Storage
{
    /// <summary>
    /// Database mapper for TransactionInfo table
    /// </summary>
    public class Transaction : PhabricatorObject<Phabricator.Data.Transaction>
    {
        /// <summary>
        /// Adds or modifies a TransactionInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="transaction"></param>
        public override void Add(Database database, Phabricator.Data.Transaction transaction)
        {
            lock (Database.dbLock)
            {
                using (SQLiteTransaction sqlTransaction = database.Connection.BeginTransaction())
                {
                    using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           INSERT OR REPLACE INTO transactionInfo(parentToken, id, type, author, oldValue, newValue, dateModified) 
                           VALUES (@parentToken, @id, @type, @author, @oldValue, @newValue, @dateModified);
                       ", database.Connection, sqlTransaction))
                    {
                        database.AddParameter(dbCommand, "parentToken", transaction.Token, Database.EncryptionMode.None);
                        database.AddParameter(dbCommand, "id", transaction.ID, Database.EncryptionMode.None);
                        database.AddParameter(dbCommand, "type", transaction.Type);
                        database.AddParameter(dbCommand, "author", transaction.Author);
                        database.AddParameter(dbCommand, "oldValue", transaction.OldValue);
                        database.AddParameter(dbCommand, "newValue", transaction.NewValue);
                        database.AddParameter(dbCommand, "dateModified", transaction.DateModified);
                        dbCommand.ExecuteNonQuery();

                        Database.IsModified = true;

                        sqlTransaction.Commit();
                    }
                }
            }
        }

        /// <summary>
        /// Returns all available TransactionInfo records
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public override IEnumerable<Phabricator.Data.Transaction> Get(Database database, Language language)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT parentToken, id, type, author, oldValue, newValue, dateModified
                       FROM transactionInfo;
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Phabricator.Data.Transaction record = new Phabricator.Data.Transaction();
                        record.Token = (string)reader["parentToken"];
                        record.ID = (string)reader["id"];
                        record.Author = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["author"]);
                        record.OldValue = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["oldValue"]);
                        record.NewValue = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["newValue"]);
                        record.DateModified = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateModified"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);

                        yield return record;
                    }
                }
            }
        }

        /// <summary>
        /// Returns all available TransactionInfo records belonging to a given token
        /// </summary>
        /// <param name="database"></param>
        /// <param name="parentToken"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public IEnumerable<Phabricator.Data.Transaction> GetAll(Database database, string parentToken, Language language)
        {
            List<Phabricator.Data.Transaction> result = new List<Phabricator.Data.Transaction>();
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT parentToken, id, type, author, oldValue, newValue, dateModified
                       FROM transactionInfo
                       WHERE parentToken = @parentToken;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "parentToken", parentToken, Database.EncryptionMode.None);

                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Phabricator.Data.Transaction record = new Phabricator.Data.Transaction();
                        record.Token = (string)reader["parentToken"];
                        record.ID = reader["id"].ToString();
                        record.IsStaged = false;
                        record.Author = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["author"]);
                        record.Type = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["type"]);
                        record.OldValue = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["oldValue"]);
                        record.NewValue = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["newValue"]);
                        record.DateModified = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateModified"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);

                        result.Add(record);
                    }
                }

                Stage stageStorage = new Stage();
                result.AddRange(stageStorage.Get<Phabricator.Data.Transaction>(database, language)
                                            .Where(stageData => stageData.Token.Equals(parentToken))
                                            .Select(stagedTransaction => new Phabricator.Data.Transaction(stagedTransaction)
                                            {
                                                IsStaged = true
                                            })
                               );

                return result.OrderBy(record => record.DateModified);
            }
        }

        /// <summary>
        /// Returns a specific TransactionInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="id"></param>
        /// <param name="ignoreStageData"></param>
        /// <returns></returns>
        public override Phabricator.Data.Transaction Get(Database database, string id, Language language, bool ignoreStageData)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                SELECT parentToken, id, type, author, oldValue, newValue, dateModified
                FROM transactionInfo
                WHERE id = @id;
            ", database.Connection))
            {
                database.AddParameter(dbCommand, "id", id, Database.EncryptionMode.None);

                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Phabricator.Data.Transaction record = new Phabricator.Data.Transaction();
                        record.Token = (string)reader["parentToken"];
                        record.ID = (string)reader["id"];
                        record.Author = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["author"]);
                        record.OldValue = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["oldValue"]);
                        record.NewValue = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["newValue"]);
                        record.DateModified = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateModified"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);

                        return record;
                    }
                }

                return null;
            }
        }
    }
}
