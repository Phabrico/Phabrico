using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Data.References;
using Phabrico.Miscellaneous;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;

namespace Phabrico.Storage
{
    /// <summary>
    /// Database mapper for PhrictionInfo table
    /// </summary>
    public class Phriction : PhabricatorObject<Phabricator.Data.Phriction>
    {
        /// <summary>
        /// Adds or modifies a PhrictionInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="phriction"></param>
        public override void Add(Database database, Phabricator.Data.Phriction phriction)
        {
            using (SQLiteTransaction transaction = database.Connection.BeginTransaction())
            {
                string info = JsonConvert.SerializeObject(new
                {
                    Content = phriction.Content,
                    Author = phriction.Author,
                    LastModifiedBy = phriction.LastModifiedBy,
                    Name = phriction.Name,
                    Projects = phriction.Projects,
                    Subscribers = phriction.Subscribers,
                    DateModified = phriction.DateModified.ToString("yyyy-MM-dd HH:mm:ss zzzz")
                });

                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           INSERT OR REPLACE INTO phrictionInfo(token, path, info) 
                           VALUES (@token, @path, @info);
                       ", database.Connection, transaction))
                {
                    database.AddParameter(dbCommand, "token", phriction.Token, Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "path", phriction.Path);
                    database.AddParameter(dbCommand, "info", info);
                    dbCommand.ExecuteNonQuery();

                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// Adds or replaces an alias PhrictionInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="url"></param>
        /// <param name="linkedDocument"></param>
        public void AddAlias(Database database, string url, Phabricator.Data.Phriction linkedDocument)
        {
            using (SQLiteTransaction transaction = database.Connection.BeginTransaction())
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           INSERT OR REPLACE INTO phrictionInfo(token, path, info) 
                           VALUES (@token, @path, @info);
                       ", database.Connection, transaction))
                {
                    database.AddParameter(dbCommand, "token", linkedDocument.Token.Replace(Phabricator.Data.Phriction.Prefix, Phabricator.Data.Phriction.PrefixAlias), Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "path", url);
                    database.AddParameter(dbCommand, "info", linkedDocument.Token);
                    dbCommand.ExecuteNonQuery();

                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// Returns the number of PhrictionInfo records
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public static long Count(Database database)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT COUNT(*) AS result
                       FROM phrictionInfo
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
        /// Returns all PhrictionInfo records which represent a Phriction document (aliases are ignored)
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public override IEnumerable<Phabricator.Data.Phriction> Get(Database database)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(string.Format(@"
                       SELECT token,
                              path,
                              info
                       FROM phrictionInfo
                       WHERE token LIKE '{0}%';
                   ", Phabricator.Data.Phriction.Prefix),
                   database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Phabricator.Data.Phriction record = new Phabricator.Data.Phriction();
                        record.Token = (string)reader["token"];
                        record.Path = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["path"]);
                        string decryptedInfo = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["info"]);
                        JObject info = JsonConvert.DeserializeObject(decryptedInfo) as JObject;

                        record.Content = (string)info["Content"];
                        record.Author = (string)info["Author"];
                        record.LastModifiedBy = (string)info["LastModifiedBy"];
                        record.Name = (string)info["Name"];
                        record.Projects = (string)info["Projects"];
                        record.Subscribers = (string)info["Subscribers"];
                        record.DateModified = DateTimeOffset.ParseExact((string)info["DateModified"], "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);

                        yield return record;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a specific PhrictionInfo or StageInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="key"></param>
        /// <param name="ignoreStageData"></param>
        /// <returns></returns>
        public override Phabricator.Data.Phriction Get(Database database, string key, bool ignoreStageData = false)
        {
            string url = key;
            if (url.EndsWith("/") == false) url += "/";

            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token,
                              path,
                              info
                       FROM phrictionInfo
                       WHERE token = @key
                          OR path = @encryptedKey
                       ORDER BY token;              -- first PHID-WIKI-, then PHID-WIKIALIAS, then PHID-WIKICOVER
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "key", key, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "encryptedKey", Encryption.Encrypt(database.EncryptionKey, System.Web.HttpUtility.UrlDecode(url).ToLower().Replace(' ', '_')));
                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Phabricator.Data.Phriction record = new Phabricator.Data.Phriction();
                        record.Token = (string)reader["token"];
                        if (record.Token.StartsWith(Phabricator.Data.Phriction.Prefix) || record.Token.StartsWith(Phabricator.Data.Phriction.PrefixCoverPage))
                        {
                            record.Path = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["path"]);
                            string decryptedInfo = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["info"]);
                            JObject info = JsonConvert.DeserializeObject(decryptedInfo) as JObject;

                            record.Content = (string)info["Content"];
                            record.Author = (string)info["Author"];
                            record.LastModifiedBy = (string)info["LastModifiedBy"];
                            record.Name = (string)info["Name"];
                            record.Projects = (string)info["Projects"];
                            record.Subscribers = (string)info["Subscribers"];
                            record.DateModified = DateTimeOffset.ParseExact((string)info["DateModified"], "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);

                            return record;
                        }
                        else
                        {
                            // document is an alias -> retrieve underlying document
                            string decryptedInfo = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["info"]);
                            return Get(database, decryptedInfo);
                        }
                    }
                    else
                    if (ignoreStageData)
                    {
                        return  null;
                    }
                    else
                    {
                        Stage stageStorage = new Stage();
                        return stageStorage.Get<Phabricator.Data.Phriction>(database)
                                           .FirstOrDefault(phrictionDocument => (phrictionDocument.Token ?? "").Equals(key)
                                                                             || phrictionDocument.Path.Equals(key)
                                                          );
                    }
                }
            }
        }

        /// <summary>
        /// Returns all PhrictionInfo records which represent an alias
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        internal IEnumerable<Phabricator.Data.Phriction> GetAliases(Database database)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(string.Format(@"
                       SELECT token,
                              path,
                              info
                       FROM phrictionInfo
                       WHERE token LIKE '{0}%';
                   ", Phabricator.Data.Phriction.PrefixAlias),
                   database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Phabricator.Data.Phriction record = new Phabricator.Data.Phriction();
                        record.Token = (string)reader["token"];
                        record.Path = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["path"]);
                        string decryptedInfo = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["info"]);

                        record.Content = decryptedInfo;

                        yield return record;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a all PhrictionInfo or StageInfo records which are marked as favorite for a given user
        /// </summary>
        /// <param name="database"></param>
        /// <param name="accountUserName"></param>
        /// <returns></returns>
        public IEnumerable<Phabricator.Data.Phriction> GetFavorites(Database database, string accountUserName)
        {
            List<Phabricator.Data.Phriction> result = new List<Phabricator.Data.Phriction>();

            // return favorite unstaged phriction documents
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT phrictionInfo.token,
                              phrictionInfo.path,
                              phrictionInfo.info,
                              favoriteObject.displayOrder
                       FROM phrictionInfo
                       INNER JOIN favoriteObject
                         ON phrictionInfo.token = favoriteObject.token
                       WHERE favoriteObject.accountUserName = @accountUserName
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "accountUserName", accountUserName, Database.EncryptionMode.Default);
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Phabricator.Data.Phriction record = new Phabricator.Data.Phriction();
                        record.Token = (string)reader["token"];
                        if (record.Token.StartsWith(Phabricator.Data.Phriction.Prefix) || record.Token.StartsWith(Phabricator.Data.Phriction.PrefixCoverPage))
                        {
                            record.Path = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["path"]);
                            record.DisplayOrderInFavorites = (Int32)reader["displayOrder"];
                            string decryptedInfo = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["info"]);
                            JObject info = JsonConvert.DeserializeObject(decryptedInfo) as JObject;

                            record.Content = (string)info["Content"];
                            record.Author = (string)info["Author"];
                            record.LastModifiedBy = (string)info["LastModifiedBy"];
                            record.Name = (string)info["Name"];
                            record.Projects = (string)info["Projects"];
                            record.Subscribers = (string)info["Subscribers"];
                            record.DateModified = DateTimeOffset.ParseExact((string)info["DateModified"], "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);

                            result.Add(record);
                        }
                    }
                }
            }

            // return favorite staged phriction documents
            FavoriteObject favoriteObjectStorage = new FavoriteObject();
            Stage stageStorage = new Stage();
            result.AddRange( stageStorage.Get<Phabricator.Data.Phriction>(database)
                                         .Where(document => document.Token.StartsWith("PHID-NEWTOKEN-")
                                                         && favoriteObjectStorage.Get(database, accountUserName, document.Token) != null
                                               )
                                         .Select(stagedDocument => new Phabricator.Data.Phriction(stagedDocument)
                                                                    {
                                                                        DisplayOrderInFavorites = favoriteObjectStorage.Get(database, accountUserName, stagedDocument.Token).DisplayOrder
                                                                    }
                                                )
                           );

            // make sure the first item(s) are not favorite-item splitters
            List<Phabricator.Data.Phriction> orderedResult = result.OrderBy(document => document.DisplayOrderInFavorites)
                                                                   .ToList();
            while (orderedResult.Any() && orderedResult.FirstOrDefault().DisplayOrderInFavorites > 1)  // first display order should be 1
            {
                // correct display order for all subsequent favorite items
                foreach (Phabricator.Data.Phriction resultWithInvalidDisplayOrderInFavorites in orderedResult.ToList())
                {
                    resultWithInvalidDisplayOrderInFavorites.DisplayOrderInFavorites--;
                }
            }

            while (orderedResult.Any() && orderedResult.FirstOrDefault().DisplayOrderInFavorites < 1)  // first display order should be 1
            {
                // correct display order for all subsequent favorite items
                foreach (Phabricator.Data.Phriction resultWithInvalidDisplayOrderInFavorites in orderedResult.ToList())
                {
                    resultWithInvalidDisplayOrderInFavorites.DisplayOrderInFavorites++;
                }
            }

            // return full result
            return orderedResult;
        }

        /// <summary>
        /// Returns the underlying tree of Phriction documents
        /// </summary>
        /// <param name="database"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public PhrictionDocumentTree GetHierarchy(Database database, string key)
        {
            PhrictionDocumentTree result = new PhrictionDocumentTree();

            foreach (string childToken in database.GetUnderlyingTokens(key, "WIKI"))
            {
                Phabricator.Data.Phriction childDocument = Get(database, childToken);
                if (childDocument == null) continue;

                PhrictionDocumentTree childTree = new PhrictionDocumentTree();
                childTree.Data = childDocument;
                result.Add(childTree);

                foreach (string grandchildToken in database.GetUnderlyingTokens(childToken, "WIKI"))
                {
                    Phabricator.Data.Phriction grandchildDocument = Get(database, grandchildToken);
                    if (grandchildDocument == null) continue;

                    PhrictionDocumentTree grandchildTree = new PhrictionDocumentTree();
                    grandchildTree.Data = grandchildDocument;
                    childTree.Add(grandchildTree);
                }
            }

            return result;
        }

        /// <summary>
        /// Validates if a given Phriction document is a favorite document for the given account
        /// </summary>
        /// <param name="database"></param>
        /// <param name="phrictionDocument"></param>
        /// <param name="accountUserName"></param>
        /// <returns></returns>
        public bool IsFavorite(Database database, Phabricator.Data.Phriction phrictionDocument, string accountUserName)
        {
            // return favorite unstaged phriction documents
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT 1
                       FROM favoriteObject
                       WHERE accountUserName = @accountUserName
                         AND token = @phrictionToken
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "accountUserName", accountUserName, Database.EncryptionMode.Default);
                database.AddParameter(dbCommand, "phrictionToken", phrictionDocument.Token ?? "", Database.EncryptionMode.None);
                using (var reader = dbCommand.ExecuteReader())
                {
                    return reader.Read();
                }
            }
        }

        /// <summary>
        /// Removes a Phriction document from the database
        /// </summary>
        /// <param name="database"></param>
        /// <param name="phrictionDocument"></param>
        public void Remove(Database database, Phabricator.Data.Phriction phrictionDocument)
        {
            using (SQLiteCommand cmdDeletePhrictionInfo = new SQLiteCommand(@"
                       DELETE FROM phrictionInfo
                       WHERE token = @token;
                   ", database.Connection))
            {
                database.AddParameter(cmdDeletePhrictionInfo, "token", phrictionDocument.Token, Database.EncryptionMode.None);
                if (cmdDeletePhrictionInfo.ExecuteNonQuery() > 0)
                {
                    using (SQLiteCommand cmdDeleteKeywordInfo = new SQLiteCommand(@"
                               DELETE FROM keywordInfo
                               WHERE token = @token;
                           ", database.Connection))
                    {
                        database.AddParameter(cmdDeleteKeywordInfo, "token", phrictionDocument.Token, Database.EncryptionMode.None);
                        cmdDeleteKeywordInfo.ExecuteNonQuery();
                    }

                    using (SQLiteCommand cmdDeleteFavorites = new SQLiteCommand(@"
                               DELETE FROM favoriteObject
                               WHERE token = @token;
                           ", database.Connection))
                    {
                        database.AddParameter(cmdDeleteFavorites, "token", phrictionDocument.Token, Database.EncryptionMode.None);
                        cmdDeleteFavorites.ExecuteNonQuery();
                    }

                    database.ClearAssignedTokens(phrictionDocument.Token);

                    database.CleanupUnusedObjectRelations();
                }
            }
        }
    }
}
