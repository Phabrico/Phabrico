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
    /// Database mapper for ManiphestPriorityInfo table
    /// </summary>
    public class ManiphestPriority : PhabricatorObject<Phabricator.Data.ManiphestPriority>
    {
        /// <summary>
        /// Adds or modifies a ManiphestPriorityInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="maniphestPriority"></param>
        public override void Add(Database database, Phabricator.Data.ManiphestPriority maniphestPriority)
        {
            using (SQLiteTransaction transaction = database.Connection.BeginTransaction())
            {
                string info = JsonConvert.SerializeObject(new
                {
                    Name = maniphestPriority.Name,
                    Identifier = maniphestPriority.Identifier,
                    Color = maniphestPriority.Color
                });

                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           INSERT OR REPLACE INTO maniphestPriorityInfo(priority, info) 
                           VALUES (@priority, @info);
                       ", database.Connection, transaction))
                {
                    database.AddParameter(dbCommand, "priority", maniphestPriority.Priority.ToString(), Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "info", info);
                    dbCommand.ExecuteNonQuery();

                    Database.IsModified = true;

                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// Copies the ManiphestPriority records from one Phabrico database to another Phabrico database
        /// </summary>
        /// <param name="sourcePhabricoDatabasePath">File path to the source Phabrico database</param>
        /// <param name="sourceUsername">Username to use for authenticating the source Phabrico database</param>
        /// <param name="sourcePassword">Password to use for authenticating the source Phabrico database</param>
        /// <param name="destinationPhabricoDatabasePath">File path to the destination Phabrico database</param>
        /// <param name="destinationUsername">Username to use for authenticating the destination Phabrico database</param>
        /// <param name="destinationPassword">Username to use for authenticating the destination Phabrico database</param>
        public static List<Phabricator.Data.ManiphestPriority> Copy(string sourcePhabricoDatabasePath, string sourceUsername, string sourcePassword, string destinationPhabricoDatabasePath, string destinationUsername, string destinationPassword)
        {
            string sourceTokenHash = Encryption.GenerateTokenKey(sourceUsername, sourcePassword);  // tokenHash is stored in the database
            string sourcePublicEncryptionKey = Encryption.GenerateEncryptionKey(sourceUsername, sourcePassword);  // encryptionKey is not stored in database (except when security is disabled)
            string sourcePrivateEncryptionKey = Encryption.GeneratePrivateEncryptionKey(sourceUsername, sourcePassword);  // privateEncryptionKey is not stored in database
            string destinationTokenHash = Encryption.GenerateTokenKey(destinationUsername, destinationPassword);  // tokenHash is stored in the database
            string destinationPublicEncryptionKey = Encryption.GenerateEncryptionKey(destinationUsername, destinationPassword);  // encryptionKey is not stored in database (except when security is disabled)
            string destinationPrivateEncryptionKey = Encryption.GeneratePrivateEncryptionKey(destinationUsername, destinationPassword);  // privateEncryptionKey is not stored in database

            string originalDataSource = Storage.Database.DataSource;

            List<Phabricator.Data.ManiphestPriority> maniphestPriorities = new List<Phabricator.Data.ManiphestPriority>();
            try
            {
                Storage.ManiphestPriority maniphestPriorityStorage = new Storage.ManiphestPriority();

                Storage.Database.DataSource = sourcePhabricoDatabasePath;
                using (Storage.Database database = new Storage.Database(null))
                {
                    bool noUserConfigured;
                    UInt64[] publicXorCipher = database.ValidateLogIn(sourceTokenHash, out noUserConfigured);
                    if (publicXorCipher != null)
                    {
                        database.EncryptionKey = Encryption.XorString(sourcePublicEncryptionKey, publicXorCipher);

                        maniphestPriorities = maniphestPriorityStorage.Get(database, Language.NotApplicable).ToList();
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

                        foreach (Phabricator.Data.ManiphestPriority maniphestPriority in maniphestPriorities)
                        {
                            maniphestPriorityStorage.Add(database, maniphestPriority);
                        }
                    }
                }
            }
            finally
            {
                Storage.Database.DataSource = originalDataSource;
            }

            return maniphestPriorities;
        }

        /// <summary>
        /// Returns all available ManiphestPriorityInfo records
        /// </summary>
        /// <param name="database"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public override IEnumerable<Phabricator.Data.ManiphestPriority> Get(Database database, Language language)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT priority, info
                       FROM maniphestPriorityInfo;
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Phabricator.Data.ManiphestPriority record = new Phabricator.Data.ManiphestPriority();
                        record.Priority = (Int32)reader["priority"];
                        string decryptedInfo = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["info"]);
                        JObject info = JsonConvert.DeserializeObject(decryptedInfo) as JObject;
                        if (info == null) continue;

                        record.Name = (string)info["Name"];
                        record.Identifier = (string)info["Identifier"];
                        record.Color = (string)info["Color"];

                        yield return record;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a specific ManiphestPriorityInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="key"></param>
        /// <param name="ignoreStageData"></param>
        /// <returns></returns>
        public override Phabricator.Data.ManiphestPriority Get(Database database, string key, Language language, bool ignoreStageData = false)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT priority, info
                       FROM maniphestPriorityInfo
                       WHERE priority = @key;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "key", key, Database.EncryptionMode.None);
                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Phabricator.Data.ManiphestPriority record = new Phabricator.Data.ManiphestPriority();
                        record.Priority = (Int32)reader["priority"];
                        string decryptedInfo = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["info"]);
                        JObject info = JsonConvert.DeserializeObject(decryptedInfo) as JObject;
                        if (info == null) return null;

                        record.Name = (string)info["Name"];
                        record.Identifier = (string)info["Identifier"];
                        record.Color = (string)info["Color"];

                        return record;
                    }
                }

                return null;
            }
        }
    }
}
