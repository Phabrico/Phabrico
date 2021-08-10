using Phabrico.Miscellaneous;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Phabrico.Storage
{
    /// <summary>
    /// Storage class for documents which are marked as Favorite
    /// </summary>
    public class FavoriteObject : PhabricatorObject<Phabricator.Data.FavoriteObject>
    {
        /// <summary>
        /// Adds or modifies a FavoriteObject record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="favoriteObject"></param>
        public override void Add(Database database, Phabricator.Data.FavoriteObject favoriteObject)
        {
            using (SQLiteTransaction transaction = database.Connection.BeginTransaction())
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           INSERT OR REPLACE INTO favoriteObject(accountUserName, token, displayOrder) 
                           VALUES (@accountUserName, @token, @displayOrder);
                       ", database.Connection, transaction))
                {
                    database.AddParameter(dbCommand, "accountUserName", favoriteObject.AccountUserName, Database.EncryptionMode.Default);
                    database.AddParameter(dbCommand, "token", favoriteObject.Token, Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "displayOrder", (int)favoriteObject.DisplayOrder, Database.EncryptionMode.None);
                    dbCommand.ExecuteNonQuery();

                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// Returns all available FavoriteObject records
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public override IEnumerable<Phabricator.Data.FavoriteObject> Get(Database database)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT accountUserName, token, displayOrder
                       FROM favoriteObject;
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Phabricator.Data.FavoriteObject record = new Phabricator.Data.FavoriteObject();
                        record.AccountUserName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["accountUserName"]);
                        record.Token = (string)reader["token"];
                        record.DisplayOrder = (int)reader["displayOrder"];

                        yield return record;
                    }
                }
            }
        }

        internal Phabricator.Data.FavoriteObject Get(Database database, string accountUserName, string favoriteToken)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT accountUserName, token, displayOrder
                       FROM favoriteObject
                       WHERE accountUserName = @accountUserName
                         AND token = @favoriteToken;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "accountUserName", accountUserName, Database.EncryptionMode.Default);
                database.AddParameter(dbCommand, "favoriteToken", favoriteToken, Database.EncryptionMode.None);
                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Phabricator.Data.FavoriteObject record = new Phabricator.Data.FavoriteObject();
                        record.AccountUserName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["accountUserName"]);
                        record.Token = (string)reader["token"];
                        record.DisplayOrder = (int)reader["displayOrder"];

                        return record;
                    }
                }
            }

            return null;
        }

        #pragma warning disable 0809
        /// <summary>
        /// Do not use this method: use Phabricator.Data.FavoriteObject.Get(Database,string,string) instead
        /// </summary>
        /// <param name="database"></param>
        /// <param name="key"></param>
        /// <param name="ignoreStageData"></param>
        /// <returns></returns>
        [Obsolete("Use Phabricator.Data.FavoriteObject.Get(Database,string,string) instead")]
        public override Phabricator.Data.FavoriteObject Get(Database database, string key, bool ignoreStageData)
        {
            throw new NotImplementedException("Use Phabricator.Data.FavoriteObject.Get(Database,string,string) instead");
        }
        #pragma warning restore 0809

        /// <summary>
        /// Removes a FavoriteObject record from the database
        /// </summary>
        /// <param name="database"></param>
        /// <param name="favoriteObject"></param>
        public void Remove(Database database, Phabricator.Data.FavoriteObject favoriteObject)
        {
            using (SQLiteTransaction transaction = database.Connection.BeginTransaction())
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           UPDATE favoriteObject
                              SET displayOrder = displayOrder - 1
                            WHERE accountUserName = @accountUserName
                              AND displayOrder > ( SELECT IFNULL( MAX(displayOrder), 999999 )
                                                   FROM favoriteObject
                                                   WHERE accountUserName = @accountUserName
                                                     AND token = @token
                                                 );

                           DELETE FROM favoriteObject
                           WHERE accountUserName = @accountUserName
                             AND token = @token;
                       ", database.Connection, transaction))
                {
                    database.AddParameter(dbCommand, "accountUserName", favoriteObject.AccountUserName, Database.EncryptionMode.Default);
                    database.AddParameter(dbCommand, "token", favoriteObject.Token, Database.EncryptionMode.None);
                    dbCommand.ExecuteNonQuery();

                    transaction.Commit();
                }
            }
        }
    }
}
