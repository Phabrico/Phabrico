﻿using Newtonsoft.Json;
using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Phabricator.Data;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;

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
                       INSERT OR REPLACE INTO accountInfo(token, userName, publicXorCipher, privateXorCipher, dpapiXorCipher1, dpapiXorCipher2, url, api, theme, parameters)
                       VALUES (@tokenHash, @userName, @publicXorCipher, @privateXorCipher, @dpapiXorCipher1, @dpapiXorCipher2, @phabricatorUrl, @conduitAPiToken, @theme, @parameters);
                   ", database.Connection))
            {
                byte[] publicXorCipherBytes = new byte[account.PublicXorCipher.Length * 8];
                Buffer.BlockCopy(account.PublicXorCipher, 0, publicXorCipherBytes, 0, publicXorCipherBytes.Length);

                byte[] privateXorCipherBytes;
                if (account.PrivateXorCipher == null)
                {
                    privateXorCipherBytes = null;
                }
                else
                {
                    privateXorCipherBytes = new byte[account.PrivateXorCipher.Length * 8];
                    Buffer.BlockCopy(account.PrivateXorCipher, 0, privateXorCipherBytes, 0, privateXorCipherBytes.Length);
                }

                byte[] dpapiXorCipher1Bytes;
                if (account.DpapiXorCipher1 == null)
                {
                    dpapiXorCipher1Bytes = null;
                }
                else
                {
                    dpapiXorCipher1Bytes = new byte[account.DpapiXorCipher1.Length * 8];
                    Buffer.BlockCopy(account.DpapiXorCipher1, 0, dpapiXorCipher1Bytes, 0, dpapiXorCipher1Bytes.Length);
                }

                byte[] dpapiXorCipher2Bytes;
                if (account.DpapiXorCipher2 == null)
                {
                    dpapiXorCipher2Bytes = null;
                }
                else
                {
                    dpapiXorCipher2Bytes = new byte[account.DpapiXorCipher2.Length * 8];
                    Buffer.BlockCopy(account.DpapiXorCipher2, 0, dpapiXorCipher2Bytes, 0, dpapiXorCipher2Bytes.Length);
                }

                database.AddParameter(dbCommand, "tokenHash", account.Token, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "userName", account.UserName);
                database.AddParameter(dbCommand, "publicXorCipher", publicXorCipherBytes, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "privateXorCipher", privateXorCipherBytes, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "dpapiXorCipher1", dpapiXorCipher1Bytes, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "dpapiXorCipher2", dpapiXorCipher2Bytes, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "phabricatorUrl", account.PhabricatorUrl);
                database.AddParameter(dbCommand, "conduitAPiToken", account.ConduitAPIToken, Database.EncryptionMode.Private);
                database.AddParameter(dbCommand, "theme", account.Theme, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "parameters", JsonConvert.SerializeObject(account.Parameters));
                lock (Database.dbLock)
                {
                    dbCommand.ExecuteNonQuery();
                }

                Database.IsModified = true;
            }
        }

        /// <summary>
        /// Returns a bunch of AccountInfo records
        /// </summary>
        /// <param name="database"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public override IEnumerable<Phabricator.Data.Account> Get(Database database, Language language)
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

                        record.Token = (string)reader["token"];
                        record.UserName = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["username"]);
                        record.PhabricatorUrl = Encryption.Decrypt(database.EncryptionKey, (byte[])reader["url"]);

                        byte[] publicXorCipherBytes = (byte[])reader["publicXorCipher"];
                        record.PublicXorCipher = new UInt64[publicXorCipherBytes.Length / 8];
                        Buffer.BlockCopy(publicXorCipherBytes, 0, record.PublicXorCipher, 0, publicXorCipherBytes.Length);

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
        public override Phabricator.Data.Account Get(Database database, string key, Language language, bool ignoreStageData = false)
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
            if (token != null && 
                token.AuthenticationFactor != AuthenticationFactor.Public && 
                token.AuthenticationFactor != AuthenticationFactor.Experience
               )
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           SELECT token, userName, publicXorCipher, privateXorCipher, dpapiXorCipher1, dpapiXorCipher2, url, api, parameters, theme
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

                            object dbPrivateXorCipherBytes = reader["privateXorCipher"];
                            if (dbPrivateXorCipherBytes is DBNull)
                            {
                                record.PrivateXorCipher = null;
                            }
                            else
                            {
                                byte[] privateXorCipherBytes = (byte[])dbPrivateXorCipherBytes;
                                record.PrivateXorCipher = new UInt64[privateXorCipherBytes.Length / 8];
                                Buffer.BlockCopy(privateXorCipherBytes, 0, record.PrivateXorCipher, 0, privateXorCipherBytes.Length);
                            }

                            object dbDpapiXorCipher1Bytes = reader["privateXorCipher"];
                            if (dbDpapiXorCipher1Bytes is DBNull)
                            {
                                record.PrivateXorCipher = null;
                            }
                            else
                            {
                                byte[] dpapiXorCipher1Bytes = (byte[])dbDpapiXorCipher1Bytes;
                                record.DpapiXorCipher1 = new UInt64[dpapiXorCipher1Bytes.Length / 8];
                                Buffer.BlockCopy(dpapiXorCipher1Bytes, 0, record.DpapiXorCipher1, 0, dpapiXorCipher1Bytes.Length);
                            }

                            object dbDpapiXorCipher2Bytes = reader["dpapiXorCipher2"];
                            if (dbDpapiXorCipher2Bytes is DBNull)
                            {
                                record.PrivateXorCipher = null;
                            }
                            else
                            {
                                byte[] dpapiXorCipher2Bytes = (byte[])dbDpapiXorCipher2Bytes;
                                record.DpapiXorCipher2 = new UInt64[dpapiXorCipher2Bytes.Length / 8];
                                Buffer.BlockCopy(dpapiXorCipher2Bytes, 0, record.DpapiXorCipher2, 0, dpapiXorCipher2Bytes.Length);
                            }

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
                if (token.AuthenticationFactor == AuthenticationFactor.Knowledge)
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
                                object dbPrivateXorCipher = reader["privateXorCipher"];
                                if (dbPrivateXorCipher is DBNull)
                                {
                                    return null;
                                }
                                else
                                {
                                    byte[] privateXorCipherBytes = (byte[])dbPrivateXorCipher;
                                    UInt64[] privateXorCipher = new UInt64[privateXorCipherBytes.Length / 8];
                                    Buffer.BlockCopy(privateXorCipherBytes, 0, privateXorCipher, 0, privateXorCipherBytes.Length);

                                    return privateXorCipher;
                                }
                            }
                        }
                    }
                }
                else
                if (token.AuthenticationFactor == AuthenticationFactor.Ownership)
                {
                    UInt64[] xorPublic, xorPrivate;
                    GetDpapiXorCiphers(database, out xorPublic, out xorPrivate);
                    return xorPrivate;
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
        /// Returns the XOR value being used for masking the DPAPI (Windows Authentication) database encryption key
        /// </summary>
        /// <param name="database"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public void GetDpapiXorCiphers(Database database, out UInt64[] xorPublic, out UInt64[] xorPrivate)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           SELECT dpapiXorCipher1, dpapiXorCipher2
                           FROM accountInfo;
                       ", database.Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        byte[] dpapiXorCipherBytes = (byte[])reader["dpapiXorCipher1"];
                        xorPublic = new UInt64[dpapiXorCipherBytes.Length / 8];
                        Buffer.BlockCopy(dpapiXorCipherBytes, 0, xorPublic, 0, dpapiXorCipherBytes.Length);

                        dpapiXorCipherBytes = (byte[])reader["dpapiXorCipher2"];
                        xorPrivate = new UInt64[dpapiXorCipherBytes.Length / 8];
                        Buffer.BlockCopy(dpapiXorCipherBytes, 0, xorPrivate, 0, dpapiXorCipherBytes.Length);
                    }
                    else
                    {
                        xorPublic = new UInt64[4];
                        xorPrivate = new UInt64[4];
                    }
                }
            }
        }

        /// <summary>
        /// Deletes a record from the AccountInfo table
        /// </summary>
        /// <param name="database"></param>
        /// <param name="account"></param>
        public void Remove(Database database, Phabricator.Data.Account account)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       DELETE FROM accountInfo
                       WHERE token = @tokenHash;
                   ", database.Connection))
            {
                database.AddParameter(dbCommand, "tokenHash", account.Token, Database.EncryptionMode.None);
                lock (Database.dbLock)
                {
                    if (dbCommand.ExecuteNonQuery() > 0)
                    {
                        Database.IsModified = true;
                    }
                }
            }
        }

        /// <summary>
        /// Updates an accountInfo record
        /// </summary>
        /// <param name="database"></param>
        /// <param name="existingAccount"></param>
        public void Set(Database database, Phabricator.Data.Account existingAccount)
        {
            lock (Database.dbLock)
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
                        else
                        {
                            Database.IsModified = true;
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
                        database.AddParameter(dbCommand, "theme", existingAccount.Theme ?? "light", Database.EncryptionMode.None);
                        if (dbCommand.ExecuteNonQuery() == 0)
                        {
                            Add(database, existingAccount);
                        }
                        else
                        {
                            Database.IsModified = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Modifies the token and xorCiphers of a given accountInfo record
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

                byte[] privateXorCipherBytes = null;
                if (newPrivateXorValue != null)
                {
                    privateXorCipherBytes = new byte[newPrivateXorValue.Length * 8];
                    Buffer.BlockCopy(newPrivateXorValue, 0, privateXorCipherBytes, 0, privateXorCipherBytes.Length);
                }

                database.AddParameter(dbCommand, "newPublicXorValue", publicXorCipherBytes, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "newPrivateXorValue", privateXorCipherBytes, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "newToken", newToken, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "oldToken", oldToken, Database.EncryptionMode.None);

                lock (Database.dbLock)
                {
                    if (dbCommand.ExecuteNonQuery() > 0)
                    {
                        Database.IsModified = true;
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Modifies the DPAPI xorCipher of all accountInfo records
        /// </summary>
        /// <param name="database"></param>
        /// <param name="newDpapiXorValue"></param>
        /// <returns></returns>
        public bool UpdateDpapiXorCipher(Database database, UInt64[] newPublicDpapiXorValue, UInt64[] newPrivateDpapiXorValue)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       UPDATE accountInfo
                         SET dpapiXorCipher1 = @newPublicDpapiXorValue,
                             dpapiXorCipher2 = @newPrivateDpapiXorValue;
                   ", database.Connection))
            {
                byte[] dpapiPublicXorCipherBytes = new byte[newPublicDpapiXorValue.Length * 8];
                Buffer.BlockCopy(newPublicDpapiXorValue, 0, dpapiPublicXorCipherBytes, 0, dpapiPublicXorCipherBytes.Length);

                byte[] dpapiPrivateXorCipherBytes = new byte[newPrivateDpapiXorValue.Length * 8];
                Buffer.BlockCopy(newPrivateDpapiXorValue, 0, dpapiPrivateXorCipherBytes, 0, dpapiPrivateXorCipherBytes.Length);

                database.AddParameter(dbCommand, "newPublicDpapiXorValue", dpapiPublicXorCipherBytes, Database.EncryptionMode.None);
                database.AddParameter(dbCommand, "newPrivateDpapiXorValue", dpapiPrivateXorCipherBytes, Database.EncryptionMode.None);

                lock (Database.dbLock)
                {
                    if (dbCommand.ExecuteNonQuery() > 0)
                    {
                        Database.IsModified = true;
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Returns the first record of the accountInfo table
        /// </summary>
        /// <param name="database"></param>
        /// <param name="browser"></param>
        /// <returns></returns>
        public Phabricator.Data.Account WhoAmI(Database database, Browser browser)
        {
            if (database.EncryptionKey == null)
            {
                return null;
            }
            else
            {
                return Get(database, browser.Session.Locale)
                        .FirstOrDefault(user => user.Token.Equals(browser.Token.Key));
            }
        }
    }
}