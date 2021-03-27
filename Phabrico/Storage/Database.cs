using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

using Newtonsoft.Json;

using Phabrico.Http;
using Phabrico.Miscellaneous;
using Phabrico.Parsers.Base64;
using Phabrico.Parsers.Remarkup;
using static Phabrico.Phabricator.Data.Account;

[assembly: InternalsVisibleTo("Phabrico.UnitTests")]
namespace Phabrico.Storage
{
    /// <summary>
    /// Represents the SQLite Phabrico database
    /// </summary>
    public class Database : IDisposable
    {
        private static string _datasource = null;
        internal static int _dbVersionInDataFile = 0;

        public void SetConfigurationParameter(string v, object p)
        {
            throw new NotImplementedException();
        }

        private static int _dbVersionInApplication = 1;
        private static DateTime _utcNextTimeToVacuum = DateTime.MinValue;

        private string encryptionKey;
        private PolyCharacterCipherEncryption keywordEncryptor = null;

        /// <summary>
        /// Represents how the column of a table should be encrypted/decrypted
        /// </summary>
        public enum EncryptionMode
        {
            /// <summary>
            /// Unencrypted
            /// </summary>
            None,

            /// <summary>
            /// Encrypted with a private/public key
            /// By default, this key is private. However when Phabrico is configured for Auto-Logon, this key is public
            /// </summary>
            Default,

            /// <summary>
            /// Encrypted with a private key
            /// </summary>
            Private
        }

        /// <summary>
        /// Returns the name of the Phabrico theme (e.g. dark, light)
        /// </summary>
        public string ApplicationTheme
        {
            get
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT theme
                       FROM accountinfo 
                   ", Connection))
                {
                    using (var reader = dbCommand.ExecuteReader())
                    {
                        if (reader.Read() == false)
                        {
                            return "light";
                        }
                        else
                        {
                            return (string)reader["theme"];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// References to the underlying SQLite database connection
        /// </summary>
        public SQLiteConnection Connection
        {
            get;
            private set;
        }

        /// <summary>
        /// The name (and path) of the SQLite database file
        /// </summary>
        public static string DataSource
        {
            get
            {
                if (_datasource == null)
                {
                    _datasource = (string)System.Configuration.ConfigurationManager.AppSettings["DatabaseDirectory"];
                    _datasource += "\\Phabrico.data";
                    System.IO.File.WriteAllText("c:\\temp\\tt.txt", _datasource);
                }

                return _datasource;
            }

            set
            {
                _datasource = value;
            }
        }

        /// <summary>
        /// The public/private key for encrypting/decrypting the SQLite database.
        /// By default, this key is private. However when Phabrico is configured for Auto-Logon, this key is public
        /// </summary>
        public string EncryptionKey
        {
            get
            {
                return encryptionKey;
            }

            set
            {
                encryptionKey = value;
            }
        }

        /// <summary>
        /// The private key for encrypting/decrypting secure parts of the SQLite database
        /// </summary>
        public string PrivateEncryptionKey { get; set; }

        /// <summary>
        /// Initializes an instance of Storage.Database
        /// </summary>
        /// <param name="encryptionKey">Key to encrypt/decrypt the database. This can be null in case no encrypted data is to be retrieved</param>
        public Database(string encryptionKey)
        {
            this.EncryptionKey = encryptionKey;

            SQLiteConnectionStringBuilder sqliteConnectionString = new SQLiteConnectionStringBuilder();
            sqliteConnectionString.DataSource = DataSource;
            if (System.Diagnostics.Debugger.IsAttached)
            {
                sqliteConnectionString.DefaultTimeout = 600000;
            }
            else
            {
                sqliteConnectionString.DefaultTimeout = 5000;
            }
            sqliteConnectionString.SyncMode = SynchronizationModes.Off;
            sqliteConnectionString.JournalMode = SQLiteJournalModeEnum.Memory;
            sqliteConnectionString.PageSize = 65536;
            sqliteConnectionString.CacheSize = 16777216;
            sqliteConnectionString.FailIfMissing = false;
            sqliteConnectionString.ReadOnly = false;
            
            this.Connection = new SQLiteConnection(sqliteConnectionString.ConnectionString);
            this.Connection.Open();

            if (_dbVersionInDataFile == 0)
            {
                string version = GetConfigurationParameter("version");
                if (string.IsNullOrEmpty(version) == false)
                {
                    _dbVersionInDataFile = Int32.Parse(version);
                }
            }

            if (_dbVersionInDataFile == 0)
            {
                Initialize();

                UpgradeIfNeeded();
            }
        }

        /// <summary>
        /// Deletes old session variable records
        /// </summary>
        /// <param name="browser"></param>
        /// <returns></returns>
        public void ClearOldSessionVariables(Browser browser)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       DELETE FROM sessionVariables
                       WHERE dateModified < @dateModified
                   ", Connection))
            {
                dbCommand.Parameters.Add(new SQLiteParameter("dateModified", DateTimeOffset.UtcNow.AddMonths(-6).Ticks));
                dbCommand.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Retrieves the value of a session variable which is stored on the server
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="sessionVariableName"></param>
        /// <returns></returns>
        public string GetSessionVariable(Browser browser, string sessionVariableName)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT name, fingerprint, value, dateModified
                       FROM sessionVariables
                       WHERE name = @sessionVariableName
                   ", Connection))
            {
                AddParameter(dbCommand, "sessionVariableName", Encryption.Encrypt(browser.Fingerprint, sessionVariableName), EncryptionMode.None);

                using (var reader = dbCommand.ExecuteReader())
                {
                    string fingerprint = null;
                    while (reader.Read())
                    {
                        fingerprint = Encryption.Decrypt(browser.Fingerprint, (byte[])reader["fingerprint"]);
                        if (browser.Fingerprint.Equals(fingerprint) == false)
                        {
                            fingerprint = null;
                            continue;
                        }

                        break;
                    }

                    if (fingerprint != null)
                    {
                        return Encryption.Decrypt(browser.Fingerprint, (byte[])reader["value"]);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Stores the value of a session variable on the server
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="sessionVariableName"></param>
        /// <returns></returns>
        public void SetSessionVariable(Browser browser, string sessionVariableName, string sessionVariableValue)
        {
            using (SQLiteCommand dbCommandUpdate = new SQLiteCommand(@"
                       UPDATE sessionVariables
                          SET value = @sessionVariableValue,
                              dateModified = @dateModified
                       WHERE name = @sessionVariableName
                         AND fingerprint = @fingerprint
                   ", Connection))
            {
                AddParameter(dbCommandUpdate, "sessionVariableValue", Encryption.Encrypt(browser.Fingerprint, sessionVariableValue), EncryptionMode.None);
                dbCommandUpdate.Parameters.Add(new SQLiteParameter("dateModified", DateTimeOffset.UtcNow.Ticks));
                AddParameter(dbCommandUpdate, "sessionVariableName", Encryption.Encrypt(browser.Fingerprint, sessionVariableName), EncryptionMode.None);
                AddParameter(dbCommandUpdate, "fingerprint", Encryption.Encrypt(browser.Fingerprint, browser.Fingerprint), EncryptionMode.None);

                if (dbCommandUpdate.ExecuteNonQuery() == 0)
                {
                    using (SQLiteCommand dbCommandInsert = new SQLiteCommand(@"
                               INSERT INTO sessionVariables(name, fingerprint, value, dateModified)
                               VALUES (@sessionVariableName, @fingerprint, @sessionVariableValue, @dateModified)
                           ", Connection))
                    {
                        AddParameter(dbCommandInsert, "sessionVariableValue", Encryption.Encrypt(browser.Fingerprint, sessionVariableValue), EncryptionMode.None);
                        dbCommandInsert.Parameters.Add(new SQLiteParameter("dateModified", DateTimeOffset.UtcNow.Ticks));
                        AddParameter(dbCommandInsert, "sessionVariableName", Encryption.Encrypt(browser.Fingerprint, sessionVariableName), EncryptionMode.None);
                        AddParameter(dbCommandInsert, "fingerprint", Encryption.Encrypt(browser.Fingerprint, browser.Fingerprint), EncryptionMode.None);

                        dbCommandInsert.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// Adds a string based parameter to a given SQL query
        /// </summary>
        /// <param name="dbCommand">SQL query to where the parameter should be defined for</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <param name="parameterValue">Value of the parameter</param>
        /// <param name="encryptionMode">How the parameter should be encrypted/decrypted</param>
        public void AddParameter(SQLiteCommand dbCommand, string parameterName, string parameterValue, EncryptionMode encryptionMode = EncryptionMode.Default)
        {
            switch (encryptionMode)
            {
                case EncryptionMode.None:
                    AddParameter(dbCommand, parameterName, UTF8Encoding.UTF8.GetBytes(parameterValue), EncryptionMode.None);
                    break;

                case EncryptionMode.Default:
                    AddParameter(dbCommand, parameterName, Encryption.Encrypt(EncryptionKey, parameterValue), EncryptionMode.None);
                    break;

                case EncryptionMode.Private:
                    AddParameter(dbCommand, parameterName, Encryption.Encrypt(PrivateEncryptionKey, parameterValue), EncryptionMode.None);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Adds a int based parameter to a given SQL query
        /// </summary>
        /// <param name="dbCommand">SQL query to where the parameter should be defined for</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <param name="parameterValue">Value of the parameter</param>
        /// <param name="encryptionMode">How the parameter should be encrypted/decrypted</param>
        public void AddParameter(SQLiteCommand dbCommand, string parameterName, int parameterValue, EncryptionMode encryptionMode = EncryptionMode.Default)
        {
            switch (encryptionMode)
            {
                case EncryptionMode.None:
                    dbCommand.Parameters.Add(parameterName, System.Data.DbType.Int32);
                    dbCommand.Parameters[parameterName].Value = parameterValue;

                    dbCommand.Parameters.Add(new System.Data.SQLite.SQLiteParameter(parameterName, parameterValue));
                    break;

                case EncryptionMode.Default:
                    AddParameter(dbCommand, parameterName, Encryption.Encrypt(EncryptionKey, parameterValue.ToString()));
                    break;

                case EncryptionMode.Private:
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Adds a boolean based parameter to a given SQL query
        /// </summary>
        /// <param name="dbCommand">SQL query to where the parameter should be defined for</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <param name="parameterValue">Value of the parameter</param>
        /// <param name="encryptionMode">How the parameter should be encrypted/decrypted</param>
        public void AddParameter(SQLiteCommand dbCommand, string parameterName, bool parameterValue, EncryptionMode encryptionMode = EncryptionMode.Default)
        {
            switch (encryptionMode)
            {
                case EncryptionMode.None:
                    dbCommand.Parameters.Add(parameterName, System.Data.DbType.Int32);
                    dbCommand.Parameters[parameterName].Value = parameterValue;

                    dbCommand.Parameters.Add(new System.Data.SQLite.SQLiteParameter(parameterName, parameterValue));
                    break;

                case EncryptionMode.Default:
                    AddParameter(dbCommand, parameterName, Encryption.Encrypt(EncryptionKey, parameterValue.ToString()));
                    break;

                case EncryptionMode.Private:
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Adds a DateTimeOffset based parameter to a given SQL query
        /// </summary>
        /// <param name="dbCommand">SQL query to where the parameter should be defined for</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <param name="parameterValue">Value of the parameter</param>
        /// <param name="encryptionMode">How the parameter should be encrypted/decrypted</param>
        public void AddParameter(SQLiteCommand dbCommand, string parameterName, DateTimeOffset parameterValue, EncryptionMode encryptionMode = EncryptionMode.Default)
        {
            switch (encryptionMode)
            {
                case EncryptionMode.Default:
                    AddParameter(dbCommand, parameterName, Encryption.Encrypt(EncryptionKey, parameterValue.ToString("yyyy-MM-dd HH:mm:ss zzzz", CultureInfo.InvariantCulture)));
                    break;

                case EncryptionMode.None:
                    dbCommand.Parameters.Add(parameterName, System.Data.DbType.UInt64);
                    dbCommand.Parameters[parameterName].Value = parameterValue.ToString();

                    dbCommand.Parameters.Add(new System.Data.SQLite.SQLiteParameter(parameterName, parameterValue.ToString()));
                    break;

                case EncryptionMode.Private:
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Adds a Base64EIDOStream based parameter to a given SQL query
        /// </summary>
        /// <param name="dbCommand">SQL query to where the parameter should be defined for</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <param name="parameterValue">Value of the parameter</param>
        /// <param name="encryptionMode">How the parameter should be encrypted/decrypted</param>
        public void AddParameter(SQLiteCommand dbCommand, string parameterName, Base64EIDOStream parameterValue, EncryptionMode encryptionMode = EncryptionMode.Default)
        {
            if (parameterValue == null)
            {
                parameterValue = new Base64EIDOStream(new byte[0]);
            }

            dbCommand.Parameters.Add(parameterName, System.Data.DbType.Binary);
            parameterValue.Seek(0, SeekOrigin.Begin);
            parameterValue.EncodedData.Seek(0, SeekOrigin.Begin);

            switch (encryptionMode)
            {
                case EncryptionMode.Default:
                    dbCommand.Parameters[parameterName].Value = Encryption.Encrypt(EncryptionKey, new BinaryReader(parameterValue.EncodedData).ReadBytes((int)parameterValue.LengthEncodedData));
                    break;

                case EncryptionMode.None:
                    dbCommand.Parameters[parameterName].Value = new BinaryReader(parameterValue).ReadBytes((int)parameterValue.Length);
                    break;

                case EncryptionMode.Private:
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Adds a byte[] based parameter to a given SQL query
        /// </summary>
        /// <param name="dbCommand">SQL query to where the parameter should be defined for</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <param name="parameterValue">Value of the parameter</param>
        /// <param name="encryptionMode">How the parameter should be encrypted/decrypted</param>
        public void AddParameter(SQLiteCommand dbCommand, string parameterName, byte[] parameterValue, EncryptionMode encryptionMode = EncryptionMode.None)
        {
            switch (encryptionMode)
            {
                case EncryptionMode.None:
                    dbCommand.Parameters.Add(parameterName, System.Data.DbType.Binary, parameterValue.Length);
                    dbCommand.Parameters[parameterName].Value = parameterValue;
                    break;

                case EncryptionMode.Default:
                case EncryptionMode.Private:
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Assigns a given token to a parent token.
        /// E.g. a wiki document that becomes part of a parent wiki document
        /// </summary>
        /// <param name="parentToken">Token to where the child-token should be assigned to</param>
        /// <param name="tokenToBeAssigned">Token to be assigned</param>
        public void DescendTokenFrom(string parentToken, string tokenToBeAssigned)
        {
            if (tokenToBeAssigned.Equals(parentToken)) return;

            using (SQLiteTransaction transaction = Connection.BeginTransaction())
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           INSERT OR REPLACE INTO objectHierarchyInfo(token, parentToken) 
                           VALUES (@token, @parentToken);
                       ", Connection, transaction))
                {
                    AddParameter(dbCommand, "token", tokenToBeAssigned, EncryptionMode.None);
                    AddParameter(dbCommand, "parentToken", parentToken, EncryptionMode.None);
                    dbCommand.ExecuteNonQuery();

                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// Assigns a token to another token.
        /// E.g. a wiki document that is referenced in a maniphest task
        /// </summary>
        /// <param name="mainToken"></param>
        /// <param name="dependentToken"></param>
        public void AssignToken(string mainToken, string dependentToken)
        {
            if (mainToken.Equals(dependentToken)) return;

            using (SQLiteTransaction transaction = Connection.BeginTransaction())
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           INSERT OR REPLACE INTO objectRelationInfo(token, linkedToken) 
                           VALUES (@token, @linkedToken);
                       ", Connection, transaction))
                {
                    AddParameter(dbCommand, "token", mainToken, EncryptionMode.None);
                    AddParameter(dbCommand, "linkedToken", dependentToken, EncryptionMode.None);
                    dbCommand.ExecuteNonQuery();

                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// Removes all files from the SQLite database which are no more referenced in Phriction or Maniphest
        /// </summary>
        internal void CleanupUnusedObjectRelations()
        {
            using (SQLiteTransaction transaction = Connection.BeginTransaction())
            {
                using (SQLiteCommand cmdDeleteUnrelatedFiles = new SQLiteCommand(@"
                            DELETE FROM fileinfo 
                            WHERE token NOT IN (
                                SELECT linkedToken 
                                FROM objectRelationInfo
                            );
                       ", Connection, transaction))
                {
                    cmdDeleteUnrelatedFiles.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }

        /// <summary>
        /// Deletes all references for a given token
        /// </summary>
        /// <param name="mainToken">Token to be freed</param>
        public void ClearAssignedTokens(string mainToken)
        {
            using (SQLiteTransaction transaction = Connection.BeginTransaction())
            {
                using (SQLiteCommand cmdDeleteObjectRelationInfo = new SQLiteCommand(@"
                           DELETE FROM objectRelationInfo
                           WHERE token = @token;
                       ", Connection, transaction))
                {
                    AddParameter(cmdDeleteObjectRelationInfo, "token", mainToken, EncryptionMode.None);
                    cmdDeleteObjectRelationInfo.ExecuteNonQuery();
                }
                
                transaction.Commit();
            }
        }
        
        /// <summary>
        /// Releases all resources
        /// </summary>
        public void Dispose()
        {
            Connection.Close();
        }

        /// <summary>
        /// Returns some account specific configuration parameters
        /// </summary>
        /// <returns></returns>
        public Configuration GetAccountConfiguration()
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT parameters
                       FROM accountinfo 
                   ", Connection))
            {
                using (var reader = dbCommand.ExecuteReader())
                {
                    if (reader.Read() == false || reader["parameters"] == null)
                    {
                        return null;
                    }

                    string serializedParameters = Encryption.Decrypt(EncryptionKey, (byte[])reader["parameters"]);
                    return JsonConvert.DeserializeObject<Configuration>(serializedParameters);
                }
            }
        }

        /// <summary>
        /// Returns an application-specific parameter
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public string GetConfigurationParameter(string name)
        {
            try
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT value
                       FROM dbinfo
                       WHERE name = @name
                   ", Connection))
                {
                    dbCommand.Parameters.Add(new SQLiteParameter("name", name));

                    return dbCommand.ExecuteScalar() as string;
                }
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Return all Phabricator objects which are dependent on a specific token
        /// </summary>
        /// <param name="linkedToken">Token to be searched for</param>
        /// <returns>Dependent Phabricator objects</returns>
        public IEnumerable<Phabricator.Data.PhabricatorObject> GetDependentObjects(string linkedToken)
        {
            List<string> dependerTokens = new List<string>();

            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                    SELECT * FROM objectRelationInfo
                    WHERE linkedToken = @linkedToken;
                ", Connection))
            {
                AddParameter(dbCommand, "linkedToken", linkedToken, EncryptionMode.None);
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        dependerTokens.Add((string)reader["token"]);
                    }
                }
            }

            foreach (string dependeeToken in dependerTokens)
            {
                if (dependeeToken.StartsWith("PHID-NEWTOKEN-"))
                {
                    Stage stageStorage = new Stage();
                    foreach (Stage.Data stageData in stageStorage.Get(this))
                    {
                        if (stageData.Token.Equals(dependeeToken))
                        {
                            Newtonsoft.Json.Linq.JObject stageInfo = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(stageData.HeaderData);

                            if (stageInfo["TokenPrefix"].ToString().Equals(Phabricator.Data.Maniphest.Prefix))
                            {
                                Phabricator.Data.Maniphest maniphestTask = JsonConvert.DeserializeObject(stageData.HeaderData, typeof(Phabricator.Data.Maniphest)) as Phabricator.Data.Maniphest;
                                if (maniphestTask != null)
                                {
                                    yield return maniphestTask;
                                }
                            }
                            
                            if (stageInfo["TokenPrefix"].ToString().Equals(Phabricator.Data.Phriction.Prefix))
                            {
                                Phabricator.Data.Phriction phrictionDocument = JsonConvert.DeserializeObject(stageData.HeaderData, typeof(Phabricator.Data.Phriction)) as Phabricator.Data.Phriction;
                                if (phrictionDocument != null)
                                {
                                    yield return phrictionDocument;
                                }
                            }
                        }
                    }
                }

                if (dependeeToken.StartsWith(Phabricator.Data.Phriction.Prefix))
                {
                    Phriction phrictionStorage = new Phriction();
                    yield return phrictionStorage.Get(this, dependeeToken);
                }

                if (dependeeToken.StartsWith(Phabricator.Data.Maniphest.Prefix))
                {
                    Maniphest maniphestStorage = new Maniphest();
                    yield return maniphestStorage.Get(this, dependeeToken);
                }
                
                if (dependeeToken.StartsWith(Phabricator.Data.File.Prefix))
                {
                    File fileStorage = new File();
                    yield return fileStorage.Get(this, dependeeToken);
                }
                
                if (dependeeToken.StartsWith(Phabricator.Data.User.Prefix))
                {
                    User userStorage = new User();
                    yield return userStorage.Get(this, dependeeToken);
                }
                
                if (dependeeToken.StartsWith(Phabricator.Data.Project.Prefix))
                {
                    Project projectStorage = new Project();
                    yield return projectStorage.Get(this, dependeeToken);
                }
            }
        }

        /// <summary>
        /// Return all Phabricator objects which are referenced by a given Phabricator object
        /// </summary>
        /// <param name="token">Token of Phabricator object to be searched for</param>
        /// <returns>A bunch of referenced Phabricator objects</returns>
        public IEnumerable<Phabricator.Data.PhabricatorObject> GetReferencedObjects(string token)
        {
            List<string> referencedTokens = new List<string>();

            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                    SELECT * FROM objectRelationInfo
                    WHERE token = @token;
                ", Connection))
            {
                AddParameter(dbCommand, "token", token, EncryptionMode.None);
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        referencedTokens.Add((string)reader["linkedToken"]);
                    }
                }
            }

            foreach (string referencedToken in referencedTokens)
            {
                if (referencedToken.StartsWith("PHID-NEWTOKEN-"))
                {
                    Stage stageStorage = new Stage();
                    foreach (Stage.Data stageData in stageStorage.Get(this))
                    {
                        if (stageData.Token.Equals(referencedToken))
                        {
                            Newtonsoft.Json.Linq.JObject stageInfo = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(stageData.HeaderData);

                            if (stageInfo["TokenPrefix"].ToString().Equals(Phabricator.Data.Maniphest.Prefix))
                            {
                                Phabricator.Data.Maniphest maniphestTask = JsonConvert.DeserializeObject(stageData.HeaderData, typeof(Phabricator.Data.Maniphest)) as Phabricator.Data.Maniphest;
                                if (maniphestTask != null)
                                {
                                    yield return maniphestTask;
                                }
                            }
                            
                            if (stageInfo["TokenPrefix"].ToString().Equals(Phabricator.Data.Phriction.Prefix))
                            {
                                Phabricator.Data.Phriction phrictionDocument = JsonConvert.DeserializeObject(stageData.HeaderData, typeof(Phabricator.Data.Phriction)) as Phabricator.Data.Phriction;
                                if (phrictionDocument != null)
                                {
                                    yield return phrictionDocument;
                                }
                            }
                            
                            if (stageInfo["TokenPrefix"].ToString().Equals(Phabricator.Data.File.Prefix))
                            {
                                Phabricator.Data.File fileObject = JsonConvert.DeserializeObject(stageData.HeaderData, typeof(Phabricator.Data.File)) as Phabricator.Data.File;
                                if (fileObject != null)
                                {
                                    yield return fileObject;
                                }
                            }
                        }
                    }
                }

                if (referencedToken.StartsWith(Phabricator.Data.Phriction.Prefix))
                {
                    Phriction phrictionStorage = new Phriction();
                    yield return phrictionStorage.Get(this, referencedToken);
                }

                if (referencedToken.StartsWith(Phabricator.Data.Maniphest.Prefix))
                {
                    Maniphest maniphestStorage = new Maniphest();
                    yield return maniphestStorage.Get(this, referencedToken);
                }
                
                if (referencedToken.StartsWith(Phabricator.Data.File.Prefix))
                {
                    File fileStorage = new File();
                    yield return fileStorage.Get(this, referencedToken);
                }
                
                if (referencedToken.StartsWith(Phabricator.Data.User.Prefix))
                {
                    User userStorage = new User();
                    yield return userStorage.Get(this, referencedToken);
                }
                
                if (referencedToken.StartsWith(Phabricator.Data.Project.Prefix))
                {
                    Project projectStorage = new Project();
                    yield return projectStorage.Get(this, referencedToken);
                }
            }
        }

        /// <summary>
        /// Returns all underlying Phabricator objects of a given type for a given Phabricator objects
        /// </summary>
        /// <param name="token">Phabricator object for which all underlying objects should be returned</param>
        /// <param name="dependentTokenType">Type of objects to be returned</param>
        /// <returns>A bunch of tokens of the underlying Phabricator objects</returns>
        public IEnumerable<string> GetUnderlyingTokens(string token, string dependentTokenType)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT * FROM objectHierarchyInfo
                       WHERE parentToken = @parentToken
                         AND CAST(token AS VARCHAR) LIKE 'PHID-" + dependentTokenType + @"%';
                   ", Connection))
            {
                AddParameter(dbCommand, "parentToken", token, EncryptionMode.None);
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        yield return (string)reader["token"];
                    }
                }
            }

            Stage stageStorage = new Stage();
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT * FROM objectHierarchyInfo
                       WHERE parentToken = @parentToken
                         AND CAST(token AS VARCHAR) LIKE 'PHID-NEWTOKEN-%'
                   ", Connection))
            {
                AddParameter(dbCommand, "parentToken", token, EncryptionMode.None);
                using (var reader = dbCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string tokenOfflineItem = (string)reader["token"];

                        Phabricator.Data.PhabricatorObject phabricatorObject;

                        switch (dependentTokenType)
                        {
                            case "WIKI":
                                phabricatorObject = stageStorage.Get<Phabricator.Data.Phriction>(this, tokenOfflineItem);
                                break;

                            case "TASK":
                                phabricatorObject = stageStorage.Get<Phabricator.Data.Maniphest>(this, tokenOfflineItem);
                                break;

                            default:
                                phabricatorObject = null;
                                break;
                        }

                        if (phabricatorObject == null) continue;

                        yield return tokenOfflineItem;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the size of the Phabrico database file
        /// </summary>
        /// <returns></returns>
        public long GetFileSize()
        {
            return new FileInfo(this.Connection.FileName).Length;
        }

        /// <summary>
        /// This method will create the initial Phabrico database
        /// </summary>
        public void Initialize()
        {
            // create database
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       CREATE TABLE IF NOT EXISTS accountInfo(
                           token VARCHAR(30) PRIMARY KEY,
                           userName BLOB,
                           url BLOB,
                           api BLOB,
                           publicXorCipher BLOB,
                           privateXorCipher BLOB,
                           theme VARCHAR,
                           language VARCHAR,
                           parameters BLOB
                       );

                       CREATE UNIQUE INDEX IF NOT EXISTS uxAccountInfoUserName
                           ON accountInfo(userName);

                       CREATE TABLE IF NOT EXISTS bannedObjectInfo(
                           key BLOB,
                           title BLOB
                       );

                       CREATE TABLE IF NOT EXISTS dbInfo(
                           name VARCHAR PRIMARY KEY,
                           value VARCHAR
                       );

                       CREATE TABLE IF NOT EXISTS favoriteObject (
                           accountUserName BLOB,
                           token           VARCHAR,
                           displayOrder    INT,
                           color           BLOB,

                           PRIMARY KEY (accountUserName, token)
                       );

                       CREATE TABLE IF NOT EXISTS fileChunkInfo(
                           id INT,
                           chunk INT,
                           nbrChunks INT,
                           dateModified SQLITE3_UINT64,
                           data BLOB,

                           PRIMARY KEY (id, chunk)
                       );

                       CREATE TABLE IF NOT EXISTS fileInfo(
                           token VARCHAR(30),
                           id INT,
                           fileName BLOB,
                           macroName BLOB,
                           contentType BLOB,
                           data BLOB,
                           size BLOB,
                           dateModified BLOB,
                           properties BLOB,

                           PRIMARY KEY (token)
                       );

                       CREATE UNIQUE INDEX
                         IF NOT EXISTS idxFileInfoId
                         ON fileInfo (id);

                       CREATE TABLE IF NOT EXISTS keywordInfo(
                           name VARCHAR,
                           token BLOB,
                           description BLOB,
                           nbrocc BLOB,

                           PRIMARY KEY (name, token)
                       );

                       CREATE TABLE IF NOT EXISTS maniphestInfo(
                           token VARCHAR(30) PRIMARY KEY,
                           id VARCHAR,
                           status BLOB,
                           info BLOB
                       );

                       CREATE TABLE IF NOT EXISTS maniphestPriorityInfo(
                           priority INT PRIMARY KEY,
                           info BLOB
                       );

                       CREATE TABLE IF NOT EXISTS maniphestStatusInfo(
                           name BLOB,
                           value BLOB,
                           closed BLOB,
                           info BLOB,

                           PRIMARY KEY (name)
                       );

                       CREATE TABLE IF NOT EXISTS phrictionInfo(
                           token VARCHAR(30) PRIMARY KEY,
                           path BLOB,
                           info BLOB
                       );

                       CREATE TABLE IF NOT EXISTS projectInfo(
                           token VARCHAR(30) PRIMARY KEY,
                           name BLOB,
                           slug BLOB,
                           description BLOB,
                           color BLOB,
                           selected BLOB,
                           dateSynchronized BLOB
                       );

                       CREATE TABLE IF NOT EXISTS sessionVariables(
                           name BLOB,
                           fingerprint BLOB,
                           value BLOB,
                           dateModified INT
                       );

                       CREATE TABLE IF NOT EXISTS stageInfo(
                           token VARCHAR(30),
                           tokenPrefix BLOB,
                           objectID BLOB,
                           operation VARCHAR,
                           dateModified BLOB,
                           headerData BLOB,
                           contentData BLOB,
                           frozen BLOB,

                           PRIMARY KEY (token, operation)
                       );

                       CREATE TABLE IF NOT EXISTS synchronizationLogging(
                           token VARCHAR(30),
                           title BLOB,
                           url BLOB,
                           previousContent BLOB,
                           metadataModified INT,
                           dateModified BLOB,
                           lastModifiedBy BLOB,
                           PRIMARY KEY (token)
                       );

                       CREATE TABLE IF NOT EXISTS objectHierarchyInfo(
                           token VARCHAR(30),
                           parentToken VARCHAR(30),

                           PRIMARY KEY (token, parentToken)
                       );

                       CREATE INDEX
                         IF NOT EXISTS idxObjectHierarchyInfoParentToken
                         ON objectHierarchyInfo (parentToken);

                       CREATE TABLE IF NOT EXISTS objectRelationInfo(
                           token VARCHAR(30),
                           linkedToken VARCHAR(30),

                           PRIMARY KEY (token, linkedToken)
                       );

                       CREATE TABLE IF NOT EXISTS transactionInfo(
                           parentToken VARCHAR(30),
                           id INT,
                           type BLOB,
                           oldValue BLOB,
                           newValue BLOB,
                           author BLOB,
                           dateModified BLOB,

                           PRIMARY KEY (parentToken, id)
                       );

                       CREATE INDEX
                         IF NOT EXISTS idxTransactionInfoInfoParentToken
                         ON transactionInfo (parentToken);

                       CREATE TABLE IF NOT EXISTS userInfo(
                           token VARCHAR(30) PRIMARY KEY,
                           realName BLOB,
                           userName BLOB,
                           selected BLOB,
                           dateSynchronized BLOB
                       );
                   ", Connection))
            {
                dbCommand.ExecuteNonQuery();
            }

            if (_utcNextTimeToVacuum < DateTime.UtcNow)
            {
                try
                {
                    // clean up old data
                    bool oldDataWasRemoved = false;
                    using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       DELETE FROM fileChunkInfo
                       WHERE dateModified < @dateModified
                   ", Connection))
                    {
                        DateTimeOffset yesterday = DateTimeOffset.UtcNow.AddDays(-1);
                        AddParameter(dbCommand, "dateModified", yesterday, EncryptionMode.None);
                        dbCommand.CommandTimeout = 0;
                        oldDataWasRemoved = (dbCommand.ExecuteNonQuery() > 0);
                    }

                    if (oldDataWasRemoved)
                    {
                        Shrink();
                    }
                }
                catch (System.Exception e)
                {
                    SQLiteException sqliteException = e as SQLiteException;
                    if (sqliteException != null)
                    {
                        if ((SQLiteErrorCode)sqliteException.ErrorCode == SQLiteErrorCode.Busy)
                        {
                            // we might get an exception in case the local SQLite database is locked
                            // This is not a big issue -> ignore exception
                            return;
                        }
                    }

                    Logging.WriteInfo(null, e.Message);
                }

                _utcNextTimeToVacuum = DateTime.UtcNow.AddSeconds(60);
            }
        }

        /// <summary>
        /// Decrypts a PolyCharacterCipherEncrypted (Vigenère) string
        /// </summary>
        /// <param name="encryptedString"></param>
        /// <returns></returns>
        public string PolyCharacterCipherDecrypt(string encryptedString)
        {
            if (keywordEncryptor == null)
            {
                if (this.EncryptionKey != null)
                {
                    keywordEncryptor = new PolyCharacterCipherEncryption(this.EncryptionKey);
                }
                else
                {
                    return null;
                }
            }

            byte[] decryptedBytes = keywordEncryptor.Decrypt(encryptedString);
            string decryptedString = UTF8Encoding.UTF8.GetString(decryptedBytes);

            return decryptedString;
        }

        /// <summary>
        /// Encrypts a string into PolyCharacterCipherEncrypted (Vigenère) string
        /// </summary>
        /// <param name="decryptedString"></param>
        /// <returns></returns>
        public string PolyCharacterCipherEncrypt(string decryptedString)
        {
            if (keywordEncryptor == null)
            {
                if (this.EncryptionKey != null)
                {
                    keywordEncryptor = new PolyCharacterCipherEncryption(this.EncryptionKey);
                }
                else
                {
                    return null;
                }
            }

            byte[] decryptedBytes = UTF8Encoding.UTF8.GetBytes(decryptedString);
            string encryptedString = keywordEncryptor.Encrypt(decryptedBytes);

            return encryptedString;
        }

        /// <summary>
        /// Renames the ID of a given object.
        /// This is mainly done when a staged object has been uploaded to Phabricator and downloaded again (with a new Phabricator generated ID)
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="referencedPhabricatorObject"></param>
        /// <param name="newReference"></param>
        public void RenameReferences(Browser browser, Phabricator.Data.PhabricatorObject referencedPhabricatorObject, string newReference)
        {
            RemarkupEngine remarkupEngine = new RemarkupEngine();
            RemarkupParserOutput remarkupParserOutput;

            foreach (Phabricator.Data.PhabricatorObject referencer in GetDependentObjects(referencedPhabricatorObject.Token))
            {
                string remarkupContent = null;

                // determine type of referencing object
                // check if referencing object is a maniphest task
                Phabricator.Data.Maniphest referencingManiphestTask = referencer as Phabricator.Data.Maniphest;
                if (referencingManiphestTask != null)
                {
                    remarkupContent = referencingManiphestTask.Description;
                }

                // check if referencing object is a phriction document
                Phabricator.Data.Phriction referencingPhrictionDocument = referencer as Phabricator.Data.Phriction;
                if (referencingPhrictionDocument != null)
                {
                    remarkupContent = referencingPhrictionDocument.Content;
                }

                // check if referencing object was identified
                if (remarkupContent == null)
                {
                    // unknown referencing object -> skip
                    continue;
                }

                // start parsing remarkup content of referencing object
                remarkupEngine.ToHTML(null, this, browser, "/", remarkupContent, out remarkupParserOutput, false);

                // check if referenced object is a maniphest task
                Phabricator.Data.Maniphest referencedManiphestTask = referencedPhabricatorObject as Phabricator.Data.Maniphest;
                if (referencedManiphestTask != null)
                {
                    foreach (Parsers.Remarkup.Rules.RuleReferenceManiphestTask reference in remarkupParserOutput.TokenList.OfType<Parsers.Remarkup.Rules.RuleReferenceManiphestTask>().OrderByDescending(rule => rule.Start))
                    {
                        System.Text.RegularExpressions.Match matchManiphestTaskReference = RegexSafe.Match(reference.Text, @"^({?)T(" + referencedManiphestTask.ID + ")(}?)", System.Text.RegularExpressions.RegexOptions.Singleline);
                        if (matchManiphestTaskReference.Success)
                        {
                            // correct remarkup content
                            remarkupContent = remarkupContent.Substring(0, reference.Start + matchManiphestTaskReference.Groups[2].Index)
                                                                    + newReference
                                                                    + remarkupContent.Substring(reference.Start + matchManiphestTaskReference.Groups[2].Index + matchManiphestTaskReference.Groups[2].Length);

                            if (referencingManiphestTask != null)
                            {
                                referencingManiphestTask.Description = remarkupContent;

                                Stage stageStorage = new Stage();
                                stageStorage.Modify(this, referencingManiphestTask);
                            }

                            if (referencingPhrictionDocument != null)
                            {
                                referencingPhrictionDocument.Content = remarkupContent;

                                Stage stageStorage = new Stage();
                                stageStorage.Modify(this, referencingPhrictionDocument);
                            }
                        }
                    }
                }

                // check if referenced object is a phriction document
                Phabricator.Data.Phriction referencedPhrictionDocument = referencedPhabricatorObject as Phabricator.Data.Phriction;
                if (referencedPhrictionDocument != null)
                {
                    foreach (Parsers.Remarkup.Rules.RuleHyperLink reference in remarkupParserOutput.TokenList.OfType<Parsers.Remarkup.Rules.RuleHyperLink>().OrderByDescending(rule => rule.Start))
                    {
                        System.Text.RegularExpressions.Match matchPhrictionDocumentReference = RegexSafe.Match(reference.Text, @"^\[\[ *(/" + referencedPhrictionDocument.Path.TrimEnd('/') + @"/?) *(|[^]]*)\]\]", System.Text.RegularExpressions.RegexOptions.Singleline);
                        if (matchPhrictionDocumentReference.Success)
                        {
                            remarkupContent = remarkupContent.Substring(0, reference.Start + matchPhrictionDocumentReference.Groups[1].Index)
                                                                    + newReference
                                                                    + remarkupContent.Substring(reference.Start + matchPhrictionDocumentReference.Groups[1].Index + matchPhrictionDocumentReference.Groups[1].Length);

                            if (referencingManiphestTask != null)
                            {
                                referencingManiphestTask.Description = remarkupContent;

                                Stage stageStorage = new Stage();
                                stageStorage.Modify(this, referencingManiphestTask);
                            }

                            if (referencingPhrictionDocument != null)
                            {
                                referencingPhrictionDocument.Content = remarkupContent;

                                Stage stageStorage = new Stage();
                                stageStorage.Modify(this, referencingPhrictionDocument);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Sets an application-specific parameter
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetConfigurationParameter(string name, string value)
        {
            using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                        INSERT OR REPLACE INTO dbInfo(name, value)
                        VALUES (@name, @value);
                    ", Connection))
            {
                dbCommand.Parameters.Add(new SQLiteParameter("name", name));
                dbCommand.Parameters.Add(new SQLiteParameter("value", value));
                dbCommand.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Shrinks or Vacuums the database
        /// </summary>
        public void Shrink()
        {
            try
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                                VACUUM
                            ", Connection))
                {
                    dbCommand.ExecuteNonQuery();
                }
            }
            catch (System.Exception e)
            {
                SQLiteException sqliteException = e as SQLiteException;
                if (sqliteException != null)
                {
                    if ((SQLiteErrorCode)sqliteException.ErrorCode == SQLiteErrorCode.Busy)
                    {
                        // we might get an exception in case the local SQLite database is locked
                        // This is not a big issue -> ignore exception
                        return;
                    }
                }

                Logging.WriteInfo(null, e.Message);
            }
        }

        /// <summary>
        /// Remove or change the parent-child link for a given token
        /// </summary>
        /// <param name="tokenToBeUnassigned"></param>
        /// <param name="parentToken"></param>
        public void UndescendTokenFrom(string tokenToBeUnassigned, string parentToken = "")
        {
            using (SQLiteTransaction transaction = Connection.BeginTransaction())
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                           DELETE FROM objectHierarchyInfo
                           WHERE token = @token
                             AND (parentToken = @parentToken OR '' = CAST(@parentToken AS VARCHAR));
                       ", Connection, transaction))
                {
                    AddParameter(dbCommand, "token", tokenToBeUnassigned, EncryptionMode.None);
                    AddParameter(dbCommand, "parentToken", parentToken, EncryptionMode.None);
                    dbCommand.ExecuteNonQuery();

                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// This method is fired once and may change the database schemes in case of application upgrades
        /// </summary>
        /// <returns></returns>
        public bool UpgradeIfNeeded()
        {
            if (_dbVersionInDataFile == 0)
            {
                try
                {
                    string version = GetConfigurationParameter("version");
                    if (string.IsNullOrEmpty(version) == false)
                    {
                        _dbVersionInDataFile = Int32.Parse(version);
                    }

                    for (int dbVersion = _dbVersionInDataFile; dbVersion < _dbVersionInApplication; dbVersion++)
                    {
                        if (UpgradeToVersion(dbVersion + 1) == false)
                        {
                            break;
                        }
                    }
                }
                catch (System.Exception exception)
                {
                    Logging.WriteException(null, exception);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// This method is fired in UpgradeIfNeeded and will execute some specific database scheme modifications
        /// </summary>
        /// <param name="dbVersion"></param>
        /// <returns></returns>
        private bool UpgradeToVersion(int dbVersion)
        {
            // store version number in database
            SetConfigurationParameter("version", dbVersion.ToString());

            return true;
        }

        /// <summary>
        /// Verifies if a given account token hash is found in the Phabrico database
        /// </summary>
        /// <param name="tokenHash"></param>
        /// <param name="noUserConfigured"></param>
        /// <returns></returns>
        public UInt64[] ValidateLogIn(string tokenHash, out bool noUserConfigured)
        {
            noUserConfigured = false;
            if (tokenHash == null)
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT 1
                       FROM accountinfo;
                   ", Connection))
                {
                    using (var reader = dbCommand.ExecuteReader())
                    {
                        noUserConfigured = !reader.Read();
                    }
                }

                return null;
            }
            else
            {
                using (SQLiteCommand dbCommand = new SQLiteCommand(@"
                       SELECT 1 AS priority, token, publicXorCipher
                       FROM accountinfo 
                       WHERE token = @tokenHash
                       
                       UNION 
                   
                       SELECT 2 AS priority, token, publicXorCipher 
                       FROM accountinfo
                   
                       ORDER BY priority LIMIT 1;
                   ", Connection))
                {
                    AddParameter(dbCommand, "tokenHash", tokenHash, EncryptionMode.None);

                    using (var reader = dbCommand.ExecuteReader())
                    {
                        noUserConfigured = !reader.Read();
                        if (noUserConfigured)
                        {
                            return null;
                        }

                        if (tokenHash.Equals((string)reader["token"]))
                        {
                            byte[] publicXorCipherBytes = (byte[])reader["publicXorCipher"];
                            UInt64[] result = new UInt64[publicXorCipherBytes.Length / 8];
                            Buffer.BlockCopy(publicXorCipherBytes, 0, result, 0, publicXorCipherBytes.Length);

                            return result;
                        }
                    }
                }
            }

            return null;
        }
    }
}
