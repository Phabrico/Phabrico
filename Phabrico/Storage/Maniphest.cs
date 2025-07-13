using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phabrico.Miscellaneous;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;

namespace Phabrico.Storage
{
    /// <summary>
    /// Database mapper for ManiphestInfo table
    /// </summary>
    public class Maniphest : PhabricatorObject<Phabricator.Data.Maniphest>
    {
        /// <summary>
        /// Adds or modifies a ManiphestInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="maniphest"></param>
        public override void Add(Database database, Phabricator.Data.Maniphest maniphest)
        {
            lock (Database.dbLock)
                    {
                using (SQLiteTransaction transaction = database.Connection.BeginTransaction())
                {
                    string info = JsonConvert.SerializeObject(new
                    {
                        Type = maniphest.Type,
                        Name = maniphest.Name,
                        Description = maniphest.Description,
                        Projects = maniphest.Projects,
                        Priority = maniphest.Priority,
                        Author = maniphest.Author,
                        Owner = maniphest.Owner,
                        Subscribers = maniphest.Subscribers,
                        DateCreated = maniphest.DateCreated.ToString("yyyy-MM-dd HH:mm:ss zzzz"),
                        DateModified = maniphest.DateModified.ToString("yyyy-MM-dd HH:mm:ss zzzz")
                    });

                    using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           INSERT OR REPLACE INTO maniphestInfo(token, id, status, info) 
                           VALUES (@token, @id, @status, @info);
                       ", database.Connection, transaction))
                    {
                        database.AddParameter(dbCommand, "token", maniphest.Token, Database.EncryptionMode.None);
                        database.AddParameter(dbCommand, "id", maniphest.ID, Database.EncryptionMode.None);
                        database.AddParameter(dbCommand, "status", maniphest.Status);
                        database.AddParameter(dbCommand, "info", info);
                        dbCommand.ExecuteNonQuery();

                        transaction.Commit();
                    }
                }
            }
        }

        /// <summary>
        /// Copies the Maniphest records from one Phabrico database to another Phabrico database
        /// </summary>
        /// <param name="sourcePhabricoDatabasePath">File path to the source Phabrico database</param>
        /// <param name="sourceUsername">Username to use for authenticating the source Phabrico database</param>
        /// <param name="sourcePassword">Password to use for authenticating the source Phabrico database</param>
        /// <param name="destinationPhabricoDatabasePath">File path to the destination Phabrico database</param>
        /// <param name="destinationUsername">Username to use for authenticating the destination Phabrico database</param>
        /// <param name="destinationPassword">Username to use for authenticating the destination Phabrico database</param>
        /// <param name="filter">LINQ method for filtering the records to be copied</param>
        public static List<Phabricator.Data.Maniphest> Copy(string sourcePhabricoDatabasePath, string sourceUsername, string sourcePassword, string destinationPhabricoDatabasePath, string destinationUsername, string destinationPassword, Func<Phabricator.Data.Maniphest,bool> filter = null)
        {
            string sourceTokenHash = Encryption.GenerateTokenKey(sourceUsername, sourcePassword);  // tokenHash is stored in the database
            string sourcePublicEncryptionKey = Encryption.GenerateEncryptionKey(sourceUsername, sourcePassword);  // encryptionKey is not stored in database (except when security is disabled)
            string sourcePrivateEncryptionKey = Encryption.GeneratePrivateEncryptionKey(sourceUsername, sourcePassword);  // privateEncryptionKey is not stored in database
            string destinationTokenHash = Encryption.GenerateTokenKey(destinationUsername, destinationPassword);  // tokenHash is stored in the database
            string destinationPublicEncryptionKey = Encryption.GenerateEncryptionKey(destinationUsername, destinationPassword);  // encryptionKey is not stored in database (except when security is disabled)
            string destinationPrivateEncryptionKey = Encryption.GeneratePrivateEncryptionKey(destinationUsername, destinationPassword);  // privateEncryptionKey is not stored in database

            string originalDataSource = Storage.Database.DataSource;

            List<Phabricator.Data.Maniphest> maniphestTasks = new List<Phabricator.Data.Maniphest>();
            try
            {
                Storage.Maniphest maniphestStorage = new Storage.Maniphest();

                Storage.Database.DataSource = sourcePhabricoDatabasePath;
                using (Storage.Database database = new Storage.Database(null))
                {
                    bool noUserConfigured;
                    UInt64[] publicXorCipher = database.ValidateLogIn(sourceTokenHash, out noUserConfigured);
                    if (publicXorCipher != null)
                    {
                        database.EncryptionKey = Encryption.XorString(sourcePublicEncryptionKey, publicXorCipher);

                        IEnumerable<Phabricator.Data.Maniphest> sourceManiphestTasks = maniphestStorage.Get(database, Language.NotApplicable);
                        if (filter != null)
                        {
                            sourceManiphestTasks = sourceManiphestTasks.Where(filter);
                        }

                        maniphestTasks = sourceManiphestTasks.ToList();
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

                        foreach (Phabricator.Data.Maniphest maniphestTask in maniphestTasks)
                        {
                            maniphestStorage.Add(database, maniphestTask);
                        }
                    }
                }
            }
            finally
            {
                Storage.Database.DataSource = originalDataSource;
            }

            return maniphestTasks;
        }

        /// <summary>
        /// Returns the number of available ManiphestInfo records
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public static long Count(Database database)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT COUNT(*) AS result
                       FROM maniphestInfo
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
        /// Returns all available ManiphestInfo records (including their states)
        /// </summary>
        /// <param name="database"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public override IEnumerable<Phabricator.Data.Maniphest> Get(Database database, Language language)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT m.token, m.id, m.status, ms.closed, m.info
                       FROM maniphestInfo m, maniphestStatusInfo ms
                       WHERE m.status = ms.value;
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Phabricator.Data.Maniphest record = new Phabricator.Data.Maniphest();
                        record.Token = (string)reader["token"];
                        record.ID = (string)reader["id"];
                        record.Status = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["status"]);
                        record.IsOpen = bool.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["closed"])) == false;
                        string decryptedInfo = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["info"]);
                        JObject info = JsonConvert.DeserializeObject(decryptedInfo) as JObject;
                        if (info == null) continue;

                        record.Name = (string)info["Name"];
                        record.Description = (string)info["Description"];
                        record.Projects = (string)info["Projects"];
                        record.Priority = (string)info["Priority"];
                        record.Author = (string)info["Author"];
                        record.Owner = (string)info["Owner"];
                        record.Subscribers = (string)info["Subscribers"];
                        record.DateCreated = DateTimeOffset.ParseExact((string)info["DateCreated"], "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);
                        record.DateModified = DateTimeOffset.ParseExact((string)info["DateModified"], "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);

                        yield return record;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a specific ManiphestInfo record (including its state)
        /// </summary>
        /// <param name="database"></param>
        /// <param name="key"></param>
        /// <param name="ignoreStageData"></param>
        /// <returns></returns>
        public override Phabricator.Data.Maniphest Get(Database database, string key, Language language, bool ignoreStageData = false)
        {
            Transaction transactionStorage = new Transaction();

            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT m.token, m.id, m.status, ms.closed, m.info
                       FROM maniphestInfo m, maniphestStatusInfo ms
                       WHERE m.status = ms.value
                         AND (token = @key
                          OR  id = @key);
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "key", key, Database.EncryptionMode.None);
                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Phabricator.Data.Maniphest record = new Phabricator.Data.Maniphest();
                        record.Token = (string)reader["token"];
                        record.ID = (string)reader["id"];
                        record.Status = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["status"]);
                        record.IsOpen = bool.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["closed"])) == false;
                        string decryptedInfo = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["info"]);
                        JObject info = JsonConvert.DeserializeObject(decryptedInfo) as JObject;
                        if (info == null) return null;

                        record.Name = (string)info["Name"];
                        record.Description = (string)info["Description"];
                        record.Projects = (string)info["Projects"];
                        record.Priority = (string)info["Priority"];
                        record.Author = (string)info["Author"];
                        record.Owner = (string)info["Owner"];
                        record.Subscribers = (string)info["Subscribers"];
                        record.DateCreated = DateTimeOffset.ParseExact((string)info["DateCreated"], "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);
                        record.DateModified = DateTimeOffset.ParseExact((string)info["DateModified"], "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);

                        record.Transactions = transactionStorage.GetAll(database, record.Token, language);

                        return record;
                    }
                    else
                    {
                        Stage stageStorage = new Stage();
                        return stageStorage.Get<Phabricator.Data.Maniphest>(database, key, language);
                    }
                }
            }
        }

        /// <summary>
        /// Reads staged transaction records from the database and merges them into a given maniphest task
        /// </summary>
        /// <param name="database">Phabrico database</param>
        /// <param name="maniphestTask">Maniphest task to be merged</param>
        /// <param name="language"></param>
        public void LoadStagedTransactionsIntoManiphestTask(Database database, Phabricator.Data.Maniphest maniphestTask, Language language)
        {
            Storage.Stage stageStorage = new Storage.Stage();

            // search for transactions on this ManiphestTask (i.e. modification of priority, status, ...)
            foreach (Phabricator.Data.Transaction stagedTransaction in stageStorage.Get<Phabricator.Data.Transaction>(database, language)
                                                                                   .Where(stageData => stageData.Token.Equals(maniphestTask.Token))
                                                                                   .OrderBy(stageData => stageData.Type))
            {
                switch (stagedTransaction.Type)
                {
                    case "owner":
                        maniphestTask.Owner = stagedTransaction.NewValue;
                        break;

                    case "priority":
                        maniphestTask.Priority = stagedTransaction.NewValue;
                        break;

                    case "status":
                        maniphestTask.Status = stagedTransaction.NewValue;
                        break;

                    case "comment":
                        // don't do anything: comments are shown directly via Phabricator.Data.Transaction
                        break;

                    default:
                        if (stagedTransaction.Type.StartsWith("project-"))
                        {
                            int projectIndex = Int32.Parse(stagedTransaction.Type.Substring("project-".Length));
                            if (projectIndex == 0)
                            {
                                maniphestTask.Projects = stagedTransaction.NewValue;
                            }
                            else
                            {
                                maniphestTask.Projects += "," + stagedTransaction.NewValue;
                            }
                        }
                        else
                        if (stagedTransaction.Type.StartsWith("subscriber-"))
                        {
                            int subscriberIndex = Int32.Parse(stagedTransaction.Type.Substring("subscriber-".Length));
                            if (subscriberIndex == 0)
                            {
                                maniphestTask.Subscribers = stagedTransaction.NewValue;
                            }
                            else
                            {
                                maniphestTask.Subscribers += "," + stagedTransaction.NewValue;
                            }
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Removes an existing ManiphestInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="maniphestTask"></param>
        public void Remove(Database database, Phabricator.Data.Maniphest maniphestTask)
        {
            using (SQLiteCommand cmdDeleteManiphestInfo = new SQLiteCommand(@"
                       DELETE FROM maniphestInfo
                       WHERE token = @token;
                   ", database.Connection))
            {
                database.AddParameter(cmdDeleteManiphestInfo, "token", maniphestTask.Token, Database.EncryptionMode.None);

                lock (Database.dbLock)
                {
                    if (cmdDeleteManiphestInfo.ExecuteNonQuery() > 0)
                    {
                        using (SQLiteCommand cmdDeleteKeywordInfo = new SQLiteCommand(@"
                               DELETE FROM keywordInfo
                               WHERE token = @token;
                           ", database.Connection))
                        {
                            database.AddParameter(cmdDeleteKeywordInfo, "token", maniphestTask.Token, Database.EncryptionMode.None);
                            cmdDeleteKeywordInfo.ExecuteNonQuery();
                        }

                        database.ClearAssignedTokens(maniphestTask.Token, Language.NotApplicable);

                        database.CleanupUnusedObjectRelations();

                        Database.IsModified = true;
                    }
                }
            }
        }
    }
}
