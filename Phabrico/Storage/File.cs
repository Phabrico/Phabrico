using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Parsers.Base64;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Phabrico.Storage
{
    /// <summary>
    /// Database mapper for FileInfo and FileChunkInfo tables
    /// </summary>
    public class File : PhabricatorObject<Phabricator.Data.File>
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
        /// <param name="file"></param>
        public override void Add(Database database, Phabricator.Data.File file)
        {
            using (SQLiteTransaction transaction = database.Connection.BeginTransaction())
            {
                if (file.ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using (System.Drawing.Image image = System.Drawing.Image.FromStream(file.DataStream))
                        {
                            file.ImagePropertyPixelWidth = image.Width;
                            file.ImagePropertyPixelHeight = image.Height;
                        }
                    }
                    catch
                    {
                        file.ImagePropertyPixelWidth = 0;
                        file.ImagePropertyPixelHeight = 0;
                    }
                }

                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           INSERT OR REPLACE INTO fileInfo(token, id, fileName, macroName, data, size, dateModified, contentType, properties)
                           VALUES (@token, @id, @fileName, @macroName, @data, @size, @dateModified, @contentType, @properties);
                       ", database.Connection, transaction))
                {
                    database.AddParameter(dbCommand, "token", file.Token, Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "id", file.ID.ToString(), Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "fileName", file.FileName);
                    database.AddParameter(dbCommand, "macroName", file.MacroName);
                    database.AddParameter(dbCommand, "data", file.DataStream);
                    database.AddParameter(dbCommand, "size", file.Size.ToString());
                    database.AddParameter(dbCommand, "dateModified", file.DateModified);
                    database.AddParameter(dbCommand, "contentType", file.ContentType);
                    database.AddParameter(dbCommand, "properties", file.Properties);
                    dbCommand.ExecuteNonQuery();

                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// Adds a new record to the fileChunkInfo table
        /// </summary>
        /// <param name="database"></param>
        /// <param name="browser"></param>
        /// <param name="fileName"></param>
        /// <param name="fileChunk"></param>
        /// <param name="language"></param>
        public void AddChunk(Database database, Browser browser, string fileName, Phabricator.Data.File.Chunk fileChunk, Language language)
        {
            SQLiteCommand dbCommand;
            using (SQLiteTransaction transaction = database.Connection.BeginTransaction())
            {
                using (dbCommand = new SQLiteCommand(@"
                           INSERT OR REPLACE INTO fileChunkInfo(id, chunk, nbrChunks, dateModified, data) 
                           VALUES (@id, @chunk, @nbrChunks, @dateModified, @data);
                       ", database.Connection, transaction))
                {
                    database.AddParameter(dbCommand, "id", fileChunk.FileID, Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "chunk", fileChunk.ChunkID, Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "nbrChunks", fileChunk.NbrChunks, Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "dateModified", fileChunk.DateModified, Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "data", System.Convert.ToBase64String(fileChunk.Data), Database.EncryptionMode.None);
                    dbCommand.ExecuteNonQuery();

                    transaction.Commit();
                }
            }

            // check if this was the last chunk
            bool lastChunkWasWritten = false;
            using (dbCommand = new SQLiteCommand(@"
                       SELECT COUNT(id) AS result
                       FROM fileChunkInfo
                       WHERE nbrChunks = (
                           SELECT COUNT(id)
                           FROM fileChunkInfo
                           WHERE id = @id
                             AND chunk >= 0
                       )
                       AND nbrChunks > 0
                       AND id = @id;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "id", fileChunk.FileID, Database.EncryptionMode.None);

                if (fileChunk.NbrChunks > 0)
                {
                    using (var reader = dbCommand.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            if (Int32.Parse(reader["result"].ToString()) == fileChunk.NbrChunks)
                            {
                                lastChunkWasWritten = true;
                            }
                        }
                    }
                }
            }

            if (lastChunkWasWritten)
            {
                Stage stageStorage = new Stage();
                Phabricator.Data.File newFileObject = new Phabricator.Data.File();

                // read all chunks 1 by 1 and add them to newFileObject
                using (dbCommand = new SQLiteCommand(@"
                           SELECT data FROM fileChunkInfo
                           WHERE id = @id
                             AND chunk >= 0
                           ORDER BY chunk;
                       ", database.Connection))
                {
                    database.AddParameter(dbCommand, "id", fileChunk.FileID, Database.EncryptionMode.None);
                    using (var reader = dbCommand.ExecuteReader())
                    {
                        List<byte> base64Data = new List<byte>();

                        if (reader.Read() == false)
                        {
                            // file was already written in database by a previous reentrant method call
                            return;
                        }
                        else
                        {
                            do
                            {
                                base64Data.AddRange((byte[])reader["data"]);
                            }
                            while (reader.Read());
                        }

                        newFileObject.FileName = fileName;
                        newFileObject.DataStream = new Base64EIDOStream(base64Data.ToArray());
                        newFileObject.Size = (int)newFileObject.DataStream.Length;
                        newFileObject.ID = fileChunk.FileID;
                        newFileObject.DateModified = DateTimeOffset.UtcNow;
                        newFileObject.Language = language;
                    }
                }

                // if image, set width and height
                if (newFileObject.ContentType.StartsWith("image/"))
                {
                    using (MemoryStream memoryStream = new MemoryStream(newFileObject.Data))
                    {
                        using (Bitmap bitmap = new Bitmap(memoryStream))
                        {
                            newFileObject.ImagePropertyPixelHeight = bitmap.Height;
                            newFileObject.ImagePropertyPixelWidth = bitmap.Width;
                        }
                    }
                }

                // write new File object
                stageStorage.Create(database, browser, newFileObject);

                // delete chunk data
                using (dbCommand = new SQLiteCommand(@"
                           DELETE FROM fileChunkInfo
                           WHERE id = @id;
                       ", database.Connection))
                {
                    database.AddParameter(dbCommand, "id", fileChunk.FileID, Database.EncryptionMode.None);
                    dbCommand.ExecuteNonQuery();
                }
            }
        }
        
        /// <summary>
        /// Returns the number of FileInfo records
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public static long Count(Database database)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT COUNT(*) AS result
                       FROM fileInfo
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
        /// Returns a specific FileInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="key"></param>
        /// <param name="ignoreStageData"></param>
        /// <returns></returns>
        public override Phabricator.Data.File Get(Database database, string key, Language language, bool ignoreStageData = false)
        {
            // search by token
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, id, fileName, macroName, data, size, dateModified, properties
                       FROM fileInfo
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

                        Phabricator.Data.File record = new Phabricator.Data.File();
                        record.Token = (string)reader["token"];
                        record.ID = (Int32)reader["id"];
                        record.FileName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["fileName"]);
                        record.DataStream = base64EIDOStream;
                        record.DateModified = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateModified"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);
                        record.Properties = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["properties"]);
                        record.MacroName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["macroName"]);
                        record.Size = Int32.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["size"]));

                        return record;
                    }
                }
            }

            // token not found: search further by macro name
            if (key.StartsWith(":"))
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, id, fileName, macroName, data, size, dateModified, properties
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

                            Phabricator.Data.File record = new Phabricator.Data.File();
                            record.Token = (string)reader["token"];
                            record.ID = (Int32)reader["id"];
                            record.FileName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["fileName"]);
                            record.DataStream = base64EIDOStream;
                            record.DateModified = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateModified"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);
                            record.Properties = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["properties"]);
                            record.MacroName = macroName;
                            record.Size = Int32.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["size"]));

                            return record;
                        }
                    }
                }
            }

            if (ignoreStageData == false && key.StartsWith("PHID-NEWTOKEN"))
            {
                int fileID = Int32.Parse(key.Substring("PHID-NEWTOKEN".Length));

                Storage.Stage stageStorage = new Storage.Stage();
                Phabricator.Data.File record = stageStorage.Get<Phabricator.Data.File>(database, language, Phabricator.Data.File.Prefix, fileID, true);
                return record;
            }

            return null;
        }

        /// <summary>
        /// Returns a bunch of FileInfo records
        /// </summary>
        /// <param name="database"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public override IEnumerable<Phabricator.Data.File> Get(Database database, Language language)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, id, fileName, macroName, data, size, dateModified, properties
                       FROM fileInfo;
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string decryptedBase64Data = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["data"]);
                        byte[] decryptedData = System.Convert.FromBase64String(decryptedBase64Data);

                        Phabricator.Data.File record = new Phabricator.Data.File();
                        record.Token = (string)reader["token"];
                        record.ID = (Int32)reader["id"];
                        record.DataStream = new Base64EIDOStream();
                        record.DataStream.WriteDecodedData(decryptedData);
                        record.DateModified = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateModified"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);
                        record.FileName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["fileName"]);
                        record.MacroName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["macroName"]);
                        record.Properties = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["properties"]);
                        record.Size = Int32.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["size"]));

                        yield return record;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a bunch of FileInfo records
        /// </summary>
        /// <param name="database"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public IEnumerable<Phabricator.Data.File> GetMacroFiles(Database database, Language language)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, id, fileName, macroName, data, size, dateModified, properties
                       FROM fileInfo
                       WHERE macroName <> @macroName;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "macroName", "", Database.EncryptionMode.Default);

                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string decryptedBase64Data = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["data"]);
                        byte[] decryptedData = System.Convert.FromBase64String(decryptedBase64Data);

                        Phabricator.Data.File record = new Phabricator.Data.File();
                        record.Token = (string)reader["token"];
                        record.ID = (Int32)reader["id"];
                        record.DataStream = new Base64EIDOStream();
                        record.DataStream.WriteDecodedData(decryptedData);
                        record.DateModified = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateModified"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);
                        record.FileName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["fileName"]);
                        record.MacroName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["macroName"]);
                        record.Properties = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["properties"]);
                        record.Size = Int32.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["size"]));

                        yield return record;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the file object identified by the given file-reference-number
        /// </summary>
        /// <param name="database">Link to local database</param>
        /// <param name="id">File-reference-number</param>
        /// <param name="referenceOnly">If false, the content of the file will also be returned</param>
        /// <returns>File object containing the identified file</returns>
        public Phabricator.Data.File GetByID(Database database, int id, bool referenceOnly)
        {
            Phabricator.Data.File record = null;

            // retrieve all properties of file except for its content
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, id, fileName, macroName, size, dateModified, contentType, properties
                       FROM fileInfo
                       WHERE id = @id;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "id", id.ToString(), Database.EncryptionMode.None);
                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        record = new Phabricator.Data.File();
                        record.Token = (string)reader["token"];
                        record.ID = (Int32)reader["id"];
                        record.ContentType = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["contentType"]);
                        record.Properties = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["properties"]);
                        record.DateModified = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateModified"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);
                        record.FileName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["fileName"]);  // set Name before Data
                        record.MacroName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["macroName"]);
                        record.Size = Int32.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["size"]));
                    }
                }
            }

            if (referenceOnly == false && record != null)
            {
                // retrieve content of file
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           SELECT data
                           FROM fileInfo
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

        /// <summary>
        /// Returns an ID which can be used for a new File object
        /// </summary>
        /// <param name="database"></param>
        /// <param name="browser"></param>
        /// <returns></returns>
        public int GetNewID(Database database, Browser browser)
        {
            lock (ReentrancyLock)
            {
                Phabricator.Data.File.Chunk dummyFileChunk = new Phabricator.Data.File.Chunk();

                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                          SELECT MAX(latestNewToken) AS latestNewToken
                          FROM (
                              SELECT CAST(SUBSTR(token, LENGTH('PHID-NEWTOKEN-') + 1) AS INT) AS latestNewToken FROM stageinfo WHERE token LIKE 'PHID-NEWTOKEN-%'
                              UNION
                              SELECT -id AS latestNewToken FROM fileChunkInfo
                              UNION
                              SELECT 0 AS latestNewToken
                          ) drv;
                       ", database.Connection))
                {
                    using (var reader = dbCommand.ExecuteReader())
                    {
                        if (reader.Read())
                        {

                            // read latest token and mark the next one as 'reserved' by writing a dummy file chunk into the database
                            dummyFileChunk.FileID = -(Int32.Parse(reader["latestNewToken"].ToString()) + 1);
                            dummyFileChunk.ChunkID = -1;
                            dummyFileChunk.Data = new byte[0];
                            AddChunk(database, browser, "", dummyFileChunk, Language.NotApplicable);

                            // return new id
                            return dummyFileChunk.FileID;
                        }
                    }

                    // mark -1 as 'reserved' by writing a dummy file chunk into the database
                    dummyFileChunk.FileID = -1;
                    AddChunk(database, browser, "", dummyFileChunk, Language.NotApplicable);

                    return -1;
                }
            }
        }
        
        /// <summary>
        /// Returns a bunch of FileInfo records, including the number of times these files are referenced to
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public IEnumerable<Phabricator.Data.File> GetReferenceInfo(Database database)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT fileInfo.token, fileInfo.id, fileInfo.fileName, fileInfo.macroName, fileInfo.contentType, fileInfo.size, fileInfo.dateModified, fileInfo.properties,
                              COUNT(*) numberOfReferences
                       FROM fileInfo, objectRelationInfo
                       WHERE fileInfo.token = objectRelationInfo.linkedToken
                       GROUP BY fileInfo.token, fileInfo.id, fileInfo.fileName, fileInfo.macroName, fileInfo.contentType, fileInfo.size, fileInfo.dateModified
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Phabricator.Data.File record = new Phabricator.Data.File();
                        record.Token = (string)reader["token"];
                        record.ID = (Int32)reader["id"];
                        record.ContentType = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["contentType"]);
                        record.DateModified = DateTimeOffset.ParseExact(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["dateModified"]), "yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture);
                        record.FileName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["fileName"]);
                        record.MacroName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["macroName"]);
                        record.Size = Int32.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["size"]));
                        record.Properties = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["properties"]);
                        record.NumberOfReferences = (Int32)(Int64)reader["numberOfReferences"];

                        yield return record;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the maximum filesize of all FileInfo records
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public static long MaximumSize(Database database)
        {
            if (CachedMaximumSize < 0)
            {
                RecalculateMedianAndMaximumFileSizesInDatabase(database);
            }

            return CachedMaximumSize;
        }

        /// <summary>
        /// Returns the median filesize of all FileInfo records
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public static long MedianSize(Database database)
        {
            if (CachedMedianSize < 0)
            {
                RecalculateMedianAndMaximumFileSizesInDatabase(database);
            }

            return CachedMedianSize;
        }

        /// <summary>
        /// Retrieves all the file objects stored in the SQLite database and calculates the median and maximum size
        /// </summary>
        /// <param name="database"></param>
        public static void RecalculateMedianAndMaximumFileSizesInDatabase(Database database)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT size
                       FROM fileInfo
                   ", database.Connection))
            {
                List<Int32> fileObjectSizes = new List<int>();
                fileObjectSizes.Add(0);

                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        do
                        {
                            fileObjectSizes.Add(Int32.Parse(Encryption.Decrypt(database.EncryptionKey, (byte[])reader["size"])));
                        }
                        while (reader.Read());

                        fileObjectSizes = fileObjectSizes.OrderByDescending(size => size).ToList();

                        CachedMaximumSize = fileObjectSizes.FirstOrDefault();
                        CachedMedianSize = fileObjectSizes.ElementAt(fileObjectSizes.Count / 2);
                    }
                }
            }
        }
    }
}
