using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;

using Phabrico.Miscellaneous;

namespace Phabrico.Storage
{
    /// <summary>
    /// Database mapper for UserInfo table
    /// </summary>
    public class User : PhabricatorObject<Phabricator.Data.User>
    {
        /// <summary>
        /// Adds or modifies a record into the UserInfo table
        /// </summary>
        /// <param name="database"></param>
        /// <param name="user"></param>
        public override void Add(Database database, Phabricator.Data.User user)
        {
            using (SQLiteTransaction transaction = database.Connection.BeginTransaction())
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           INSERT OR REPLACE INTO userInfo(token, userName, realName, selected, dateSynchronized) 
                           VALUES (@userToken, @userName, @userRealName, @selected, @dateSynchronized);
                       ", database.Connection, transaction))
                {

                    database.AddParameter(dbCommand, "userToken", user.Token, Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "userName", user.UserName);
                    database.AddParameter(dbCommand, "userRealName", user.RealName);
                    database.AddParameter(dbCommand, "selected", Encryption.Encrypt(database.EncryptionKey, user.Selected.ToString()));
                    database.AddParameter(dbCommand, "dateSynchronized", user.DateSynchronized);
                    dbCommand.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }

        /// <summary>
        /// Returns the number of UserInfo records
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public static long Count(Database database)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT COUNT(*) AS result
                       FROM userInfo
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return (long)reader["result"];
                    }
                }

                return 0;
            }
        }

        /// <summary>
        /// Returns all UserInfo records
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public override IEnumerable<Phabricator.Data.User> Get(Database database)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, userName, realName, selected, dateSynchronized
                       FROM userInfo;
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Phabricator.Data.User record = new Phabricator.Data.User();
                        record.Token = (string)reader["token"];
                        record.UserName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["userName"]);
                        record.RealName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["realName"]);
                        record.Selected = bool.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["selected"]));
                        record.DateSynchronized = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateSynchronized"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);

                        yield return record;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a specific UserInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="key"></param>
        /// <param name="ignoreStageData"></param>
        /// <returns></returns>
        public override Phabricator.Data.User Get(Database database, string key, bool ignoreStageData = false)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, userName, realName, selected, dateSynchronized
                       FROM userInfo
                       WHERE token = @key
                          OR userName = @userName;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "key", key, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "userName", key, Database.EncryptionMode.Default);
                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Phabricator.Data.User record = new Phabricator.Data.User();
                        record.Token = (string)reader["token"];
                        record.UserName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["userName"]);
                        record.RealName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["realName"]);
                        record.Selected = bool.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["selected"]));
                        record.DateSynchronized = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateSynchronized"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);

                        return record;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Changes the 'selected' state of a UserInfo record
        /// If 'selected' than the UserInfo record is taken into account for synchronizing with Phabricator
        /// </summary>
        /// <param name="database"></param>
        /// <param name="token"></param>
        /// <param name="doSelectUser"></param>
        public void SelectUser(Database database, string token, bool doSelectUser)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       UPDATE userInfo
                          SET selected = @selected
                       WHERE token = @token;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "token", token, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "selected", Encryption.Encrypt(database.EncryptionKey, doSelectUser.ToString()));
                dbCommand.ExecuteNonQuery();
            }
        }
    }
}
