using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Data.References;
using Phabrico.Http;
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
            lock (Database.dbLock)
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

                        Database.IsModified = true;

                        transaction.Commit();
                    }
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
            lock (Database.dbLock)
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

                        Database.IsModified = true;

                        transaction.Commit();
                    }
                }
            }
        }


        /// <summary>
        /// Copies the Phriction records from one Phabrico database to another Phabrico database
        /// </summary>
        /// <param name="sourcePhabricoDatabasePath">File path to the source Phabrico database</param>
        /// <param name="sourceUsername">Username to use for authenticating the source Phabrico database</param>
        /// <param name="sourcePassword">Password to use for authenticating the source Phabrico database</param>
        /// <param name="destinationPhabricoDatabasePath">File path to the destination Phabrico database</param>
        /// <param name="destinationUsername">Username to use for authenticating the destination Phabrico database</param>
        /// <param name="destinationPassword">Username to use for authenticating the destination Phabrico database</param>
        /// <param name="filter">LINQ method for filtering the records to be copied</param>
        public static List<Phabricator.Data.Phriction> Copy(string sourcePhabricoDatabasePath, string sourceUsername, string sourcePassword, string destinationPhabricoDatabasePath, string destinationUsername, string destinationPassword, Func<Phabricator.Data.Phriction, bool> filter = null)
        {
            string sourceTokenHash = Encryption.GenerateTokenKey(sourceUsername, sourcePassword);  // tokenHash is stored in the database
            string sourcePublicEncryptionKey = Encryption.GenerateEncryptionKey(sourceUsername, sourcePassword);  // encryptionKey is not stored in database (except when security is disabled)
            string sourcePrivateEncryptionKey = Encryption.GeneratePrivateEncryptionKey(sourceUsername, sourcePassword);  // privateEncryptionKey is not stored in database
            string destinationTokenHash = Encryption.GenerateTokenKey(destinationUsername, destinationPassword);  // tokenHash is stored in the database
            string destinationPublicEncryptionKey = Encryption.GenerateEncryptionKey(destinationUsername, destinationPassword);  // encryptionKey is not stored in database (except when security is disabled)
            string destinationPrivateEncryptionKey = Encryption.GeneratePrivateEncryptionKey(destinationUsername, destinationPassword);  // privateEncryptionKey is not stored in database

            string originalDataSource = Storage.Database.DataSource;

            List<Phabricator.Data.Phriction> wikiDocuments = new List<Phabricator.Data.Phriction>();
            try
            {
                Storage.Database.DataSource = sourcePhabricoDatabasePath;
                using (Storage.Database database = new Storage.Database(null))
                {
                    bool noUserConfigured;
                    UInt64[] publicXorCipher = database.ValidateLogIn(sourceTokenHash, out noUserConfigured);
                    if (publicXorCipher != null)
                    {
                        database.EncryptionKey = Encryption.XorString(sourcePublicEncryptionKey, publicXorCipher);

                        using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                                SELECT token, path, info
                                FROM phrictionInfo;
                           ", database.Connection))
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
                                    if (info == null) continue;

                                    record.Content = (string)info["Content"];
                                    record.Author = (string)info["Author"];
                                    record.LastModifiedBy = (string)info["LastModifiedBy"];
                                    record.Name = (string)info["Name"];
                                    record.Projects = (string)info["Projects"];
                                    record.Subscribers = (string)info["Subscribers"];
                                    record.DateModified = DateTimeOffset.ParseExact((string)info["DateModified"], "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);

                                    wikiDocuments.Add(record);
                                }
                            }
                        }
                    }
                }

                Storage.Database.DataSource = destinationPhabricoDatabasePath;
                using (Storage.Database database = new Storage.Database(null))
                {
                    Storage.Phriction phrictionStorage = new Storage.Phriction();

                    bool noUserConfigured;
                    UInt64[] publicXorCipher = database.ValidateLogIn(destinationTokenHash, out noUserConfigured);
                    if (publicXorCipher != null)
                    {
                        database.EncryptionKey = Encryption.XorString(destinationPublicEncryptionKey, publicXorCipher);

                        if (filter != null)
                        {
                            wikiDocuments = wikiDocuments.Where(filter).ToList();
                        }

                        foreach (Phabricator.Data.Phriction wikiDocument in wikiDocuments)
                        {
                            phrictionStorage.Add(database, wikiDocument);
                        }
                    }
                }
            }
            finally
            {
                Storage.Database.DataSource = originalDataSource;
            }

            return wikiDocuments;
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
        public override IEnumerable<Phabricator.Data.Phriction> Get(Database database, Language language)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(string.Format(@"
                       SELECT phrictionInfo.token,
                              phrictionInfo.path,
                              phrictionInfo.info,
                              contentTranslation.translation
                       FROM phrictionInfo
                       LEFT OUTER JOIN Translation.contentTranslation
                         ON phrictionInfo.token = contentTranslation.token
                        AND contentTranslation.language = @language
                       WHERE phrictionInfo.token LIKE '{0}%';
                   ", Phabricator.Data.Phriction.Prefix),
                   database.Connection))
            {
                database.AddParameter(dbCommand, "language", language, Database.EncryptionMode.Default);
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Phabricator.Data.Phriction record = new Phabricator.Data.Phriction();
                        record.Token = (string)reader["token"];
                        record.Path = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["path"]);
                        string decryptedInfo = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["info"]);
                        JObject info = JsonConvert.DeserializeObject(decryptedInfo) as JObject;
                        if (info == null) continue;

                        if (reader["translation"] is DBNull)
                        {
                            record.Content = (string)info["Content"];
                        }
                        else
                        {
                            record.Content = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["translation"]);
                        }
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
        public override Phabricator.Data.Phriction Get(Database database, string key, Language language, bool ignoreStageData = false)
        {
            return Get(database, key, language, ignoreStageData, true);
        }

        /// <summary>
        /// Returns a specific PhrictionInfo or StageInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="key"></param>
        /// <param name="ignoreStageData"></param>
        /// <param name="includeAliases"></param>
        /// <returns></returns>
        public Phabricator.Data.Phriction Get(Database database, string key, Language language, bool ignoreStageData, bool includeAliases)
        {
            Phabricator.Data.Phriction result = null;
            string url = key;
            if (url.EndsWith("/") == false) url += "/";

            if (ignoreStageData == false)
            {
                Stage stageStorage = new Stage();
                result = stageStorage.Get<Phabricator.Data.Phriction>(database, language)
                                     .FirstOrDefault(phrictionDocument => (phrictionDocument.Token ?? "").Equals(key)
                                                                       || phrictionDocument.Path.TrimEnd('/').Equals(url.TrimEnd('/'))
                                                    );
            }

            if (result == null)
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT phrictionInfo.token,
                              phrictionInfo.path,
                              phrictionInfo.info,
                              contentTranslation.title,
                              contentTranslation.translation
                       FROM phrictionInfo
                       LEFT OUTER JOIN Translation.contentTranslation
                         ON phrictionInfo.token = contentTranslation.token
                        AND contentTranslation.language = @language
                       WHERE phrictionInfo.token = @key
                          OR phrictionInfo.path = @url
                       ORDER BY phrictionInfo.token;              -- first PHID-WIKI-, then PHID-WIKIALIAS, then PHID-WIKICOVER
                   ", database.Connection))
                {
                    database.AddParameter(dbCommand, "key", key, Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "url", Encryption.Encrypt(database.EncryptionKey, System.Web.HttpUtility.UrlDecode(url).ToLower().Replace(' ', '_')));
                    database.AddParameter(dbCommand, "language", language, Database.EncryptionMode.None);
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
                                if (info == null) return null;

                                if (reader["translation"] is DBNull)
                                {
                                    record.Name = (string)info["Name"];
                                    record.Content = (string)info["Content"];
                                    record.Language = Language.NotApplicable;
                                }
                                else
                                {
                                    record.Name = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["title"]);
                                    record.Content = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["translation"]);
                                    record.Language = language;
                                }
                                record.Author = (string)info["Author"];
                                record.LastModifiedBy = (string)info["LastModifiedBy"];
                                record.Projects = (string)info["Projects"];
                                record.Subscribers = (string)info["Subscribers"];
                                record.DateModified = DateTimeOffset.ParseExact((string)info["DateModified"], "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);

                                result = record;
                            }
                            else
                            {
                                // document is an alias -> retrieve underlying document
                                string decryptedInfo = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["info"]);
                                result = Get(database, decryptedInfo, language);
                            }
                        }
                    }
                }
            }

            if (result == null)
            {
                if (includeAliases)
                {
                    Phabricator.Data.Phriction[] aliases = GetAliases(database).ToArray();
                    foreach (Phabricator.Data.Phriction alias in aliases)
                    {
                        if (alias.Path.Equals(key.TrimEnd('/') + "/")) continue;

                        Phabricator.Data.Phriction reference = Get(database, alias.Content, language, false, false);
                        if (reference != null)
                        {
                            if (key.StartsWith(reference.Path)) continue;

                            result = Get(database, reference.Path + key.TrimEnd('/') + "/", language);
                            if (result != null) break;

                            // check if reference-path-url and key-url overlap (this can happen when the database was downloaded by the commandline with the initialPath parameter)
                            if (reference.Path.TrimEnd('/').Split('/').LastOrDefault() == key.TrimStart('/').Split('/').FirstOrDefault())
                            {
                                // overlap found
                                string[] referenceParts = reference.Path.TrimEnd('/').Split('/');
                                string[] keyParts = key.TrimStart('/').Split('/');

                                string nonOverlappedUrl = string.Join("/", referenceParts.Take(referenceParts.Length - 1))
                                                        + "/"
                                                        + string.Join("/", keyParts);
                                result = Get(database, nonOverlappedUrl, language);
                            }
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Returns all PhrictionInfo records which represent an alias
        /// </summary>
        /// <param name="database"></param>
        /// <param name="language"></param>
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
        /// <param name="browser"></param>
        /// <param name="accountUserName"></param>
        /// <returns></returns>
        public IEnumerable<Phabricator.Data.Phriction> GetFavorites(Database database, Browser browser, string accountUserName)
        {
            List<Phabricator.Data.Phriction> result = new List<Phabricator.Data.Phriction>();

            // return favorite unstaged phriction documents
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT phrictionInfo.token,
                              phrictionInfo.path,
                              phrictionInfo.info,
                              favoriteObject.displayOrder,
                              contentTranslation.translation
                       FROM phrictionInfo
                       INNER JOIN favoriteObject
                         ON phrictionInfo.token = favoriteObject.token
                       LEFT OUTER JOIN Translation.contentTranslation
                         ON phrictionInfo.token = contentTranslation.token
                        AND contentTranslation.language = @language
                       WHERE favoriteObject.accountUserName = @accountUserName
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "accountUserName", accountUserName, Database.EncryptionMode.Default);
                database.AddParameter(dbCommand, "language", browser.Session.Locale, Database.EncryptionMode.Default);
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
                            if (info == null) continue;

                            if (reader["translation"] is DBNull)
                            {
                                record.Content = (string)info["Content"];
                            }
                            else
                            {
                                record.Content = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["translation"]);
                            }
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
            result.AddRange(stageStorage.Get<Phabricator.Data.Phriction>(database, browser.Session.Locale)
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
                                                                   .Where(document => browser.HttpServer.ValidUserRoles(database, browser, document))
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
        /// <param name="browser"></param>
        /// <param name="key"></param>
        /// <param name="depthLevel"></param>
        /// <returns></returns>
        public PhrictionDocumentTree GetHierarchy(Database database, Http.Browser browser, string key, int depthLevel = 2)
        {
            PhrictionDocumentTree result = new PhrictionDocumentTree();

            foreach (string childToken in database.GetUnderlyingTokens(key, "WIKI", browser))
            {
                Phabricator.Data.Phriction childDocument = Get(database, childToken, browser.Session.Locale);
                if (childDocument == null) continue;
                if (string.IsNullOrEmpty(childDocument.Content))
                {
                    if (database.GetUnderlyingTokens(childDocument.Token, "WIKI", browser).Any() == false)
                    {
                        continue;
                    }
                }
                if (browser.HttpServer.ValidUserRoles(database, browser, childDocument) == false) continue;

                PhrictionDocumentTree childTree = new PhrictionDocumentTree();
                childTree.Data = childDocument;
                result.Add(childTree);

                if (depthLevel > 1)
                {
                    foreach (string grandchildToken in database.GetUnderlyingTokens(childToken, "WIKI", browser))
                    {
                        Phabricator.Data.Phriction grandchildDocument = Get(database, grandchildToken, browser.Session.Locale);
                        if (string.IsNullOrEmpty(grandchildDocument.Content))
                        {
                            if (database.GetUnderlyingTokens(grandchildDocument.Token, "WIKI", browser).Any() == false)
                            {
                                continue;
                            }
                        }
                        if (browser.HttpServer.ValidUserRoles(database, browser, grandchildDocument) == false) continue;

                        PhrictionDocumentTree grandchildTree = new PhrictionDocumentTree();
                        grandchildTree.Data = grandchildDocument;
                        childTree.Add(grandchildTree);

                        if (depthLevel > 2)
                        {
                            foreach (string greatgrandchildToken in database.GetUnderlyingTokens(grandchildToken, "WIKI", browser))
                            {
                                Phabricator.Data.Phriction greatgrandchildDocument = Get(database, greatgrandchildToken, browser.Session.Locale);
                                if (string.IsNullOrEmpty(greatgrandchildDocument.Content))
                                {
                                    if (database.GetUnderlyingTokens(greatgrandchildDocument.Token, "WIKI", browser).Any() == false)
                                    {
                                        continue;
                                    }
                                }
                                if (browser.HttpServer.ValidUserRoles(database, browser, greatgrandchildDocument) == false) continue;

                                PhrictionDocumentTree greatgrandchildTree = new PhrictionDocumentTree();
                                greatgrandchildTree.Data = greatgrandchildDocument;
                                grandchildTree.Add(greatgrandchildTree);
                            }
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Hides or Shows a wiki page (and all underlying documents) from the global search results
        /// </summary>
        /// <param name="database">Phabrico database</param>
        /// <param name="phriction">Phriction document to hide/show</param>
        /// <param name="accountUserName">User account for which the wiki document should be hidden/shown</param>
        /// <param name="doHide">True: hide document in search results; False: show document in search results</param>
        public void HideFromSearchResults(Database database, Phabricator.Data.Phriction phriction, string accountUserName, bool doHide)
        {
            lock (Database.dbLock)
            {
                using (SQLiteTransaction transaction = database.Connection.BeginTransaction())
                {
                    if (doHide)
                    {
                        using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           INSERT OR REPLACE INTO keywordHiddenTokens(accountUserName, url) 
                           VALUES (@accountUserName, @url);
                       ", database.Connection, transaction))
                        {
                            database.AddParameter(dbCommand, "accountUserName", accountUserName, Database.EncryptionMode.Default);
                            database.AddParameter(dbCommand, "url", phriction.Path, Database.EncryptionMode.None);
                            if (dbCommand.ExecuteNonQuery() > 0)
                            {
                                Database.IsModified = true;
                            }
                        }
                    }
                    else
                    {
                        using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           DELETE FROM keywordHiddenTokens
                           WHERE accountUserName = @accountUserName
                             AND url = @url;
                       ", database.Connection, transaction))
                        {
                            database.AddParameter(dbCommand, "accountUserName", accountUserName, Database.EncryptionMode.Default);
                            database.AddParameter(dbCommand, "url", phriction.Path, Database.EncryptionMode.None);
                            if (dbCommand.ExecuteNonQuery() > 0)
                            {
                                Database.IsModified = true;
                            }
                        }
                    }

                    transaction.Commit();
                }
            }
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
        /// Validates if a given Phriction document is hidden from the search results for the given account
        /// </summary>
        /// <param name="database"></param>
        /// <param name="browser"></param>
        /// <param name="url"></param>
        /// <param name="accountUserName"></param>
        /// <returns></returns>
        public bool IsHiddenFromSearchResults(Database database, Http.Browser browser, string url, string accountUserName)
        {
            // return favorite unstaged phriction documents
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT 1
                       FROM keywordHiddenTokens
                       WHERE accountUserName = @accountUserName
                         AND LOWER(SUBSTR(@url, 1, LENGTH(url))) || '/' = LOWER(url) || '/'
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "accountUserName", accountUserName, Database.EncryptionMode.Default);
                database.AddParameter(dbCommand, "url", url, Database.EncryptionMode.None);
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
            lock (Database.dbLock)
            {
                using (SQLiteCommand cmdDeletePhrictionInfo = new SQLiteCommand(@"
                       DELETE FROM phrictionInfo
                       WHERE token = @token;
                   ", database.Connection))
                {
                    database.AddParameter(cmdDeletePhrictionInfo, "token", phrictionDocument.Token, Database.EncryptionMode.None);
                    if (cmdDeletePhrictionInfo.ExecuteNonQuery() > 0)
                    {
                        Database.IsModified = true;

                        using (SQLiteCommand cmdDeleteKeywordInfo = new SQLiteCommand(@"
                               DELETE FROM keywordInfo
                               WHERE token = @token;
                           ", database.Connection))
                        {
                            database.AddParameter(cmdDeleteKeywordInfo, "token", phrictionDocument.Token, Database.EncryptionMode.Default);
                            cmdDeleteKeywordInfo.ExecuteNonQuery();
                        }

                        using (SQLiteCommand cmdDeleteHiddenKeywordTokens = new SQLiteCommand(@"
                               DELETE FROM keywordHiddenTokens
                               WHERE url = @url;
                           ", database.Connection))
                        {
                            database.AddParameter(cmdDeleteHiddenKeywordTokens, "url", phrictionDocument.Path, Database.EncryptionMode.None);
                            cmdDeleteHiddenKeywordTokens.ExecuteNonQuery();
                        }

                        using (SQLiteCommand cmdDeleteFavorites = new SQLiteCommand(@"
                               DELETE FROM favoriteObject
                               WHERE token = @token;
                           ", database.Connection))
                        {
                            database.AddParameter(cmdDeleteFavorites, "token", phrictionDocument.Token, Database.EncryptionMode.None);
                            cmdDeleteFavorites.ExecuteNonQuery();
                        }

                        using (SQLiteCommand cmdDeleteContentTranslations = new SQLiteCommand(@"
                               DELETE FROM objectHierarchyInfo
                               WHERE token = @token
                                  OR parentToken = @token;
                           ", database.Connection))
                        {
                            database.AddParameter(cmdDeleteContentTranslations, "token", phrictionDocument.Token, Database.EncryptionMode.None);
                            cmdDeleteContentTranslations.ExecuteNonQuery();
                        }

                        using (SQLiteCommand cmdDeleteContentTranslations = new SQLiteCommand(@"
                               DELETE FROM Translation.contentTranslation
                               WHERE token = @token;
                           ", database.Connection))
                        {
                            database.AddParameter(cmdDeleteContentTranslations, "token", phrictionDocument.Token, Database.EncryptionMode.None);
                            cmdDeleteContentTranslations.ExecuteNonQuery();
                        }

                        database.ClearAssignedTokens(phrictionDocument.Token, null);  // language=null => all languages

                        database.CleanupUnusedObjectRelations();
                    }
                }
            }
        }

        /// <summary>
        /// Removes old child document trees from a versioned document trees
        /// </summary>
        /// <param name="database">Phabrico database</param>
        /// <param name="browser">Browser</param>
        public void CleanupOldVersions(Database database, Browser browser)
        {
            Storage.Account accountStorage = new Storage.Account();
            Phabricator.Data.Account account = accountStorage.WhoAmI(database, browser);
            if (account == null) return;

            List<string> versionedRoots = account.Parameters.VersionedDocumentRoots;
            if (versionedRoots == null || versionedRoots.Count == 0) return;

            foreach (string root in versionedRoots)
            {
                string normalizedRoot = root.ToLower().Replace(' ', '_').Trim();
                if (!normalizedRoot.EndsWith("/")) normalizedRoot += "/";
                if (normalizedRoot.StartsWith("/")) normalizedRoot = normalizedRoot.Substring(1);
                if (normalizedRoot.StartsWith("w/")) normalizedRoot = normalizedRoot.Substring(2);
                if (string.IsNullOrEmpty(normalizedRoot)) continue;

                // Get all documents under this root
                var documentsUnderRoot = Get(database, Language.NotApplicable)
                    .Where(doc => doc.Path.ToLower().Replace(' ', '_').StartsWith(normalizedRoot))
                    .ToList();

                // Identify direct child documents (one level below root)
                var childDocuments = documentsUnderRoot
                    .Where(doc =>
                    {
                        string normalizedPath = doc.Path.ToLower().Replace(' ', '_');
                        int nextSlash = normalizedPath.IndexOf('/', normalizedRoot.Length);
                        return nextSlash == normalizedPath.Length - 1;
                    })
                    .ToList();

                // Filter to versioned child documents
                var versionedChildDocuments = childDocuments
                    .Where(doc =>
                    {
                        string childPath = doc.Path.Substring(normalizedRoot.Length).TrimEnd('/');
                        return IsVersionSegment(childPath);
                    })
                    .ToList();

                if (versionedChildDocuments.Count > 1)
                {
                    // Map documents to their parsed versions
                    var versionDocMap = versionedChildDocuments
                        .Select(doc =>
                        {
                            string versionSegment = doc.Path.Substring(normalizedRoot.Length).TrimEnd('/');
                            string versionPart = ExtractNumericVersionPart(versionSegment);
                            return new { Doc = doc, Version = ParseVersion(versionPart) };
                        })
                        .Where(x => x.Version != null)
                        .ToList();

                    if (versionDocMap.Any())
                    {
                        // Find the document with the highest version
                        var latestDoc = versionDocMap
                            .OrderByDescending(x => x.Version)
                            .First().Doc;

                        // Delete all other versioned documents and their subtrees
                        foreach (var oldDoc in versionDocMap.Where(x => x.Doc != latestDoc).Select(x => x.Doc))
                        {
                            string oldVersionPath = oldDoc.Path.ToLower().Replace(' ', '_');
                            var documentsToDelete = documentsUnderRoot
                                .Where(doc => doc.Path.ToLower().Replace(' ', '_').StartsWith(oldVersionPath))
                                .ToList();

                            foreach (var docToDelete in documentsToDelete)
                            {
                                Remove(database, docToDelete);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extract numeric version part of version strings (e.g. "v1.0" -> "1.0")
        /// </summary>
        /// <param name="versionString"></param>
        /// <returns></returns>
        private string ExtractNumericVersionPart(string versionString)
        {
            var match = System.Text.RegularExpressions.Regex.Match(versionString, @"^(.*?)(\d+(\.\d+)*)$");
            return match.Success ? match.Groups[2].Value : null;
        }

        /// <summary>
        /// Converts a version string into a Version object
        /// </summary>
        /// <param name="versionString"></param>
        /// <returns></returns>
        private Version ParseVersion(string versionString)
        {
            return Version.TryParse(versionString, out Version version) ? version : null;
        }

        /// <summary>
        /// Validates if a string is a version string
        /// </summary>
        /// <param name="data">String to be validated</param>
        /// <returns>True if data is valid version string</returns>
        private bool IsVersionSegment(string data)
        {
            string versionPart = ExtractNumericVersionPart(data);
            return versionPart != null && ParseVersion(versionPart) != null;
        }
    }
}
