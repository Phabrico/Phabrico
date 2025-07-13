using Phabrico.Miscellaneous;
using Phabrico.Parsers.Base64;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Phabrico.Storage
{
    /// <summary>
    /// Database mapper for DiagramInfo tables
    /// </summary>
    public class Diagram : PhabricatorObject<Phabricator.Data.Diagram>
    {
        /// <summary>
        /// Contains the median size of all file objects in the SQLite database
        /// In case this property is set to a value lower than zero, the value will be recalculated by 
        /// reading all the file objects in the database
        /// </summary>
        internal static long CachedMedianSize = -1;

        /// <summary>
        /// Contains the maximum size of all file objects in the SQLite database
        /// In case this property is set to a value lower than zero, the value will be recalculated by 
        /// reading all the file objects in the database
        /// </summary>
        internal static long CachedMaximumSize = -1;

        /// <summary>
        /// Adds a new record to the FileInfo table
        /// </summary>
        /// <param name="database"></param>
        /// <param name="diagram"></param>
        public override void Add(Database database, Phabricator.Data.Diagram diagram)
        {
            lock (Database.dbLock)
                {
                using (SQLiteTransaction transaction = database.Connection.BeginTransaction())
                {
                    using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           INSERT OR REPLACE INTO diagramInfo(token, id, fileName, data, size, dateModified)
                           VALUES (@token, @id, @fileName, @data, @size, @dateModified);
                       ", database.Connection, transaction))
                    {
                        database.AddParameter(dbCommand, "token", diagram.Token, Database.EncryptionMode.None);
                        database.AddParameter(dbCommand, "id", diagram.ID.ToString(), Database.EncryptionMode.None);
                        database.AddParameter(dbCommand, "fileName", diagram.FileName);
                        database.AddParameter(dbCommand, "data", diagram.DataStream);
                        database.AddParameter(dbCommand, "size", diagram.Size.ToString());
                        database.AddParameter(dbCommand, "dateModified", diagram.DateModified);
                        if (dbCommand.ExecuteNonQuery() > 0)
                        {
                            Database.IsModified = true;
                        }
                        transaction.Commit();
                    }
                }
            }
        }

        /// <summary>
        /// Returns a specific DiagramInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="key"></param>
        /// <param name="ignoreStageData"></param>
        /// <returns></returns>
        public override Phabricator.Data.Diagram Get(Database database, string key, Language language, bool ignoreStageData = false)
        {
            // search by token
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, id, fileName, data, size, dateModified
                       FROM diagramInfo
                       WHERE token = @key;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "key", key, Database.EncryptionMode.None);
                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        string decryptedBase64Data = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["data"]);
                        byte[] decryptedData = System.Convert.FromBase64String(decryptedBase64Data);
                        Base64EIDOStream base64EIDOStream = new Base64EIDOStream();
                        base64EIDOStream.WriteDecodedData(decryptedData);

                        Phabricator.Data.Diagram record = new Phabricator.Data.Diagram();
                        record.Token = (string)reader["token"];
                        record.ID = (Int32)reader["id"];
                        record.FileName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["fileName"]);
                        record.DataStream = base64EIDOStream;
                        record.DateModified = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateModified"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);
                        record.Size = Int32.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["size"]));

                        return record;
                    }
                }
            }

            // token not found: search further by macro name
            if (key.StartsWith(":"))
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, id, diagramName, data, size, dateModified
                       FROM fileInfo
                   ", database.Connection))
                {
                    using (var reader = dbCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string macroName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["macroName"]);
                            if (string.IsNullOrEmpty(macroName)) continue;
                            if (macroName.Equals(key) == false) continue;

                            string decryptedBase64Data = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["data"]);
                            byte[] decryptedData = System.Convert.FromBase64String(decryptedBase64Data);
                            Base64EIDOStream base64EIDOStream = new Base64EIDOStream();
                            base64EIDOStream.WriteDecodedData(decryptedData);

                            Phabricator.Data.Diagram record = new Phabricator.Data.Diagram();
                            record.Token = (string)reader["token"];
                            record.ID = (Int32)reader["id"];
                            record.FileName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["fileName"]);
                            record.DataStream = base64EIDOStream;
                            record.DateModified = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateModified"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);
                            record.Size = Int32.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["size"]));

                            return record;
                        }
                    }
                }
            }

            if (ignoreStageData == false && key.StartsWith("PHID-NEWTOKEN"))
            {
                int diagramID = Int32.Parse(key.Substring("PHID-NEWTOKEN".Length));

                Storage.Stage stageStorage = new Storage.Stage();
                Phabricator.Data.Diagram record = stageStorage.Get<Phabricator.Data.Diagram>(database, language, Phabricator.Data.Diagram.Prefix, diagramID, true);
                return record;
            }

            return null;
        }

        /// <summary>
        /// Returns a bunch of DiagramInfo records
        /// </summary>
        /// <param name="database"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public override IEnumerable<Phabricator.Data.Diagram> Get(Database database, Language language)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, id, fileName, data, size, dateModified
                       FROM diagramInfo;
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string decryptedBase64Data = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["data"]);
                        byte[] decryptedData = System.Convert.FromBase64String(decryptedBase64Data);

                        Phabricator.Data.Diagram record = new Phabricator.Data.Diagram();
                        record.Token = (string)reader["token"];
                        record.ID = (Int32)reader["id"];
                        record.DataStream = new Base64EIDOStream();
                        record.DataStream.WriteDecodedData(decryptedData);
                        record.DateModified = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateModified"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);
                        record.FileName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["fileName"]);
                        record.Size = Int32.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["size"]));

                        yield return record;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the diagram object identified by the given diagram-reference-number
        /// </summary>
        /// <param name="database">Link to local database</param>
        /// <param name="id">Diagram-reference-number</param>
        /// <param name="referenceOnly">If false, the content of the diagram will also be returned</param>
        /// <returns>Diagram object containing the identified diagram</returns>
        public Phabricator.Data.Diagram GetByID(Database database, int id, bool referenceOnly)
        {
            Phabricator.Data.Diagram record = null;

            // retrieve all properties of diagram except for its content
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, id, fileName, size, dateModified
                       FROM diagramInfo
                       WHERE id = @id;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "id", id.ToString(), Database.EncryptionMode.None);
                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        record = new Phabricator.Data.Diagram();
                        record.Token = (string)reader["token"];
                        record.ID = (Int32)reader["id"];
                        record.DateModified = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateModified"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);
                        record.FileName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["fileName"]);  // set Name before Data
                        record.Size = Int32.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["size"]));
                    }
                }
            }

            if (referenceOnly == false && record != null)
            {
                // retrieve content of diagram
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           SELECT data
                           FROM diagramInfo
                           WHERE id = @id;
                       ", database.Connection))
                {
                    database.AddParameter(dbCommand, "id", id.ToString(), Database.EncryptionMode.None);
                    using (var reader = dbCommand.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            record.DataStream = Encryption.Decrypt(database.EncryptionKey, reader.GetStream(0));
                        }
                        else
                        {
                            // should not happen
                            record = null;
                        }
                    }
                }
            }

            return record;
        }
    }
}
