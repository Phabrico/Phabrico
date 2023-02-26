using Phabrico.Miscellaneous;
using System.Data.SQLite;

namespace Phabrico.Storage
{
    /// <summary>
    /// Represents a link to a Phriction document which was not downloaded from Phabricator
    /// </summary>
    public class BannedObject
    {
        /// <summary>
        /// Creates a new banned phriction document in the Phabrico database
        /// </summary>
        /// <param name="database"></param>
        /// <param name="reference"></param>
        /// <param name="title"></param>
        public void Add(Database database, string reference, string title)
        {
            using (SQLiteTransaction transaction = database.Connection.BeginTransaction())
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           DELETE FROM bannedObjectInfo WHERE key = @key;

                           INSERT INTO bannedObjectInfo(key, title) 
                           VALUES (@key, @title);
                       ", database.Connection, transaction))
                {
                    database.AddParameter(dbCommand, "key", reference);
                    database.AddParameter(dbCommand, "title", title);
                    if (dbCommand.ExecuteNonQuery() > 0)
                    {
                        Database.IsModified = true;
                    }

                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// Removes all banned objects from the Phabrico database
        /// </summary>
        /// <param name="database"></param>
        public void Clear(Database database)
        {
            using (SQLiteCommand sqlCommand = new SQLiteCommand(@"
                       DELETE FROM bannedObjectInfo;
                   ", database.Connection))
            {
                if (sqlCommand.ExecuteNonQuery() > 0)
                {
                    Database.IsModified = true;
                }
            }
        }

        /// <summary>
        /// Verifies if a Phriction document was downloaded or not
        /// </summary>
        /// <param name="database"></param>
        /// <param name="reference"></param>
        /// <param name="title"></param>
        /// <returns></returns>
        public bool Exists(Database database, string reference, ref string title)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT title
                       FROM bannedObjectInfo
                       WHERE key = @key;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "key", reference);
                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        title = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["title"]);
                        return true;
                    }
                    
                    return false;
                }
            }
        }
    }
}
