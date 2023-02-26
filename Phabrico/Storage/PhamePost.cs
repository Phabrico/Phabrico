using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Miscellaneous;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;

namespace Phabrico.Storage
{
    /// <summary>
    /// Database mapper for PhamePost table
    /// </summary>
    public class PhamePost : PhabricatorObject<Phabricator.Data.PhamePost>
    {
        /// <summary>
        /// Adds or modifies a PhamePost record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="phriction"></param>
        public override void Add(Database database, Phabricator.Data.PhamePost phamePost)
        {
            string info = JsonConvert.SerializeObject(new
            {
                Author = phamePost.Author,
                Blog = phamePost.Blog,
                Content = phamePost.Content,
                DateModified = phamePost.DateModified.ToString("yyyy-MM-dd HH:mm:ss zzzz"),
                Title = phamePost.Title,
            });

            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           INSERT OR REPLACE INTO phamePostInfo(token, id, info) 
                           VALUES (@token, @id, @info);
                       ", database.Connection))
            {
                database.AddParameter(dbCommand, "token", phamePost.Token, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "id", phamePost.ID, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "info", info);
                dbCommand.ExecuteNonQuery();

                Database.IsModified = true;
            }
        }

        /// <summary>
        /// Returns all PhamePost records
        /// </summary>
        /// <param name="database"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public override IEnumerable<Phabricator.Data.PhamePost> Get(Database database, Language language)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token,
                              id,
                              info
                       FROM phamePostInfo;
                   ",
                   database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Phabricator.Data.PhamePost record = new Phabricator.Data.PhamePost();
                        record.ID = (string)reader["id"];
                        record.Token = (string)reader["token"];
                        string decryptedInfo = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["info"]);
                        JObject info = JsonConvert.DeserializeObject(decryptedInfo) as JObject;
                        if (info == null) continue;

                        record.Author = (string)info["Author"];
                        record.Blog = (string)info["Blog"];
                        record.Content = (string)info["Content"];
                        record.DateModified = DateTimeOffset.ParseExact((string)info["DateModified"], "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);
                        record.Title = (string)info["Title"];

                        yield return record;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a specific PhamePost record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="key"></param>
        /// <param name="ignoreStageData"></param>
        /// <returns></returns>
        public override Phabricator.Data.PhamePost Get(Database database, string key, Language language, bool ignoreStageData = false)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token,
                              id,
                              info
                       FROM phamePostInfo
                       WHERE id = @key
                          OR token = @key;
                   ",
                   database.Connection))
            {
                database.AddParameter(dbCommand, "key", key, Database.EncryptionMode.None);
                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Phabricator.Data.PhamePost record = new Phabricator.Data.PhamePost();
                        record.ID = (string)reader["id"];
                        record.Token = (string)reader["token"];
                        string decryptedInfo = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["info"]);
                        JObject info = JsonConvert.DeserializeObject(decryptedInfo) as JObject;
                        if (info == null) return null;

                        record.Author = (string)info["Author"];
                        record.Blog = (string)info["Blog"];
                        record.Content = (string)info["Content"];
                        record.DateModified = DateTimeOffset.ParseExact((string)info["DateModified"], "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);
                        record.Title = (string)info["Title"];

                        return record;
                    }
                }
            }

            return null;
        }
    }
}
