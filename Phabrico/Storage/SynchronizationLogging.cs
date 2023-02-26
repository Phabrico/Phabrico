using Phabrico.Miscellaneous;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;

namespace Phabrico.Storage
{
    /// <summary>
    /// Database mapper for SynchronizationLogging table
    /// </summary>
    public class SynchronizationLogging : PhabricatorObject<Phabricator.Data.SynchronizationLogging>
    {
        /// <summary>
        /// Inserts or updates a SynchronizationLogging record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="synchronizationLogging"></param>
        public override void Add(Database database, Phabricator.Data.SynchronizationLogging synchronizationLogging)
        {
            using (SQLiteTransaction transaction = database.Connection.BeginTransaction())
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           INSERT OR REPLACE INTO synchronizationLogging(token, title, url, previousContent, metadataModified, dateModified, lastModifiedBy) 
                           VALUES (@token, @title, @url, @previousContent, @metadataModified, @dateModified, @lastModifiedBy);
                       ", database.Connection, transaction))
                {

                    database.AddParameter(dbCommand, "token", synchronizationLogging.Token, Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "title", synchronizationLogging.Title);
                    database.AddParameter(dbCommand, "url", synchronizationLogging.URL);
                    database.AddParameter(dbCommand, "previousContent", synchronizationLogging.PreviousContent);
                    database.AddParameter(dbCommand, "metadataModified", synchronizationLogging.MetadataIsModified ? 1 : 0, Database.EncryptionMode.None);  // no encryption because there are only 2 possible values
                    database.AddParameter(dbCommand, "dateModified", synchronizationLogging.DateModified);
                    database.AddParameter(dbCommand, "lastModifiedBy", synchronizationLogging.LastModifiedBy);
                    dbCommand.ExecuteNonQuery();

                    Database.IsModified = true;

                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// Deletes all SynchronizationLogging records
        /// </summary>
        /// <param name="database"></param>
        public void Clear(Database database)
        {
            using (SQLiteTransaction transaction = database.Connection.BeginTransaction())
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           DELETE FROM synchronizationLogging;
                       ", database.Connection, transaction))
                {
                    if (dbCommand.ExecuteNonQuery() > 0)
                    {
                        Database.IsModified = true;
                    }

                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// Deletes a SynchronizationLogging record for a given token
        /// </summary>
        /// <param name="database"></param>
        /// <param name="token"></param>
        public static void Delete(Database database, string token)
        {
            using (SQLiteTransaction transaction = database.Connection.BeginTransaction())
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           DELETE FROM synchronizationLogging
                           WHERE token = @token;
                       ", database.Connection, transaction))
                {
                    database.AddParameter(dbCommand, "token", token, Database.EncryptionMode.None);
                    if (dbCommand.ExecuteNonQuery() > 0)
                    {
                        Database.IsModified = true;
                    }

                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// Returns all available keywords from the SQLite database
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public override IEnumerable<Phabricator.Data.SynchronizationLogging> Get(Database database, Language language)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, title, url, previousContent, metadataModified, dateModified, lastModifiedBy
                       FROM synchronizationLogging;
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Phabricator.Data.SynchronizationLogging record = new Phabricator.Data.SynchronizationLogging();
                        record.Token = (string)reader["token"];
                        record.Title = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["title"]);
                        record.URL = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["url"]);
                        record.PreviousContent = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["previousContent"]);
                        record.MetadataIsModified = (int)reader["metadataModified"] > 0;
                        record.DateModified = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateModified"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);
                        record.LastModifiedBy = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["lastModifiedBy"]);

                        yield return record;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a specific SynchronizationLogging record for a given token
        /// </summary>
        /// <param name="database">SQLite database reference</param>
        /// <param name="token">Token to be searched for</param>
        /// <param name="ignoreStageData">Not used</param>
        /// <returns></returns>
        public override Phabricator.Data.SynchronizationLogging Get(Database database, string token, Language language, bool ignoreStageData)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, title, url, previousContent, metadataModified, dateModified, lastModifiedBy
                       FROM synchronizationLogging
                       WHERE token = @token;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "token", token, Database.EncryptionMode.None);

                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Phabricator.Data.SynchronizationLogging record = new Phabricator.Data.SynchronizationLogging();
                        record.Token = (string)reader["token"];
                        record.Title = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["title"]);
                        record.URL = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["url"]);
                        record.PreviousContent = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["previousContent"]);
                        record.MetadataIsModified = (int)reader["metadataModified"] > 0;
                        record.DateModified = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateModified"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);
                        record.LastModifiedBy = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["lastModifiedBy"]);

                        return record;
                    }

                    return null;
                }
            }
        }
    }
}
