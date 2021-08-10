using Phabrico.Miscellaneous;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Phabrico.Http
{
    /// <summary>
    /// Represents Phabrico's session manager
    /// It will manage the web browser connections
    /// </summary>
    public class SessionManager
    {
        /// <summary>
        /// Represents a web client session
        /// </summary>
        public class ClientSession
        {
            /// <summary>
            /// Locale (language) for the current session
            /// </summary>
            public string Locale { get; set; } = null;

            /// <summary>
            /// Web form variables being shared between client and server
            /// </summary>
            public DictionarySafe<string, string> FormVariables { get; internal set; } = new DictionarySafe<string, string>();

            /// <summary>
            /// Buffer for use of download and upload of files
            /// </summary>
            public byte[] OctetStreamData { get; internal set; }
        }

        /// <summary>
        /// Represents an identifier for a browser session
        /// </summary>
        public class Token
        {
            private string privateEncryptionKey;


            /// <summary>
            /// Key stored in the database (to verify if the user is known in the SQLite database)
            /// </summary>
            public string Key { get; private set; }

            /// <summary>
            /// Key stored in the browser (http cookie)
            /// </summary>
            public string ID { get; set; }

            /// <summary>
            /// Partial key stored in memory (to decrypt the SQlite database)
            /// To get the full key, this string should be XOR'ed with a UInt256 value which is stored in the AccountInfo table
            /// </summary>
            public string EncryptionKey { get; set; }

            /// <summary>
            /// Determines how the user is logged on
            /// </summary>
            public AuthenticationFactor AuthenticationFactor { get; set; }

            /// <summary>
            /// Private encryption key stored in memory (to decrypt some of the SQlite database, i.e. EncryptionMode.Private)
            /// </summary>
            public string PrivateEncryptionKey
            {
                get
                {
                    switch (AuthenticationFactor)
                    {
                        case AuthenticationFactor.Knowledge:
                            return privateEncryptionKey;

                        case AuthenticationFactor.Ownership:
                            return privateEncryptionKey;

                        default:
                            return null;
                    }
                }

                set
                {
                    privateEncryptionKey = value;
                }
            }

            /// <summary>
            /// The time at which the session was last poked (see Poke method and Invalid property)
            /// </summary>
            private DateTime lastPokeTime;

            /// <summary>
            /// Returns true if the session hasn't been poked recently (see Poke method)
            /// If AuthenticationFactor is not Knowledge, the session will always remain valid
            /// </summary>
            public bool Invalid
            {
                get
                {
                    if (AuthenticationFactor == AuthenticationFactor.Knowledge)
                    {
                        return DateTime.UtcNow.Subtract(lastPokeTime).TotalSeconds >= 120;
                    }

                    return false;
                }
            }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="key"></param>
            public Token(string key)
            {
                Key = key;
                ID = RandomString(32);

                AuthenticationFactor = AuthenticationFactor.Knowledge;

                Poke();
            }

            /// <summary>
            /// This method should be fired to keep the session longer alive.
            /// If this method isn't executed for a long time, the session becomes invalidated and
            /// the user needs to log on again (see also the Invalid property)
            /// </summary>
            public void Poke()
            {
                lastPokeTime = DateTime.UtcNow;
            }

            /// <summary>
            /// Returns a random string of a given length containing only (uppercase and lowercase) letters and numbers
            /// </summary>
            /// <param name="size">the length of the string to be generated</param>
            /// <returns>random string</returns>
            public static string RandomString(int size)
            {
                StringBuilder builder = new StringBuilder();
                Random random = new Random();
                char ch = '.';
                for (int i = 0; i < size; i++)
                {
                    int randomValue = Convert.ToInt32(Math.Floor(62 * random.NextDouble()));
                    if (randomValue >= 0 && randomValue < 26) ch = Convert.ToChar(randomValue + 1 + 65);
                    if (randomValue >= 26 && randomValue < 52) ch = Convert.ToChar(randomValue - 26 + 97);
                    if (randomValue >= 52 && randomValue < 62) ch = Convert.ToChar(randomValue - 52 + 48);

                    builder.Append(ch);
                }
                return builder.ToString();
            }
        }

        /// <summary>
        /// List of active client tokens
        /// </summary>
        public static List<Token> ActiveTokens { get; } = new List<Token>();

        /// <summary>
        /// Active client sessions per token-id
        /// </summary>
        public Dictionary<string, ClientSession> ClientSessions = new Dictionary<string, ClientSession>();

        /// <summary>
        /// This token will be  created during startup and is temporary used during the authentication.
        /// After being authenticated, a real token will be created and being used instead
        /// </summary>
        public static Token TemporaryToken { get; private set; } = null;

        /// <summary>
        /// Initializes a new instance of the SessionManager
        /// </summary>
        public SessionManager()
        {
            if (TemporaryToken == null)
            {
                TemporaryToken = CreateToken("temp", null);
            }
        }

        /// <summary>
        /// Cancels a session token based on a give token id
        /// </summary>
        /// <param name="tokenId"></param>
        public void CancelToken(string tokenId) 
        {
            ActiveTokens.RemoveAll(token => token.ID.Equals(tokenId));

            if (ClientSessions.Any(token => token.Key.Equals(tokenId)))
            {
                ClientSessions.Remove(tokenId);
            }

            Logging.WriteInfo(null, "SessionManager: token canceled: {0}", tokenId);
        }

        /// <summary>
        /// Creates a new session token based on a token hash
        /// </summary>
        /// <param name="tokenHash"></param>
        /// <param name="browser"></param>
        /// <returns></returns>
        public Token CreateToken(string tokenHash, Browser browser)
        {
            Token token = new Token(tokenHash);

            ActiveTokens.Add(token);
            ClientSessions[token.ID] = new ClientSession();

            if (browser == null)
            {
                ClientSessions[token.ID].Locale = "en";
            }
            else
            {
                ClientSessions[token.ID].Locale = browser.Language;
            }
            
            Logging.WriteInfo(token.ID, "SessionManager: new token created: {0}", token.ID);
            if (browser != null)
            {
                Logging.WriteInfo(token.ID, "Browser: {0}", browser.Request.UserAgent);
            }

            return token;
        }

        /// <summary>
        /// Returns the session token for a given Browser instance
        /// </summary>
        /// <param name="browser"></param>
        /// <returns></returns>
        public static Token GetToken(Browser browser)
        {
            string tokenId = browser.GetCookie("token");
            SessionManager.Token token = browser.HttpServer.Session.GetToken(tokenId);
            return token;
        }

        /// <summary>
        /// Returns a session token for a given token-id
        /// </summary>
        /// <param name="tokenId"></param>
        /// <returns></returns>
        public Token GetToken(string tokenId)
        {
            return ActiveTokens.Where(token => token != null && token.ID.Equals(tokenId))
                               .OrderByDescending(token => token.PrivateEncryptionKey)
                               .FirstOrDefault();
        }

        /// <summary>
        /// Validates if a given token-id is still representing a valid session token
        /// </summary>
        /// <param name="tokenId"></param>
        /// <returns></returns>
        public bool TokenValid(string tokenId)
        {
            Token activeToken = ActiveTokens.FirstOrDefault(token => token != null && token.ID.Equals(tokenId));
            if (activeToken == null) return false;

            if (activeToken.Invalid)
            {
                ActiveTokens.Remove(activeToken);

                if (ClientSessions.Any(token => token.Key.Equals(tokenId)))
                {
                    ClientSessions.Remove(tokenId);
                }

                Logging.WriteInfo(null, "SessionManager: token invalidated: {0}", tokenId);
                return false;
            }

            activeToken.Poke();

            return true;
        }
    }
}
