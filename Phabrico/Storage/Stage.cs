using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Parsers.Base64;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Phabrico.Storage
{
    /// <summary>
    /// Database mapper for StageInfo table
    /// </summary>
    public class Stage
    {
        /// <summary>
        /// Represents a StageInfo record
        /// </summary>
        public class Data
        {
            /// <summary>
            /// Token of the object that has been modified
            /// </summary>
            public string Token;

            /// <summary>
            /// Token-prefix of the object that has been modified.
            /// </summary>
            public string TokenPrefix;

            /// <summary>
            /// In case the object is a file to be uploaded, the ObjectID contains a (negative) number to identify the file reference number.
            /// So you can reference the file for example in a staged Maniphest task as {F-123}
            /// After being uploaded and downloaded again, this negative number will be replaced by a Phabricator generated number
            /// </summary>
            public int ObjectID;

            /// <summary>
            /// Identifies the type of modification (e.g. edit, new)
            /// </summary>
            public string Operation;

            /// <summary>
            /// Timestamp when the object was last modified
            /// </summary>
            public DateTimeOffset DateModified;

            /// <summary>
            /// General info about the modified object.
            /// In case the modified object is a file, the content is not stored in this field
            /// </summary>
            public string HeaderData;

            /// <summary>
            /// If true, the object will not be uploaded to Phabricator during the next synchronization action
            /// </summary>
            public bool Frozen;

            /// <summary>
            /// If true, the object was modified in both Phabricator and Phabrico
            /// </summary>
            public bool MergeConflict;

            /// <summary>
            /// Is only used for file objects.
            /// This will contain the content of the file
            /// </summary>
            [JsonIgnore]
            public Base64EIDOStream ContentDataStream;

            /// <summary>
            /// Used by serialization/deserialization during loading/storing the object from/into the local database
            /// </summary>
            public byte[] ContentData
            {
                get
                {
                    if (ContentDataStream == null)
                    {
                        return new byte[0];
                    }

                    byte[] decodedData = new byte[ContentDataStream.Length];
                    ContentDataStream.Seek(0, SeekOrigin.Begin);
                    ContentDataStream.Read(decodedData, 0, decodedData.Length);
                    ContentDataStream.Seek(0, SeekOrigin.Begin);
                    return decodedData;
                }

                set
                {
                    ContentDataStream = new Base64EIDOStream();
                    ContentDataStream.WriteDecodedData(value);
                }
            }
        }

        private Controllers.Staging privateStagingController = new Controllers.Staging();

        /// <summary>
        /// Returns the number of frozen staged objects (i.e. the number of objects that will not be uploaded during the next synchronization process)
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public static long CountFrozen(Database database)
        {
            long result = 0;
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT frozen
                       FROM stageInfo;
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (bool.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["frozen"])) == true)
                        {
                            result++;
                        }
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Returns the number of unfrozen staged objects (i.e. the number of objects that will be uploaded during the next synchronization process)
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public static long CountUncommitted(Database database)
        {
            long result = 0;
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT frozen
                       FROM stageInfo;
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (bool.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["frozen"])) == false)
                        {
                            result++;
                        }
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Creates a new staged object
        /// </summary>
        /// <param name="database"></param>
        /// <param name="newPhabricatorObject"></param>
        /// <returns></returns>
        public string Create(Database database, Phabricator.Data.PhabricatorObject newPhabricatorObject)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT MAX(token) AS lastToken
                       FROM stageInfo
                       WHERE token LIKE 'PHID-NEWTOKEN-%';
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    UInt64 lastNewTokenId = 0;
                    if (reader.Read() && (reader["lastToken"] is DBNull) == false)
                    {
                        lastNewTokenId = UInt64.Parse(System.Text.UTF8Encoding.UTF8.GetString((byte[])reader["lastToken"]).Substring("PHID-NEWTOKEN-".Length));
                    }

                    string operation = "new";
                    Stage.Data stageData = new Data();
                    stageData.Operation = operation;
                    stageData.Token = string.Format("PHID-NEWTOKEN-{0:D16}", lastNewTokenId + 1);
                    newPhabricatorObject.Token = stageData.Token;

                    Phabricator.Data.File stagedFileData = newPhabricatorObject as Phabricator.Data.File;
                    if (stagedFileData != null)
                    {
                        stageData.ObjectID = -(int)(lastNewTokenId + 1);
                        stagedFileData.ID = stageData.ObjectID;
                        if (string.IsNullOrWhiteSpace(stagedFileData.TemplateFileName) == false)
                        {
                            // regenerate FileName based on TemplateFileName
                            stagedFileData.TemplateFileName = stagedFileData.TemplateFileName;
                        }

                        byte[] decryptedData = stagedFileData.Data;
                        string encryptedData = System.Convert.ToBase64String(decryptedData);
                        stageData.ContentDataStream = new Base64EIDOStream(encryptedData);

                        // invalidate cached data
                        Server.InvalidateNonStaticCache(database, DateTime.MaxValue);
                    }

                    stageData.DateModified = DateTimeOffset.UtcNow;
                    stageData.HeaderData = JsonConvert.SerializeObject(newPhabricatorObject);
                    stageData.TokenPrefix = newPhabricatorObject.TokenPrefix;

                    SaveStageData(database, stageData);

                    return stageData.Token;
                }
            }
        }

        /// <summary>
        /// Updates an existing staged object
        /// </summary>
        /// <param name="database"></param>
        /// <param name="fileObject"></param>
        /// <returns></returns>
        public string Edit(Database database, Phabricator.Data.File fileObject)
        {
            if (fileObject.ID > 0)
            {
                // fileObject is not staged yet
                return Create(database, fileObject);
            }
            else
            {
                Modify(database, fileObject);
                return fileObject.Token;
            }
        }

        /// <summary>
        /// Returns all stageInfo records
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public IEnumerable<Data> Get(Database database)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, tokenPrefix, objectID, operation, dateModified, headerData, frozen
                       FROM stageInfo;
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Data record = new Data();
                        record.Token = (string)reader["token"];
                        record.TokenPrefix = Encryption.Decrypt(record.Token, (byte[])reader["tokenPrefix"]);  // Token is used as encryption "seed"
                        record.ObjectID = int.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["objectID"]));
                        record.Operation = (string)reader["operation"];
                        record.DateModified = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateModified"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);
                        record.HeaderData = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["headerData"]);
                        record.Frozen = bool.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["frozen"]));

                        record.MergeConflict = false;
                        if (record.Token.StartsWith(Phabricator.Data.Phriction.Prefix))
                        {
                            Storage.Phriction phrictionStorage = new Storage.Phriction();
                            Phabricator.Data.Phriction phrictionDocument = phrictionStorage.Get(database, record.Token);
                            if (phrictionDocument != null && phrictionDocument.DateModified.ToUnixTimeSeconds() > record.DateModified.ToUnixTimeSeconds())
                            {
                                record.MergeConflict = true;
                            }
                        }
                        else
                        if (record.Token.StartsWith(Phabricator.Data.Maniphest.Prefix))
                        {
                            Storage.Maniphest maniphestStorage = new Storage.Maniphest();
                            Phabricator.Data.Maniphest maniphestTask = maniphestStorage.Get(database, record.Token);
                            if (maniphestTask != null && maniphestTask.DateModified.ToUnixTimeSeconds() > record.DateModified.ToUnixTimeSeconds())
                            {
                                record.MergeConflict = true;
                            }
                        }

                        yield return record;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a specific stageInfo record
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="database"></param>
        /// <param name="tokenPrefix"></param>
        /// <param name="stageObjectID"></param>
        /// <param name="includeContent"></param>
        /// <returns></returns>
        public T Get<T>(Database database, string tokenPrefix, int stageObjectID, bool includeContent) where T : Phabricator.Data.PhabricatorObject
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, tokenPrefix, headerData, contentData
                       FROM stageInfo
                       WHERE objectID = @objectID
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "objectID", stageObjectID, Database.EncryptionMode.Default);

                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Data record = new Data();
                        record.Token = (string)reader["token"];
                        record.TokenPrefix = Encryption.Decrypt(record.Token, (byte[])reader["tokenPrefix"]);  // Token is used as encryption "seed"
                        if (record.TokenPrefix.Equals(tokenPrefix) == false) continue;

                        record.HeaderData = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["headerData"]);

                        T result = JsonConvert.DeserializeObject(record.HeaderData, typeof(T)) as T;

                        if (includeContent)
                        {
                            Phabricator.Data.File stagedFile = result as Phabricator.Data.File;
                            if (stagedFile != null)
                            {
                                string decryptedBase64Data = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["contentData"]);
                                stagedFile.DataStream = new Base64EIDOStream(decryptedBase64Data);
                            }
                        }

                        return result;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Returns all stageInfo records of a given type.
        /// In case the record represents a Phriction alias, the underlying document will be returned.
        /// </summary>
        /// <typeparam name="T">Type of objects to be returned</typeparam>
        /// <param name="database">Phabrico database</param>
        /// <param name="unfrozenOnly">If true, return only the unfrozen records</param>
        /// <returns></returns>
        public IEnumerable<T> Get<T>(Database database, bool unfrozenOnly = false) where T : Phabricator.Data.PhabricatorObject
        {
            string prefixToken;
            if (typeof(T) == typeof(Phabricator.Data.Phriction))
            {
                prefixToken = Phabricator.Data.Phriction.Prefix;
            }
            else
            if (typeof(T) == typeof(Phabricator.Data.Maniphest))
            {
                prefixToken = Phabricator.Data.Maniphest.Prefix;
            }
            else
            if (typeof(T) == typeof(Phabricator.Data.Transaction))
            {
                prefixToken = Phabricator.Data.Transaction.Prefix;
            }
            else
            if (typeof(T) == typeof(Phabricator.Data.File))
            {
                prefixToken = Phabricator.Data.File.Prefix;
            }
            else
            if (typeof(T) == typeof(Phabricator.Data.PhabricatorObject))
            {
                prefixToken = "";
            }
            else
            {
                yield break;
            }

            using (SQLiteCommand dbCommand = new SQLiteCommand(
                       string.Format(@"
                           SELECT token, tokenPrefix, objectID, operation, dateModified, headerData, frozen
                           FROM stageInfo
                           WHERE token LIKE '{0}%'
                              OR (token NOT LIKE 'PHID-NEWTOKEN-%' AND '{0}' = '{1}')
                       ", prefixToken, Phabricator.Data.Transaction.Prefix)
                   , database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Data record = new Data();
                        record.Frozen = bool.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["frozen"]));
                        if (unfrozenOnly && record.Frozen) continue;

                        record.Token = (string)reader["token"];
                        record.TokenPrefix = Encryption.Decrypt(record.Token, (byte[])reader["tokenPrefix"]);  // Token is used as encryption "seed"
                        if (string.IsNullOrEmpty(prefixToken) == false && record.TokenPrefix.Equals(prefixToken) == false) continue;

                        record.HeaderData = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["headerData"]);

                        yield return JsonConvert.DeserializeObject(record.HeaderData, typeof(T)) as T;
                    }
                }
            }

            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, tokenPrefix, objectID, operation, dateModified, headerData, frozen
                       FROM stageInfo
                       WHERE token LIKE 'PHID-NEWTOKEN-%'
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Data record = new Data();

                        record.Frozen = bool.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["frozen"]));
                        if (unfrozenOnly && record.Frozen) continue;

                        record.Token = (string)reader["token"];

                        record.TokenPrefix = Encryption.Decrypt(record.Token, (byte[])reader["tokenPrefix"]);  // Token is used as encryption "seed"
                        if (record.TokenPrefix.Equals(prefixToken) == false) continue;

                        record.HeaderData = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["headerData"]);

                        Phabricator.Data.PhabricatorObject phabricatorObject = JsonConvert.DeserializeObject(record.HeaderData, typeof(T)) as T;
                        phabricatorObject.Token = record.Token;

                        yield return phabricatorObject as T;
                    }
                }
            }

            if (typeof(T) == typeof(Phabricator.Data.Phriction))
            {
                Storage.Stage stageStorage = new Storage.Stage();
                Storage.Phriction phrictionStorage = new Storage.Phriction();

                foreach (Phabricator.Data.Phriction alias in phrictionStorage.GetAliases(database))
                {
                    Phabricator.Data.Phriction stagedDocument = stageStorage.Get<Phabricator.Data.Phriction>(database, alias.Content) as Phabricator.Data.Phriction;
                    if (stagedDocument != null)
                    {
                        stagedDocument.Path = alias.Path;
                        yield return stagedDocument as T;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a specific stageInfo record
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="database"></param>
        /// <param name="token"></param>
        /// <param name="operation"></param>
        /// <returns></returns>
        public T Get<T>(Database database, string token, string operation = null) where T : Phabricator.Data.PhabricatorObject
        {
            string sqlStatement = @"
                SELECT token, tokenPrefix, objectID, operation, dateModified, headerData, frozen
                FROM stageInfo
                WHERE token = @token
            ";

            if (string.IsNullOrEmpty(operation))
            {
                // return staged maniphest task or phriction document
                sqlStatement += " AND (operation LIKE 'edit' OR operation LIKE 'new')";
            }
            else
            {
                // return staged maniphest transaction item
                sqlStatement += " AND operation = @operation";
            }

            sqlStatement += ";";

            using (SQLiteCommand dbCommand = new SQLiteCommand(sqlStatement, database.Connection))
            {
                database.AddParameter(dbCommand, "token", token, Database.EncryptionMode.None);
                if (operation != null)
                {
                    database.AddParameter(dbCommand, "operation", operation, Database.EncryptionMode.None);
                }

                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Data record = new Data();
                        record.HeaderData = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["headerData"]);
                        JObject deserializedObject = JsonConvert.DeserializeObject(record.HeaderData) as JObject;
                        if (deserializedObject == null) continue;
                        if (typeof(T) == typeof(Phabricator.Data.Phriction) && deserializedObject["TokenPrefix"].ToString().Equals(Phabricator.Data.Phriction.Prefix) == false) continue;
                        if (typeof(T) == typeof(Phabricator.Data.Maniphest) && deserializedObject["TokenPrefix"].ToString().Equals(Phabricator.Data.Maniphest.Prefix) == false) continue;

                        record.Token = (string)reader["token"];
                        record.Operation = (string)reader["operation"];
                        record.TokenPrefix = Encryption.Decrypt(record.Token, (byte[])reader["tokenPrefix"]);  // Token is used as encryption "seed"
                        record.ObjectID = int.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["objectID"]));
                        record.DateModified = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateModified"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);
                        record.Frozen = bool.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["frozen"]));

                        T result = null;
                        if (typeof(T) == typeof(Phabricator.Data.PhabricatorObject))
                        {
                            if (record.Token.StartsWith(Phabricator.Data.Phriction.Prefix))
                            {
                                result = JsonConvert.DeserializeObject<Phabricator.Data.Phriction>(record.HeaderData) as T;
                            }
                            else
                            if (record.Token.StartsWith(Phabricator.Data.Maniphest.Prefix))
                            {
                                // find out if we have a Maniphest Task or a transaction on a Maniphest Task
                                if (record.TokenPrefix.Equals(Phabricator.Data.Transaction.Prefix))
                                {
                                    // it's a transaction
                                    result = JsonConvert.DeserializeObject<Phabricator.Data.Transaction>(record.HeaderData) as T;
                                }
                                else
                                {
                                    // it's a maniphest task
                                    result = JsonConvert.DeserializeObject<Phabricator.Data.Maniphest>(record.HeaderData) as T;
                                }
                            }
                            else
                            if (record.Token.StartsWith("PHID-NEWTOKEN-"))
                            {
                                // a new token was created: try to find out what content it has
                                try
                                {
                                    string unknownToken = (string)deserializedObject["TokenPrefix"];
                                    if (unknownToken.StartsWith(Phabricator.Data.Phriction.Prefix))
                                    {
                                        result = JsonConvert.DeserializeObject<Phabricator.Data.Phriction>(record.HeaderData) as T;
                                    }
                                    else
                                    if (unknownToken.StartsWith(Phabricator.Data.Maniphest.Prefix))
                                    {
                                        result = JsonConvert.DeserializeObject<Phabricator.Data.Maniphest>(record.HeaderData) as T;
                                    }
                                }
                                catch
                                {
                                    result = null;
                                }

                                if (result == null)
                                {
                                    try
                                    {
                                        result = JsonConvert.DeserializeObject<Phabricator.Data.Maniphest>(record.HeaderData) as T;
                                    }
                                    catch
                                    {
                                        result = null;
                                    }
                                }
                            }
                        }

                        if (result == null)
                        {
                            result = JsonConvert.DeserializeObject<T>(record.HeaderData);
                        }

                        result.Token = record.Token;

                        return result;
                    }

                    return null;
                }
            }
        }

        /// <summary>
        /// Returns the frozen-state of a given object
        /// </summary>
        /// <param name="database"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public bool IsFrozen(Database database, string token)
        {
            string sqlStatement = @"
                SELECT frozen
                FROM stageInfo
                WHERE token = @token
            ";

            using (SQLiteCommand dbCommand = new SQLiteCommand(sqlStatement, database.Connection))
            {
                database.AddParameter(dbCommand, "token", token, Database.EncryptionMode.None);

                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return bool.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["frozen"])) == true;
                    }

                    return false;
                }
            }
        }

        /// <summary>
        /// Freezes or unfreezes an object.
        /// Freezing means that the object will not uploaded to Phabricator during the nex synchronization action.
        /// </summary>
        /// <param name="database"></param>
        /// <param name="token"></param>
        /// <param name="doFreeze"></param>
        public void Freeze(Database database, string token, bool doFreeze)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       UPDATE stageInfo
                          SET frozen = @frozen
                       WHERE token = @token;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "token", token, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "frozen", Encryption.Encrypt(database.EncryptionKey, doFreeze.ToString()));
                dbCommand.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Modifies a staged object
        /// </summary>
        /// <param name="database"></param>
        /// <param name="modifiedPhabricatorObject"></param>
        public void Modify(Database database, Phabricator.Data.PhabricatorObject modifiedPhabricatorObject)
        {
            string operation = "edit";

            if (modifiedPhabricatorObject.Token.StartsWith("PHID-NEWTOKEN-"))
            {
                operation = "new";
            }
            else
            {
                Phabricator.Data.Transaction modifiedTransaction = modifiedPhabricatorObject as Phabricator.Data.Transaction;
                if (modifiedTransaction != null)
                {
                    // transaction item is modified: correct operation id
                    operation = modifiedTransaction.Type;
                }
                else
                {
                    // maniphest task is modified: check if number of comments to users has been changed (i.e. "@" remarkup-code)
                    Phabricator.Data.Maniphest modifiedManiphestTask = modifiedPhabricatorObject as Phabricator.Data.Maniphest;
                    Storage.Maniphest maniphestStorage = new Storage.Maniphest();
                    Phabricator.Data.Maniphest originalManiphestTask = maniphestStorage.Get(database, modifiedPhabricatorObject.Token);
                    if (originalManiphestTask != null)
                    {
                        string originalCommentsToUsers = string.Join(",", RegexSafe.Matches(originalManiphestTask.Description, "@[a-z0-9._-]*[a-z0-9_-]")
                                                                                   .OfType<Match>()
                                                                                   .Select(match => match.Value)
                                                                                   .OrderBy(user => user));
                        string newCommentsToUsers = string.Join(",", RegexSafe.Matches(modifiedManiphestTask.Description, "@[a-z0-9._-]*[a-z0-9_-]")
                                                                              .OfType<Match>()
                                                                              .Select(match => match.Value)
                                                                              .OrderBy(user => user));
                        if (originalCommentsToUsers.Equals(newCommentsToUsers) == false)
                        {
                            string newSubscriberList = "";
                            Storage.User userStorage = new Storage.User();
                            foreach (string userId in newCommentsToUsers.Split(',').Where(comment => comment.StartsWith("@"))
                                                                                   .Select(comment => comment.Substring("@".Length))
                                    )
                            {
                                Phabricator.Data.User user = userStorage.Get(database, userId);
                                if (user != null)
                                {
                                    if (modifiedManiphestTask.Subscribers.Contains(user.Token) == false)
                                    {
                                        newSubscriberList += "," + user.Token;
                                    }
                                }
                            }

                            if (newSubscriberList.Any())
                            {
                                if (modifiedManiphestTask.Subscribers.Any() == false)
                                {
                                    newSubscriberList = newSubscriberList.Substring(1);  // skip first comma, if any
                                }

                                modifiedManiphestTask.Subscribers += newSubscriberList;
                            }
                        }
                    }
                }
            }

            Stage.Data stageData = new Data();
            stageData.Operation = operation;
            stageData.Token = modifiedPhabricatorObject.Token;
            stageData.DateModified = DateTimeOffset.UtcNow;
            stageData.TokenPrefix = modifiedPhabricatorObject.TokenPrefix;
            stageData.HeaderData = JsonConvert.SerializeObject(modifiedPhabricatorObject);
            stageData.ContentData = new byte[0];

            Phabricator.Data.File modifiedFileObject = modifiedPhabricatorObject as Phabricator.Data.File;
            if (modifiedFileObject != null)
            {
                stageData.ContentDataStream = modifiedFileObject.DataStream;
            }

            Stage.Data previouslyStagedObject = Get(database).FirstOrDefault(obj => obj.Token.Equals(stageData.Token));
            if (previouslyStagedObject != null)
            {
                stageData.Frozen = previouslyStagedObject.Frozen;
                stageData.ObjectID = previouslyStagedObject.ObjectID;
            }
            else
            {
                if (modifiedPhabricatorObject is Phabricator.Data.Phriction)
                {
                    Storage.Account accountStorage = new Storage.Account();
                    if (accountStorage.WhoAmI(database).Parameters.DefaultStateModifiedPhriction == Phabricator.Data.Account.DefaultStateModification.Frozen)
                    {
                        stageData.Frozen = true;
                    }
                    else
                    {
                        stageData.Frozen = false;
                    }
                }

                if (modifiedPhabricatorObject is Phabricator.Data.Maniphest || modifiedPhabricatorObject is Phabricator.Data.Transaction)
                {
                    Storage.Account accountStorage = new Storage.Account();
                    if (accountStorage.WhoAmI(database).Parameters.DefaultStateModifiedManiphest == Phabricator.Data.Account.DefaultStateModification.Frozen)
                    {
                        stageData.Frozen = true;
                    }
                    else
                    {
                        stageData.Frozen = false;
                    }
                }
            }

            SaveStageData(database, stageData);

            // invalidate cached data
            Server.InvalidateNonStaticCache(database, DateTime.MaxValue);
        }

        /// <summary>
        /// Deletes a staged object
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="database"></param>
        /// <param name="existingPhabricatorObject"></param>
        public void Remove(Http.Browser browser, Database database, Phabricator.Data.PhabricatorObject existingPhabricatorObject)
        {
            Phabricator.Data.Maniphest stagedManiphestTask = existingPhabricatorObject as Phabricator.Data.Maniphest;
            Phabricator.Data.Phriction stagedPhrictionDocument = existingPhabricatorObject as Phabricator.Data.Phriction;
            if (stagedPhrictionDocument != null && stagedPhrictionDocument.Token.StartsWith("PHID-NEWTOKEN-"))
            {
                Phriction phrictionStorage = new Phriction();
                foreach (Phabricator.Data.Phriction underlyingStagedPhrictionDocument in phrictionStorage.GetHierarchy(database, stagedPhrictionDocument.Token).Children)
                {
                    Remove(browser, database, underlyingStagedPhrictionDocument);
                }
            }

            Phabricator.Data.Transaction stagedTransaction = existingPhabricatorObject as Phabricator.Data.Transaction;
            if (stagedTransaction != null)
            {
                // delete phabricator object
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           DELETE FROM stageInfo
                           WHERE token = @token
                             AND operation = @operation;
                       ", database.Connection))
                {
                    database.AddParameter(dbCommand, "token", stagedTransaction.Token, Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "operation", stagedTransaction.Type, Database.EncryptionMode.None);
                    dbCommand.ExecuteNonQuery();
                }

                if (stagedTransaction.Type.StartsWith("subscriber-") || stagedTransaction.Type.StartsWith("project-"))
                {
                    string prefixStagedTransactionName = stagedTransaction.Type.Split('-')[0] + "-";
                    int indexStagedTransactionName = Int32.Parse(stagedTransaction.Type.Split('-')[1]);
                    while (true)
                    {
                        indexStagedTransactionName++;

                        // try loading the next newer transaction item
                        string newerStagedTransactionName = prefixStagedTransactionName + indexStagedTransactionName.ToString();
                        Phabricator.Data.Transaction newerStagedTransaction = Get<Phabricator.Data.Transaction>(database, stagedTransaction.Token, newerStagedTransactionName);
                        if (newerStagedTransaction == null) break;

                        // delete this newer transaction item
                        using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                                   DELETE FROM stageInfo
                                   WHERE token = @token
                                       AND operation = @operation;
                               ", database.Connection))
                        {
                            database.AddParameter(dbCommand, "token", newerStagedTransaction.Token, Database.EncryptionMode.None);
                            database.AddParameter(dbCommand, "operation", newerStagedTransaction.Type, Database.EncryptionMode.None);
                            dbCommand.ExecuteNonQuery();
                        }

                        // change the name of this transaction item and save it again
                        indexStagedTransactionName--;
                        newerStagedTransaction.Type = prefixStagedTransactionName + indexStagedTransactionName.ToString();
                        Modify(database, newerStagedTransaction);
                    }
                }
            }
            else
            {
                if (stagedPhrictionDocument != null && stagedPhrictionDocument.Token.StartsWith("PHID-NEWTOKEN-"))
                {
                    // delete links to phabricator object
                    database.UndescendTokenFrom(existingPhabricatorObject.Token);
                }

                // remove search keywords linked to phabricator object to be removed
                Keyword keywordStorage = new Keyword();
                keywordStorage.DeletePhabricatorObject(database, existingPhabricatorObject);

                // delete phabricator object
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           DELETE FROM stageInfo
                           WHERE token = @token
                             AND (operation LIKE 'new' OR operation LIKE 'edit')
                       ", database.Connection))
                {
                    database.AddParameter(dbCommand, "token", existingPhabricatorObject.Token, Database.EncryptionMode.None);
                    dbCommand.ExecuteNonQuery();
                }

                // (re)assign dependent Phabricator objects
                Phabrico.Parsers.Remarkup.RemarkupParserOutput remarkupParserOutput;
                string remarkupContent = null;
                string url = "/";
                if (stagedManiphestTask != null)
                {
                    Storage.Maniphest maniphestStorage = new Storage.Maniphest();
                    Phabricator.Data.Maniphest originalManiphestTask = maniphestStorage.Get(database, stagedManiphestTask.ID, true);
                    if (originalManiphestTask != null)
                    {
                        remarkupContent = originalManiphestTask.Description;
                    }
                }
                else
                if (stagedPhrictionDocument != null)
                {
                    Storage.Phriction phrictionStorage = new Storage.Phriction();
                    Phabricator.Data.Phriction originalPhrictionDocument = phrictionStorage.Get(database, stagedPhrictionDocument.Path, true);
                    if (originalPhrictionDocument != null)
                    {
                        remarkupContent = originalPhrictionDocument.Content;
                        url = "/" + originalPhrictionDocument.Path;
                    }
                }

                if (remarkupContent != null)
                {
                    database.ClearAssignedTokens(existingPhabricatorObject.Token);
                    privateStagingController.browser = browser;
                    privateStagingController.ConvertRemarkupToHTML(database, url, remarkupContent, out remarkupParserOutput, false);
                    foreach (Phabricator.Data.PhabricatorObject linkedPhabricatorObject in remarkupParserOutput.LinkedPhabricatorObjects)
                    {
                        database.AssignToken(existingPhabricatorObject.Token, linkedPhabricatorObject.Token);
                    }
                }

                database.CleanupUnusedObjectRelations();

                // invalidate cached data
                Server.InvalidateNonStaticCache(database, DateTime.MaxValue);
            }
        }

        /// <summary>
        /// Adds or updates a stageInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="stageData"></param>
        private void SaveStageData(Database database, Data stageData)
        {
            if (stageData.Token.StartsWith("PHID-NEWTOKEN-") && stageData.Operation.Equals("new") == false)
            {
                Phabricator.Data.PhabricatorObject newPhabricatorObject = Get<Phabricator.Data.PhabricatorObject>(database, stageData.Token);
                if (newPhabricatorObject.MergeStageData(stageData))
                {
                    stageData.Operation = "new";
                }
            }

            using (SQLiteTransaction transaction = database.Connection.BeginTransaction())
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           INSERT OR REPLACE INTO stageInfo(token, tokenPrefix, objectID, operation, dateModified, headerData, contentData, frozen)
                           VALUES (@token, @tokenPrefix, @objectID, @operation, @dateModified, @headerData, @contentData, @frozen);
                       ", database.Connection, transaction))
                {
                    database.AddParameter(dbCommand, "token", stageData.Token, Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "tokenPrefix", Encryption.Encrypt(stageData.Token, stageData.TokenPrefix));  // Token is used as encryption "seed"
                    database.AddParameter(dbCommand, "objectID", Encryption.Encrypt(database.EncryptionKey, stageData.ObjectID.ToString()));
                    database.AddParameter(dbCommand, "operation", stageData.Operation, Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "dateModified", Encryption.Encrypt(database.EncryptionKey, stageData.DateModified.ToString("yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture)));
                    database.AddParameter(dbCommand, "headerData", Encryption.Encrypt(database.EncryptionKey, stageData.HeaderData));
                    database.AddParameter(dbCommand, "contentData", stageData.ContentDataStream);
                    database.AddParameter(dbCommand, "frozen", Encryption.Encrypt(database.EncryptionKey, stageData.Frozen.ToString()));
                    dbCommand.ExecuteNonQuery();

                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// Removes all staged file objects which are not referenced by any staged Phriction document or Maniphest task
        /// </summary>
        /// <param name="database"></param>
        /// <param name="browser"></param>
        public static void DeleteUnreferencedFiles(Database database, Browser browser)
        {
            Logging.WriteInfo("Staging", "DeleteUnreferencedFiles");

            Storage.Stage stageStorage = new Stage();

            Phabricator.Data.PhabricatorObject[] stagedObjects = stageStorage.Get<Phabricator.Data.PhabricatorObject>(database).ToArray();
            List<Phabricator.Data.File> stagedFiles = stageStorage.Get<Phabricator.Data.File>(database, true).ToList();
            List<int> referencedFileIDs = new List<int>();
            Regex matchFileAttachments = new Regex("{F(-?[0-9]+)[^}]*}");
            foreach (Phabricator.Data.PhabricatorObject stagedObject in stagedObjects.Where(obj => stagedFiles.All(file => file.Token.Equals(obj.Token) == false)).ToList())
            {
                Phabricator.Data.Maniphest maniphestTask = stageStorage.Get<Phabricator.Data.Maniphest>(database, stagedObject.Token);
                if (maniphestTask != null)
                {
                    referencedFileIDs.AddRange( matchFileAttachments.Matches(maniphestTask.Description)
                                                                    .OfType<Match>()
                                                                    .Select(match => Int32.Parse(match.Groups[1].Value))
                                              );
                    continue;
                }

                Phabricator.Data.Phriction phrictionDocument = stageStorage.Get<Phabricator.Data.Phriction>(database, stagedObject.Token);
                if (phrictionDocument != null)
                {
                    referencedFileIDs.AddRange( matchFileAttachments.Matches(phrictionDocument.Content)
                                                                    .OfType<Match>()
                                                                    .Select(match => Int32.Parse(match.Groups[1].Value))
                                              );
                    continue;
                }
            }

            foreach (Phabricator.Data.File stagedFile in stagedFiles.Where(file => referencedFileIDs.Contains(file.ID) == false))
            {
                if (stagedFile.ContentType.Equals("image/drawio") == false)
                {
                    stageStorage.Remove(browser, database, stagedFile);
                }
            }

            // shrinks the database
            database.Shrink();
        }
    }
}