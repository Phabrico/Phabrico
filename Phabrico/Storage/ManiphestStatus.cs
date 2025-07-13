using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Miscellaneous;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;

namespace Phabrico.Storage
{
    /// <summary>
    /// Database mapper for ManiphestStatusInfo table
    /// </summary>
    public class ManiphestStatus : PhabricatorObject<Phabricator.Data.ManiphestStatus>
    {
        /// <summary>
        /// Adds or modifies a ManiphestStatusInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="maniphestStatus"></param>
        public override void Add(Database database, Phabricator.Data.ManiphestStatus maniphestStatus)
        {
            lock (Database.dbLock)
            {
                using (SQLiteTransaction transaction = database.Connection.BeginTransaction())
                {
                    string info = JsonConvert.SerializeObject(new
                    {
                        Icon = maniphestStatus.Icon
                    });

                    using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           INSERT OR REPLACE INTO maniphestStatusInfo(name, value, closed, info) 
                           VALUES (@name, @value, @closed, @info);
                       ", database.Connection, transaction))
                    {
                        database.AddParameter(dbCommand, "name", maniphestStatus.Name);
                        database.AddParameter(dbCommand, "value", maniphestStatus.Value);
                        database.AddParameter(dbCommand, "closed", maniphestStatus.Closed);
                        database.AddParameter(dbCommand, "info", info);
                        dbCommand.ExecuteNonQuery();

                        Database.IsModified = true;

                        transaction.Commit();
                    }
                }
            }
        }

        /// <summary>
        /// Copies the ManiphestStatus records from one Phabrico database to another Phabrico database
        /// </summary>
        /// <param name="sourcePhabricoDatabasePath">File path to the source Phabrico database</param>
        /// <param name="sourceUsername">Username to use for authenticating the source Phabrico database</param>
        /// <param name="sourcePassword">Password to use for authenticating the source Phabrico database</param>
        /// <param name="destinationPhabricoDatabasePath">File path to the destination Phabrico database</param>
        /// <param name="destinationUsername">Username to use for authenticating the destination Phabrico database</param>
        /// <param name="destinationPassword">Username to use for authenticating the destination Phabrico database</param>
        public static List<Phabricator.Data.ManiphestStatus> Copy(string sourcePhabricoDatabasePath, string sourceUsername, string sourcePassword, string destinationPhabricoDatabasePath, string destinationUsername, string destinationPassword)
        {
            string sourceTokenHash = Encryption.GenerateTokenKey(sourceUsername, sourcePassword);  // tokenHash is stored in the database
            string sourcePublicEncryptionKey = Encryption.GenerateEncryptionKey(sourceUsername, sourcePassword);  // encryptionKey is not stored in database (except when security is disabled)
            string sourcePrivateEncryptionKey = Encryption.GeneratePrivateEncryptionKey(sourceUsername, sourcePassword);  // privateEncryptionKey is not stored in database
            string destinationTokenHash = Encryption.GenerateTokenKey(destinationUsername, destinationPassword);  // tokenHash is stored in the database
            string destinationPublicEncryptionKey = Encryption.GenerateEncryptionKey(destinationUsername, destinationPassword);  // encryptionKey is not stored in database (except when security is disabled)
            string destinationPrivateEncryptionKey = Encryption.GeneratePrivateEncryptionKey(destinationUsername, destinationPassword);  // privateEncryptionKey is not stored in database

            string originalDataSource = Storage.Database.DataSource;

            List<Phabricator.Data.ManiphestStatus> maniphestStatuses = new List<Phabricator.Data.ManiphestStatus>();
            try
            {
                Storage.ManiphestStatus maniphestStatusStorage = new Storage.ManiphestStatus();

                Storage.Database.DataSource = sourcePhabricoDatabasePath;
                using (Storage.Database database = new Storage.Database(null))
                {
                    bool noUserConfigured;
                    UInt64[] publicXorCipher = database.ValidateLogIn(sourceTokenHash, out noUserConfigured);
                    if (publicXorCipher != null)
                    {
                        database.EncryptionKey = Encryption.XorString(sourcePublicEncryptionKey, publicXorCipher);

                        maniphestStatuses = maniphestStatusStorage.Get(database, Language.NotApplicable).ToList();
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

                        foreach (Phabricator.Data.ManiphestStatus maniphestStatus in maniphestStatuses)
                        {
                            maniphestStatusStorage.Add(database, maniphestStatus);
                        }
                    }
                }
            }
            finally
            {
                Storage.Database.DataSource = originalDataSource;
            }

            return maniphestStatuses;
        }

        /// <summary>
        /// Returns all available ManiphestStatusInfo records
        /// </summary>
        /// <param name="database"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public override IEnumerable<Phabricator.Data.ManiphestStatus> Get(Database database, Language language)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT name, value, closed, info
                       FROM maniphestStatusInfo;
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Phabricator.Data.ManiphestStatus record = new Phabricator.Data.ManiphestStatus();
                        record.Name = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["name"]);
                        record.Value = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["value"]);
                        record.Closed = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["closed"]).Equals("true", StringComparison.OrdinalIgnoreCase);
                        string decryptedInfo = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["info"]);
                        JObject info = JsonConvert.DeserializeObject(decryptedInfo) as JObject;
                        if (info == null) continue;

                        record.Icon = (string)info["Icon"];

                        yield return record;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a specific ManiphestStatusInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="key"></param>
        /// <param name="ignoreStageData"></param>
        /// <returns></returns>
        public override Phabricator.Data.ManiphestStatus Get(Database database, string key, Language language, bool ignoreStageData = false)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT name, value, closed, info
                       FROM maniphestStatusInfo
                       WHERE value = @key;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "key", key, Database.EncryptionMode.Default);
                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Phabricator.Data.ManiphestStatus record = new Phabricator.Data.ManiphestStatus();
                        record.Name = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["name"]);
                        record.Value = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["value"]);
                        record.Closed = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["closed"]).Equals("true", StringComparison.OrdinalIgnoreCase);
                        string decryptedInfo = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["info"]);
                        JObject info = JsonConvert.DeserializeObject(decryptedInfo) as JObject;
                        if (info == null) return null;

                        record.Icon = (string)info["Icon"];

                        return record;
                    }
                }

                return null;
            }
        }
    }
}
