using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Miscellaneous;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

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

                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// Returns all available ManiphestStatusInfo records
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public override IEnumerable<Phabricator.Data.ManiphestStatus> Get(Database database)
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
        public override Phabricator.Data.ManiphestStatus Get(Database database, string key, bool ignoreStageData = false)
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

                        record.Icon = (string)info["Icon"];

                        return record;
                    }
                }

                return null;
            }
        }
    }
}
