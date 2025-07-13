using Phabrico.Miscellaneous;
using Phabrico.Storage;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Phabrico.Plugin.Storage
{
    /// <summary>
    /// Database mapper for GitanosConfiguration table
    /// </summary>
    public class GitanosConfigurationRootPath : Phabrico.Storage.PhabricatorObject<Plugin.Model.GitanosConfigurationRootPath>
    {
        /// <summary>
        /// Not used: use Overwrite method instead
        /// </summary>
        /// <param name="database"></param>
        /// <param name="rootPath"></param>
        public override void Add(Phabrico.Storage.Database database, Plugin.Model.GitanosConfigurationRootPath rootPath)
        {
            throw new System.NotImplementedException("Use GitanosConfigurationRootPath::Overwrite instead");
        }

        /// <summary>
        /// Overwrites all the GitanosConfigurationRootPath records with a given record set
        /// </summary>
        /// <param name="database">Link to SQLite database</param>
        /// <param name="rootPaths">Recordset to be written to database</param>
        /// <returns>True if all records were successfully written to the database</returns>
        public void Overwrite(Phabrico.Storage.Database database, IEnumerable<Plugin.Model.GitanosConfigurationRootPath> rootPaths)
        {
            lock (Database.dbLock)
            {
                using (SQLiteTransaction sqlTransaction = database.Connection.BeginTransaction())
                {
                    using (SQLiteCommand dbDeleteCommand = new SQLiteCommand(@"
                           DELETE FROM gitanosConfigurationRootPath;
                       ", database.Connection))
                    {
                        dbDeleteCommand.ExecuteNonQuery();
                    }

                    foreach (Plugin.Model.GitanosConfigurationRootPath rootPath in rootPaths)
                    {
                        using (SQLiteCommand dbInsertCommand = new SQLiteCommand(@"
                               INSERT OR REPLACE INTO gitanosConfigurationRootPath(directory) 
                               VALUES (@directory);
                           ", database.Connection))
                        {
                            database.AddParameter(dbInsertCommand, "directory", rootPath.Directory, Phabrico.Storage.Database.EncryptionMode.None);
                            dbInsertCommand.ExecuteNonQuery();
                        }
                    }

                    sqlTransaction.Commit();
                }
            }
        }

        public override IEnumerable<Plugin.Model.GitanosConfigurationRootPath> Get(Phabrico.Storage.Database database, Language language)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT directory
                       FROM gitanosConfigurationRootPath
                       ORDER BY directory;
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Plugin.Model.GitanosConfigurationRootPath record = new Plugin.Model.GitanosConfigurationRootPath();
                        record.Directory = (string)reader["directory"];
                        
                        yield return record;
                    }
                }
            }
        }

        public override Plugin.Model.GitanosConfigurationRootPath Get(Phabrico.Storage.Database database, string key, Language language, bool ignoreStageData)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT directory
                       FROM gitanosConfigurationRootPath
                       WHERE directory = @directory;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "directory", key, Phabrico.Storage.Database.EncryptionMode.None);
                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Plugin.Model.GitanosConfigurationRootPath record = new Plugin.Model.GitanosConfigurationRootPath();
                        record.Directory = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["directory"]);
                        
                        return record;
                    }
                }
            }

            return null;
        }
    }
}
