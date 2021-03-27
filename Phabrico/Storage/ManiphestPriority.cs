using System;
using System.Collections.Generic;
using System.Data.SQLite;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Phabrico.Miscellaneous;

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

                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// Returns all available ManiphestPriorityInfo records
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public override IEnumerable<Phabricator.Data.ManiphestPriority> Get(Database database)
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
        public override Phabricator.Data.ManiphestPriority Get(Database database, string key, bool ignoreStageData = false)
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
