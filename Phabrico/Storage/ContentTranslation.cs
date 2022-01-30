using Phabrico.Miscellaneous;
using Phabrico.Phabricator.Data;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using static Phabrico.Storage.Database;

namespace Phabrico.Storage
{
    public class Content
    {
        public class Translation
        {
            public string Token { get; set; }
            public Language Language { get; set; }
            public string TranslatedTitle { get; set; }
            public string TranslatedRemarkup { get; set; }
            public bool IsReviewed { get; set; }
            public DateTimeOffset DateModified { get; set; }
        }

        private readonly Database database;


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="database"></param>
        public Content(Database database)
        {
            this.database = database;
        }

        /// <summary>
        /// Translates a given PhabricatorObject (by token) into a given language.
        /// The translation is stored in the contentTranslation table
        /// </summary>
        /// <param name="token">Token to be translated (e.g. Phriction document)</param>
        /// <param name="language">Language to translated to</param>
        /// <param name="title">Translated title</param>
        /// <param name="translation">Translated content</param>
        public void AddTranslation(string token, Language language, string title, string translation)
        {
            if (language == Language.NotApplicable) return;

            using (SQLiteCommand dbCommandUpdate = new SQLiteCommand(@"
                        INSERT OR REPLACE INTO Translation.contentTranslation(token, language, title, translation, reviewed, dateModified)
                        VALUES (@token, @language, @title, @translation, 0, @dateModified)
                   ", database.Connection))
            {
                database.AddParameter(dbCommandUpdate, "token", token, EncryptionMode.None);
                database.AddParameter(dbCommandUpdate, "language", language.ToString(), EncryptionMode.None);
                database.AddParameter(dbCommandUpdate, "title", title, EncryptionMode.Default);
                database.AddParameter(dbCommandUpdate, "translation", translation, EncryptionMode.Default);
                database.AddParameter(dbCommandUpdate, "dateModified", DateTimeOffset.UtcNow, EncryptionMode.None);

                dbCommandUpdate.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Translates a given PhabricatorObject (by token) into a given language.
        /// The translation is stored in the contentTranslation table
        /// </summary>
        /// <param name="language">Language to translated to</param>
        /// <param name="jsonSerializedObject">Translated content</param>
        /// <returns>The new generated reference id</returns>
        public int AddTranslation(Language language, string jsonSerializedObject, out string newToken)
        {
            if (language == Language.NotApplicable) throw new ArgumentException("Language cannot be NotApplicable");

            int tokenReferenceID;
            string token;
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                    SELECT IFNULL(MAX(token), 'PHID-OBJECT-0') token
                    FROM Translation.contentTranslation
                    WHERE token LIKE 'PHID-OBJECT-%';
               ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        token = (string)reader["token"];
                        tokenReferenceID = Int32.Parse(token.Substring("PHID-OBJECT-".Length)) + 1;
                        token = string.Format("PHID-OBJECT-{0}", tokenReferenceID.ToString().PadLeft(18, '0'));
                    }
                    else
                    {
                        throw new InvalidOperationException("Translation.AddTranslation: unable to retrieve last object token");
                    }
                }
            }

            using (SQLiteCommand dbCommandUpdate = new SQLiteCommand(@"
                        INSERT OR REPLACE INTO Translation.contentTranslation(token, language, title, translation, reviewed, dateModified)
                        VALUES (@token, @language, @title, @translation, 0, @dateModified)
                   ", database.Connection))
            {
                database.AddParameter(dbCommandUpdate, "token", token, EncryptionMode.None);
                database.AddParameter(dbCommandUpdate, "language", language.ToString(), EncryptionMode.None);
                database.AddParameter(dbCommandUpdate, "title", "", EncryptionMode.Default);
                database.AddParameter(dbCommandUpdate, "translation", jsonSerializedObject, EncryptionMode.Default);
                database.AddParameter(dbCommandUpdate, "dateModified", DateTimeOffset.UtcNow, EncryptionMode.None);

                dbCommandUpdate.ExecuteNonQuery();
            }

            newToken = token;
            return tokenReferenceID;
        }

        /// <summary>
        /// Translation for a given token has been reviewed and approved
        /// </summary>
        /// <param name="token"></param>
        /// <param name="language"></param>
        public void ApproveTranslation(string token, Language language)
        {
            if (language == Language.NotApplicable) return;

            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                        UPDATE Translation.contentTranslation
                           SET reviewed = 1,
                               dateModified = @dateModified
                        WHERE token = @token
                          AND language = @language
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "token", token, EncryptionMode.None);
                database.AddParameter(dbCommand, "language", language, EncryptionMode.None);
                database.AddParameter(dbCommand, "dateModified", DateTimeOffset.UtcNow, EncryptionMode.None);

                dbCommand.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Copies the content translation records from one Phabrico database to another Phabrico database
        /// </summary>
        /// <param name="sourcePhabricoDatabasePath">File path to the source Phabrico database</param>
        /// <param name="sourceUsername">Username to use for authenticating the source Phabrico database</param>
        /// <param name="sourcePassword">Password to use for authenticating the source Phabrico database</param>
        /// <param name="destinationPhabricoDatabasePath">File path to the destination Phabrico database</param>
        /// <param name="destinationUsername">Username to use for authenticating the destination Phabrico database</param>
        /// <param name="destinationPassword">Username to use for authenticating the destination Phabrico database</param>
        /// <param name="filter">LINQ method for filtering the records to be copied</param>
        public static void Copy(string sourcePhabricoDatabasePath, string sourceUsername, string sourcePassword, string destinationPhabricoDatabasePath, string destinationUsername, string destinationPassword, Func<Translation,bool> filter = null)
        {
            string sourceTokenHash = Encryption.GenerateTokenKey(sourceUsername, sourcePassword);  // tokenHash is stored in the database
            string sourcePublicEncryptionKey = Encryption.GenerateEncryptionKey(sourceUsername, sourcePassword);  // encryptionKey is not stored in database (except when security is disabled)
            string sourcePrivateEncryptionKey = Encryption.GeneratePrivateEncryptionKey(sourceUsername, sourcePassword);  // privateEncryptionKey is not stored in database
            string destinationTokenHash = Encryption.GenerateTokenKey(destinationUsername, destinationPassword);  // tokenHash is stored in the database
            string destinationPublicEncryptionKey = Encryption.GenerateEncryptionKey(destinationUsername, destinationPassword);  // encryptionKey is not stored in database (except when security is disabled)
            string destinationPrivateEncryptionKey = Encryption.GeneratePrivateEncryptionKey(destinationUsername, destinationPassword);  // privateEncryptionKey is not stored in database

            string originalDataSource = Storage.Database.DataSource;

            try
            {
                List<Translation> translations = new List<Translation>();

                Storage.Database.DataSource = sourcePhabricoDatabasePath;
                using (Storage.Database database = new Storage.Database(null))
                {
                    bool noUserConfigured;
                    UInt64[] publicXorCipher = database.ValidateLogIn(sourceTokenHash, out noUserConfigured);
                    if (publicXorCipher != null)
                    {
                        database.EncryptionKey = Encryption.XorString(sourcePublicEncryptionKey, publicXorCipher);

                        using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                                SELECT *
                                FROM Translation.contentTranslation;
                           ", database.Connection))
                        {
                            using (var reader = dbCommand.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string token = (string)reader["token"];

                                    Translation record = new Translation();
                                    record.Token = token;
                                    record.TranslatedTitle = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["title"]);
                                    record.TranslatedRemarkup = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["translation"]);
                                    record.IsReviewed = false;
                                    record.Language = (string)reader["language"];
                                    record.DateModified = DateTimeOffset.FromUnixTimeSeconds(long.Parse(reader["dateModified"].ToString()));

                                    translations.Add(record);
                                }
                            }
                        }
                    }
                }

                if (filter != null)
                {
                    translations = translations.Where(filter).ToList();
                }

                Storage.Database.DataSource = destinationPhabricoDatabasePath;
                using (Storage.Database database = new Storage.Database(null))
                {
                    bool noUserConfigured;
                    UInt64[] publicXorCipher = database.ValidateLogIn(destinationTokenHash, out noUserConfigured);
                    if (publicXorCipher != null)
                    {
                        database.EncryptionKey = Encryption.XorString(destinationPublicEncryptionKey, publicXorCipher);

                        Content content = new Content(database);
                        foreach (Translation translation in translations)
                        {
                            content.AddTranslation(translation.Token, translation.Language, translation.TranslatedTitle, translation.TranslatedRemarkup);
                        }
                    }
                }
            }
            finally
            {
                Storage.Database.DataSource = originalDataSource;
            }
        }

        /// <summary>
        /// A previously approved translation for a given token has been disapproved.
        /// This can happen in case the original document has been changed.
        /// The translation should occur again on the new version of this original document.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="language"></param>
        public void DisapproveTranslationForAllLanguages(string token)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                        UPDATE Translation.contentTranslation
                           SET reviewed = 0
                        WHERE token = @token
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "token", token, EncryptionMode.None);

                dbCommand.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Deletes a translation for a given PhabricatorObject (by token)
        /// </summary>
        /// <param name="token"></param>
        /// <param name="language"></param>
        public void DeleteTranslation(string token, Language language)
        {
            if (language == Language.NotApplicable) return;

            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                        DELETE FROM Translation.contentTranslation
                        WHERE token = @token
                          AND language = @language
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "token", token, EncryptionMode.None);
                database.AddParameter(dbCommand, "language", language, EncryptionMode.None);

                dbCommand.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Deletes all translations of unreferenced objects
        /// An unreferenced object is for example an old diagram file
        /// </summary>
        public int DeleteUnreferencedTranslatedObjects()
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                        DELETE FROM Translation.contentTranslation
                        WHERE token NOT IN (
                                  SELECT token
                                  FROM objectrelationinfo
                                UNION
                                  SELECT linkedToken
                                  FROM objectrelationinfo
                                UNION
                                  SELECT token
                                  FROM phrictionInfo
                                UNION
                                  SELECT token
                                  FROM stageInfo
                              )
                          AND token LIKE 'PHID-NEWTOKEN%'
                   ", database.Connection))
            {
                return dbCommand.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Returns all available translations
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        public IEnumerable<Translation> GetAllTranslations()
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                        SELECT * FROM Translation.contentTranslation
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Translation record = new Translation();
                        record.Token = (string)reader["token"];
                        record.TranslatedTitle = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["title"]);
                        record.TranslatedRemarkup = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["translation"]);
                        record.IsReviewed = (int)reader["reviewed"] == 0 ? false : true;
                        record.Language = (Language)(string)reader["language"];
                        record.DateModified = DateTimeOffset.FromUnixTimeSeconds(long.Parse(reader["dateModified"].ToString()));

                        yield return record;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the languages for which a translated version of the phabricatorObject is available
        /// </summary>
        /// <param name="database"></param>
        /// <param name="phabricatorObject"></param>
        /// <returns></returns>
        public static Language[] GetAvailableLanguages(Database database, Phabricator.Data.PhabricatorObject phabricatorObject)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                        SELECT language FROM Translation.contentTranslation
                        WHERE token = @token
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "token", phabricatorObject.Token, EncryptionMode.None);

                List<Language> translationLanguages = new List<Language>();
                translationLanguages.Add(Language.NotApplicable);

                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        translationLanguages.Add((Language)(string)reader["language"]);
                    }
                }

                return translationLanguages.ToArray();
            }
        }

        /// <summary>
        /// Returns the translation for a given token and language.
        /// If no translation was found, NULL is returned
        /// </summary>
        /// <param name="token">Token to be translated</param>
        /// <param name="language">Language for translation</param>
        /// <returns>Translated token or NULL</returns>
        public Translation GetTranslation(string token, Language language)
        {
            if (language == Language.NotApplicable) return null;

            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                        SELECT * FROM Translation.contentTranslation
                        WHERE token = @token
                          AND language = @language
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "token", token, EncryptionMode.None);
                database.AddParameter(dbCommand, "language", language, EncryptionMode.None);

                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Translation record = new Translation();
                        record.Token = (string)reader["token"];
                        record.TranslatedTitle = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["title"]);
                        record.TranslatedRemarkup = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["translation"]);
                        record.IsReviewed = (int)reader["reviewed"] == 0 ? false : true;
                        record.Language = (Language)(string)reader["language"];
                        record.DateModified = DateTimeOffset.FromUnixTimeSeconds(long.Parse(reader["dateModified"].ToString()));

                        return record;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Returns a bunch of translations for a given language which need a review
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        public IEnumerable<Translation> GetUnreviewedTranslations(Language language)
        {
            if (language == Language.NotApplicable) yield break;

            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                        SELECT * FROM Translation.contentTranslation
                        WHERE language = @language
                          AND reviewed = 0
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "language", language, EncryptionMode.None);

                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Translation record = new Translation();
                        record.Token = (string)reader["token"];
                        record.TranslatedTitle = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["title"]);
                        record.TranslatedRemarkup = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["translation"]);
                        record.IsReviewed = false;
                        record.Language = language;
                        record.DateModified = DateTimeOffset.FromUnixTimeSeconds(long.Parse(reader["dateModified"].ToString()));

                        yield return record;
                    }
                }
            }
        }
        
        /// <summary>
        /// This method will set reviewed translations back to 'unreviewed' in case the master content has been updated.
        /// This method is fired after authentication and after synchronization
        /// </summary>
        /// <param name="database"></param>
        public static void SynchronizeReviewStatesWithMasterObjects(Database database)
        {
            // load all reviewed translations into a list
            List<Translation> contentTranslationRecords = new List<Translation>();
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                        SELECT * FROM Translation.contentTranslation
                        WHERE reviewed = 1
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Translation record = new Translation();
                        record.Token = (string)reader["token"];
                        record.TranslatedTitle = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["title"]);
                        record.TranslatedRemarkup = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["translation"]);
                        record.IsReviewed = true;
                        record.Language = (string)reader["language"];
                        record.DateModified = DateTimeOffset.FromUnixTimeSeconds(long.Parse(reader["dateModified"].ToString()));

                        contentTranslationRecords.Add(record);
                    }
                }
            }

            // compare collected translations with phriction documents
            foreach (string token in contentTranslationRecords.Select(record => record.Token).Distinct().ToArray())
            {
                Storage.Phriction phrictionStorage = new Storage.Phriction();
                Phabricator.Data.Phriction masterDocument = phrictionStorage.Get(database, token, Language.NotApplicable, false);
                if (masterDocument == null)
                {
                    contentTranslationRecords.RemoveAll(translation => translation.Token.Equals(token));
                }
                else
                {
                    contentTranslationRecords.RemoveAll(translation => translation.DateModified > masterDocument.DateModified);
                }
            }

            // mark left-overs as 'unreviewed'
            foreach (Translation translation in contentTranslationRecords.ToArray())
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                        UPDATE Translation.contentTranslation
                           SET reviewed = 0
                        WHERE token = @token
                          AND language = @language
                   ", database.Connection))
                {
                    database.AddParameter(dbCommand, "token", translation.Token, EncryptionMode.None);
                    database.AddParameter(dbCommand, "language", translation.Language, EncryptionMode.None);

                    dbCommand.ExecuteNonQuery();
                }
            }
        }
    }
}