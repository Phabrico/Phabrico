using System.Collections.Generic;
using System.Linq;
using System.Data.SQLite;
using System;

using Newtonsoft.Json;

using Phabrico.Http;
using Phabrico.Miscellaneous;

namespace Phabrico.Storage
{
    /// <summary>
    /// Database mapper for AccountInfo table
    /// </summary>
    public class Account : PhabricatorObject<Phabricator.Data.Account>
    {
        /// <summary>
        /// Creates a new record in the AccountInfo table
        /// </summary>
        /// <param name="database"></param>
        /// <param name="account"></param>
        public override void Add(Database database, Phabricator.Data.Account account)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       INSERT INTO accountInfo(token, userName, publicXorCipher, privateXorCipher, url, api, theme, parameters) 
                       VALUES (@tokenHash, @userName, @publicXorCipher, @privateXorCipher, @phabricatorUrl, @conduitAPiToken, @theme, @parameters);
                   ", database.Connection))
            {
                byte[] publicXorCipherBytes = new byte[account.PublicXorCipher.Length * 8];
                Buffer.BlockCopy(account.PublicXorCipher, 0, publicXorCipherBytes, 0, publicXorCipherBytes.Length);

                byte[] privateXorCipherBytes = new byte[account.PrivateXorCipher.Length * 8];
                Buffer.BlockCopy(account.PrivateXorCipher, 0, privateXorCipherBytes, 0, privateXorCipherBytes.Length);

                database.AddParameter(dbCommand, "tokenHash", account.Token, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "userName", account.UserName);
                database.AddParameter(dbCommand, "publicXorCipher", publicXorCipherBytes, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "privateXorCipher", privateXorCipherBytes, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "phabricatorUrl", account.PhabricatorUrl);
                database.AddParameter(dbCommand, "conduitAPiToken", account.ConduitAPIToken, Database.EncryptionMode.Private);
                database.AddParameter(dbCommand, "theme", account.Theme, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "parameters", JsonConvert.SerializeObject(account.Parameters));
                dbCommand.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Returns a bunch of AccountInfo records
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public override IEnumerable<Phabricator.Data.Account> Get(Database database)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, username, publicXorCipher, url, api, parameters, theme
                       FROM accountInfo;
                   ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Phabricator.Data.Account record = new Phabricator.Data.Account();

                        byte[] publicXorCipherBytes = (byte[])reader["publicXorCipher"];
                        record.PublicXorCipher = new UInt64[publicXorCipherBytes.Length / 8];
                        Buffer.BlockCopy(publicXorCipherBytes, 0, record.PublicXorCipher, 0, publicXorCipherBytes.Length);

                        record.Token = (string)reader["token"];
                        record.UserName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["username"]);
                        record.PhabricatorUrl = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["url"]);

                        if (database.PrivateEncryptionKey != null)
                        {
                            record.ConduitAPIToken = Encryption.Decrypt(database.PrivateEncryptionKey, (byte[])reader["api"]);
                        }

                        record.Theme = (string)reader["theme"];
                        string serializedParameters = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["parameters"]);
                        if (serializedParameters != null)
                        {
                            record.Parameters = JsonConvert.DeserializeObject<Phabricator.Data.Account.Configuration>(serializedParameters);
                        }

                        yield return record;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a given accountInfo record
        /// </summary>
        /// <param name="database">Phabrico database</param>
        /// <param name="key">Token to be searched for</param>
        /// <param name="ignoreStageData">Not used</param>
        /// <returns></returns>
        public override Phabricator.Data.Account Get(Database database, string key, bool ignoreStageData = false)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT token, userName, publicXorCipher, url, api, parameters, theme
                       FROM accountInfo
                       WHERE token = @key;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "key", key, Database.EncryptionMode.None);
                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Phabricator.Data.Account record = new Phabricator.Data.Account();

                        byte[] publicXorCipherBytes = (byte[])reader["publicXorCipher"];
                        record.PublicXorCipher = new UInt64[publicXorCipherBytes.Length / 8];
                        Buffer.BlockCopy(publicXorCipherBytes, 0, record.PublicXorCipher, 0, publicXorCipherBytes.Length);

                        record.Token = (string)reader["token"];
                        record.UserName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["username"]);
                        record.PhabricatorUrl = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["url"]);
                        if (database.PrivateEncryptionKey != null)
                        {
                            record.ConduitAPIToken = Encryption.Decrypt(database.PrivateEncryptionKey, (byte[])reader["api"]);
                        }
                        record.Theme = (string)reader["theme"];
                        string serializedParameters = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["parameters"]);
                        if (serializedParameters != null)
                        {
                            record.Parameters = JsonConvert.DeserializeObject<Phabricator.Data.Account.Configuration>(serializedParameters);
                        }

                        return record;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Returns a given accountInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public Phabricator.Data.Account Get(Database database, SessionManager.Token token)
        {
            if (token != null)
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           SELECT token, userName, publicXorCipher, privateXorCipher, url, api, parameters, theme
                           FROM accountInfo
                           WHERE token = @token;
                       ", database.Connection))
                {
                    database.AddParameter(dbCommand, "token", token.Key, Database.EncryptionMode.None);
                    using (var reader = dbCommand.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            Phabricator.Data.Account record = new Phabricator.Data.Account();

                            byte[] publicXorCipherBytes = (byte[])reader["publicXorCipher"];
                            record.PublicXorCipher = new UInt64[publicXorCipherBytes.Length / 8];
                            Buffer.BlockCopy(publicXorCipherBytes, 0, record.PublicXorCipher, 0, publicXorCipherBytes.Length);

                            byte[] privateXorCipherBytes = (byte[])reader["privateXorCipher"];
                            record.PrivateXorCipher = new UInt64[privateXorCipherBytes.Length / 8];
                            Buffer.BlockCopy(privateXorCipherBytes, 0, record.PrivateXorCipher, 0, privateXorCipherBytes.Length);

                            record.Token = (string)reader["token"];
                            record.Theme = (string)reader["theme"];
                            record.UserName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["username"]);
                            record.PhabricatorUrl = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["url"]);
                            if (database.PrivateEncryptionKey != null)
                            {
                                record.ConduitAPIToken = Encryption.Decrypt(database.PrivateEncryptionKey, (byte[])reader["api"]);
                            }
                            string serializedParameters = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["parameters"]);
                            if (serializedParameters != null)
                            {
                                record.Parameters = JsonConvert.DeserializeObject<Phabricator.Data.Account.Configuration>(serializedParameters);
                            }

                            return record;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the XOR value being used for masking the private database encryption key
        /// </summary>
        /// <param name="database"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public UInt64[] GetPrivateXorCipher(Database database, SessionManager.Token token)
        {
            if (token != null)
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           SELECT privateXorCipher
                           FROM accountInfo
                           WHERE token = @token;
                       ", database.Connection))
                {
                    database.AddParameter(dbCommand, "token", token.Key, Database.EncryptionMode.None);
                    using (var reader = dbCommand.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            byte[] privateXorCipherBytes = (byte[])reader["privateXorCipher"];
                            UInt64[] privateXorCipher = new UInt64[privateXorCipherBytes.Length / 8];
                            Buffer.BlockCopy(privateXorCipherBytes, 0, privateXorCipher, 0, privateXorCipherBytes.Length);

                            return privateXorCipher;
                        }
                    }
                }
            }

            return new UInt64[4];
        }

        /// <summary>
        /// Returns the XOR value being used for masking the public database encryption key
        /// </summary>
        /// <param name="database"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public UInt64[] GetPublicXorCipher(Database database, SessionManager.Token token)
        {
            if (token != null)
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           SELECT publicXorCipher
                           FROM accountInfo
                           WHERE token = @token;
                       ", database.Connection))
                {
                    database.AddParameter(dbCommand, "token", token.Key, Database.EncryptionMode.None);
                    using (var reader = dbCommand.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            byte[] publicXorCipherBytes = (byte[])reader["publicXorCipher"];
                            UInt64[] publicXorCipher = new UInt64[publicXorCipherBytes.Length / 8];
                            Buffer.BlockCopy(publicXorCipherBytes, 0, publicXorCipher, 0, publicXorCipherBytes.Length);

                            return publicXorCipher;
                        }
                    }
                }
            }

            return new UInt64[4];
        }

        /// <summary>
        /// Updates an accountInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="existingAccount"></param>
        public void Set(Database database, Phabricator.Data.Account existingAccount)
        {
            if (database.PrivateEncryptionKey == null)
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       UPDATE accountInfo
                         SET url = @url, 
                             parameters = @parameters,
                             theme = @theme
                       WHERE token = @token;
                   ", database.Connection))
                {
                    database.AddParameter(dbCommand, "url", existingAccount.PhabricatorUrl);
                    database.AddParameter(dbCommand, "parameters", JsonConvert.SerializeObject(existingAccount.Parameters));
                    database.AddParameter(dbCommand, "token", existingAccount.Token, Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "theme", existingAccount.Theme, Database.EncryptionMode.None);
                    if (dbCommand.ExecuteNonQuery() == 0)
                    {
                        Add(database, existingAccount);
                    }
                }
            }
            else
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       UPDATE accountInfo
                         SET url = @url, 
                             api = @api,
                             parameters = @parameters,
                             theme = @theme
                       WHERE token = @token;
                   ", database.Connection))
                {
                    database.AddParameter(dbCommand, "url", existingAccount.PhabricatorUrl);
                    database.AddParameter(dbCommand, "api", existingAccount.ConduitAPIToken, Database.EncryptionMode.Private);
                    database.AddParameter(dbCommand, "parameters", JsonConvert.SerializeObject(existingAccount.Parameters));
                    database.AddParameter(dbCommand, "token", existingAccount.Token, Database.EncryptionMode.None);
                    database.AddParameter(dbCommand, "theme", existingAccount.Theme, Database.EncryptionMode.None);
                    if (dbCommand.ExecuteNonQuery() == 0)
                    {
                        Add(database, existingAccount);
                    }
                }
            }
        }

        /// <summary>
        /// Modifies the token and xorCipher of a given accountInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="oldToken"></param>
        /// <param name="newToken"></param>
        /// <param name="newPublicXorValue"></param>
        /// <param name="newPrivateXorValue"></param>
        /// <returns></returns>
        public bool UpdateToken(Database database, string oldToken, string newToken, UInt64[] newPublicXorValue, UInt64[] newPrivateXorValue)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       UPDATE accountInfo
                         SET token = @newToken,
                             publicXorCipher = @newPublicXorValue,
                             privateXorCipher = @newPrivateXorValue
                       WHERE token = @oldToken;
                   ", database.Connection))
            {

                byte[] publicXorCipherBytes = new byte[newPublicXorValue.Length * 8];
                Buffer.BlockCopy(newPublicXorValue, 0, publicXorCipherBytes, 0, publicXorCipherBytes.Length);

                byte[] privateXorCipherBytes = new byte[newPrivateXorValue.Length * 8];
                Buffer.BlockCopy(newPrivateXorValue, 0, privateXorCipherBytes, 0, privateXorCipherBytes.Length);

                database.AddParameter(dbCommand, "newPublicXorValue", publicXorCipherBytes, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "newPrivateXorValue", privateXorCipherBytes, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "newToken", newToken, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "oldToken", oldToken, Database.EncryptionMode.None);
                
                return dbCommand.ExecuteNonQuery() > 0;
            }
        }

        /// <summary>
        /// Returns the first record of the accountInfo table
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public Phabricator.Data.Account WhoAmI(Database database)
        {
            return Get(database).FirstOrDefault();
        }
    }
}