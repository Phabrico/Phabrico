using Phabrico.Miscellaneous;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;

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
            lock (Database.dbLock)
            {
                using (SQLiteTransaction transaction = database.Connection.BeginTransaction())
                {
                    using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           INSERT OR REPLACE INTO userInfo(token, userName, realName, selected, dateSynchronized, isBot, isDisabled)
                           VALUES (@userToken, @userName, @userRealName, @selected, @dateSynchronized, @isBot, @isDisabled);
                       ", database.Connection, transaction))
                    {

                        database.AddParameter(dbCommand, "userToken", user.Token, Database.EncryptionMode.None);
                        database.AddParameter(dbCommand, "userName", user.UserName);
                        database.AddParameter(dbCommand, "userRealName", user.RealName);
                        database.AddParameter(dbCommand, "selected", Encryption.Encrypt(database.EncryptionKey, user.Selected.ToString()));
                        database.AddParameter(dbCommand, "isBot", Encryption.Encrypt(database.EncryptionKey, user.IsBot.ToString()));
                        database.AddParameter(dbCommand, "isDisabled", Encryption.Encrypt(database.EncryptionKey, user.IsDisabled.ToString()));
                        database.AddParameter(dbCommand, "dateSynchronized", user.DateSynchronized);
                        dbCommand.ExecuteNonQuery();

                        Database.IsModified = true;
                    }

                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// Copies the User records from one Phabrico database to another Phabrico database
        /// </summary>
        /// <param name="sourcePhabricoDatabasePath">File path to the source Phabrico database</param>
        /// <param name="sourceUsername">Username to use for authenticating the source Phabrico database</param>
        /// <param name="sourcePassword">Password to use for authenticating the source Phabrico database</param>
        /// <param name="destinationPhabricoDatabasePath">File path to the destination Phabrico database</param>
        /// <param name="destinationUsername">Username to use for authenticating the destination Phabrico database</param>
        /// <param name="destinationPassword">Username to use for authenticating the destination Phabrico database</param>
        /// <param name="filter">LINQ method for filtering the records to be copied</param>
        public static List<Phabricator.Data.User> Copy(string sourcePhabricoDatabasePath, string sourceUsername, string sourcePassword, string destinationPhabricoDatabasePath, string destinationUsername, string destinationPassword, Func<Phabricator.Data.User, bool> filter = null)
        {
            string sourceTokenHash = Encryption.GenerateTokenKey(sourceUsername, sourcePassword);  // tokenHash is stored in the database
            string sourcePublicEncryptionKey = Encryption.GenerateEncryptionKey(sourceUsername, sourcePassword);  // encryptionKey is not stored in database (except when security is disabled)
            string sourcePrivateEncryptionKey = Encryption.GeneratePrivateEncryptionKey(sourceUsername, sourcePassword);  // privateEncryptionKey is not stored in database
            string destinationTokenHash = Encryption.GenerateTokenKey(destinationUsername, destinationPassword);  // tokenHash is stored in the database
            string destinationPublicEncryptionKey = Encryption.GenerateEncryptionKey(destinationUsername, destinationPassword);  // encryptionKey is not stored in database (except when security is disabled)
            string destinationPrivateEncryptionKey = Encryption.GeneratePrivateEncryptionKey(destinationUsername, destinationPassword);  // privateEncryptionKey is not stored in database

            string originalDataSource = Storage.Database.DataSource;

            List<Phabricator.Data.User> users = new List<Phabricator.Data.User>();
            try
            {
                Storage.User userStorage = new Storage.User();

                Storage.Database.DataSource = sourcePhabricoDatabasePath;
                using (Storage.Database database = new Storage.Database(null))
                {
                    bool noUserConfigured;
                    UInt64[] publicXorCipher = database.ValidateLogIn(sourceTokenHash, out noUserConfigured);
                    if (publicXorCipher != null)
                    {
                        database.EncryptionKey = Encryption.XorString(sourcePublicEncryptionKey, publicXorCipher);

                        IEnumerable<Phabricator.Data.User> sourceUsers = userStorage.Get(database, Language.NotApplicable);
                        if (filter != null)
                        {
                            sourceUsers = sourceUsers.Where(filter);
                        }
                        users = sourceUsers.ToList();
                    }
                }

                Storage.Database.DataSource = destinationPhabricoDatabasePath;
                using (Storage.Database database = new Storage.Database(null))
                {
                    bool noUserConfigured;
                    UInt64[] publicXorCipher = database.ValidateLogIn(destinationTokenHash, out noUserConfigured);
                    if (publicXorCipher != null)
                    {
                        database.EncryptionKey = Encryption.XorString(destinationPublicEncryptionKey, publicXorCipher);

                        foreach (Phabricator.Data.User user in users)
                        {
                            userStorage.Add(database, user);
                        }
                    }
                }
            }
            finally
            {
                Storage.Database.DataSource = originalDataSource;
            }

            return users;
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
        /// <param name="language"></param>
        /// <returns></returns>
        public override IEnumerable<Phabricator.Data.User> Get(Database database, Language language)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT *
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
                        try
                        {
                            record.IsBot = bool.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["isBot"]));
                            record.IsDisabled = bool.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["isDisabled"]));
                        }
                        catch
                        {
                            record.IsBot = false;
                            record.IsDisabled = false;
                        }
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
        public override Phabricator.Data.User Get(Database database, string key, Language language, bool ignoreStageData = false)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, userName, realName, selected, dateSynchronized, isBot, isDisabled
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
                        try
                        {
                            record.IsBot = bool.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["isBot"]));
                            record.IsDisabled = bool.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["isDisabled"]));
                        }
                        catch
                        {
                            record.IsBot = false;
                            record.IsDisabled = false;
                        }
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
            lock (Database.dbLock)
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       UPDATE userInfo
                          SET selected = @selected
                       WHERE token = @token;
                   ", database.Connection))
                {
                    database.AddParameter(dbCommand, "token", token, Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "selected", Encryption.Encrypt(database.EncryptionKey, doSelectUser.ToString()));
                    if (dbCommand.ExecuteNonQuery() > 0)
                    {
                        Database.IsModified = true;
                    }
                }
            }
        }

        /// <summary>
        /// Removes a user from the database
        /// </summary>
        /// <param name="database"></param>
        /// <param name="user"></param>
        public void Remove(Database database, Phabricator.Data.User user)
        {
            lock (Database.dbLock)
            {
                using (SQLiteCommand cmdDeleteUserInfo = new SQLiteCommand(@"
                       DELETE FROM userInfo
                       WHERE token = @token;
                   ", database.Connection))
                {
                    database.AddParameter(cmdDeleteUserInfo, "token", user.Token, Database.EncryptionMode.None);
                    if (cmdDeleteUserInfo.ExecuteNonQuery() > 0)
                    {
                        Database.IsModified = true;
                    }
                }
            }
        }
    }
}
