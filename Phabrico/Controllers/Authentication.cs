using Newtonsoft.Json;
using Phabrico.Http;
using Phabrico.Http.Response;
using Phabrico.Miscellaneous;
using Phabrico.Phabricator.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Phabrico.Controllers
{
    /// <summary>
    /// Represents the controller being used for authenticating in Phabrico
    /// </summary>
    public class Authentication : Controller
    {
        /// <summary>
        /// variable which is used to initialize some stuff after the first user is connected to Phabrico
        /// </summary>
        private static bool firstAuthentication = true;

        /// <summary>
        /// This method is fired after the user has posted its username and password to the HTTP server
        /// </summary>
        /// <param name="httpServer">webserver object</param>
        /// <param name="browser">webbrowser connection</param>
        /// <param name="parameters">
        /// First parameter should be 'login', the second one and others construct the local url to be redirected 
        /// to after the authentication process is finished
        /// </param>
        [UrlController(URL = "/auth")]
        public void HttpPostAuthenticate(Http.Server httpServer, Browser browser, string[] parameters)
        {
            if (httpServer == null) return;
            if (browser == null) return;
            if (parameters == null) return;

            if (parameters.Any())
            {
                if (parameters[0].Equals("login"))
                {
                    string accountDataUserName = browser.Session.FormVariables[browser.Request.RawUrl]["username"];
                    string accountDataPassword = browser.Session.FormVariables[browser.Request.RawUrl]["password"];
                    string tokenHash = Encryption.GenerateTokenKey(accountDataUserName, accountDataPassword);  // tokenHash is stored in the database
                    string publicEncryptionKey = Encryption.GenerateEncryptionKey(accountDataUserName, accountDataPassword);  // encryptionKey is not stored in database (except when security is disabled)
                    string privateEncryptionKey = Encryption.GeneratePrivateEncryptionKey(accountDataUserName, accountDataPassword);  // privateEncryptionKey is not stored in database

                    Http.Response.HomePage httpResponse = new Http.Response.HomePage(httpServer, browser, "/");

                    using (Storage.Database database = new Storage.Database(null))
                    {
                        Storage.Account accountStorage = new Storage.Account();

                        httpResponse.Theme = database.ApplicationTheme;

                        bool noUserConfigured;
                        UInt64[] publicXorCipher = database.ValidateLogIn(tokenHash, out noUserConfigured);
                        if (publicXorCipher == null)
                        {
                            if (noUserConfigured)
                            {
                                httpResponse.Status = Http.Response.HomePage.HomePageStatus.EmptyDatabase;
                                Account newAccountData = new Account();
                                newAccountData.ConduitAPIToken = browser.Session.FormVariables[browser.Request.RawUrl]["conduitApiToken"];
                                newAccountData.PhabricatorUrl = browser.Session.FormVariables[browser.Request.RawUrl]["phabricatorUrl"];
                                newAccountData.Token = tokenHash;
                                newAccountData.UserName = accountDataUserName;
                                newAccountData.Parameters = new Account.Configuration();
                                newAccountData.Parameters.ColumnHeadersToHide = "".Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                newAccountData.Theme = "light";

                                database.EncryptionKey = publicEncryptionKey;
                                database.PrivateEncryptionKey = privateEncryptionKey;

                                accountStorage.Add(database, newAccountData);

                                // create new session token
                                SessionManager.Token token = httpServer.Session.CreateToken(tokenHash, browser);

                                // use real session token instead of temporary session token
                                browser.ResetToken(token);

                                // copy form-variables from temporary session
                                DictionarySafe<string, string> temporaryFormVariables = new DictionarySafe<string, string>(browser.HttpServer
                                                                                                                                  .Session
                                                                                                                                  .ClientSessions[SessionManager.TemporaryToken.ID]
                                                                                                                                  .FormVariables[browser.Request.RawUrl]
                                                                                                                         );
                                browser.Session.FormVariables[browser.Request.RawUrl] = temporaryFormVariables;

                                // clean form-variables from temporary session
                                browser.HttpServer
                                       .Session
                                       .ClientSessions[SessionManager.TemporaryToken.ID]
                                       .FormVariables[browser.Request.RawUrl] = new DictionarySafe<string, string>();


                                browser.SetCookie("token", token.ID, true);
                                token.EncryptionKey = publicEncryptionKey;
                                token.PrivateEncryptionKey = privateEncryptionKey;
                            }
                            else
                            {
                                httpResponse.Status = Http.Response.HomePage.HomePageStatus.AuthenticationError;
                                httpResponse.HttpStatusMessage = "NOK";  // mark the user validation as invalid, so the page won't be reloaded
                            }
                        }
                        else
                        {
                            httpResponse.Status = Http.Response.HomePage.HomePageStatus.Authenticated;
                            SessionManager.Token token = httpServer.Session.CreateToken(tokenHash, browser);
                            UInt64[] privateXorCipher = accountStorage.GetPrivateXorCipher(database, token);

                            browser.SetCookie("token", token.ID, true);
                            token.EncryptionKey = Encryption.XorString(publicEncryptionKey, publicXorCipher);
                            token.PrivateEncryptionKey = Encryption.XorString(privateEncryptionKey, privateXorCipher);
                            token.AuthenticationFactor = AuthenticationFactor.Knowledge;
                        }

                        // send content to browser
                        httpResponse.Send(browser, string.Join("/", parameters.Skip(1)));

                        if (httpResponse.Status == HomePage.HomePageStatus.Authenticated)
                        {
                            // postpone some after-initialization stuff to make te response the browser faster
                            // E.g. Firefox has a slower visible response when this code is not executed in a postponed task
                            Task.Delay(500)
                                .ContinueWith((task, obj) =>
                            {
                                object[] objects = (object[])obj;
                                Browser localBrowser = (Browser)objects[0];
                                Storage.Database localDatabase = (Storage.Database)objects[1];
                                using (localDatabase = new Storage.Database(localDatabase.EncryptionKey))
                                {
                                // initialize some stuff after the first time we authenticate
                                if (firstAuthentication && httpResponse.Status == Http.Response.HomePage.HomePageStatus.Authenticated)
                                    {
                                        firstAuthentication = false;

                                    // clean up unreferenced staged files
                                    Storage.Stage.DeleteUnreferencedFiles(localDatabase, localBrowser);
                                    }
                                }

                            // start preloading
                            if (httpResponse.Status == Http.Response.HomePage.HomePageStatus.Authenticated)
                                {
                                    string encryptionKey = Encryption.XorString(publicEncryptionKey, publicXorCipher);
                                    httpServer.PreloadContent(localBrowser, encryptionKey, accountDataUserName);
                                }
                            }, new object[] { browser, database });
                        }
                    }

                    return;
                }
            }

            throw new InvalidOperationException();
        }

        /// <summary>
        /// This method is fired when the user has changed the language in the Create-User dialog.
        /// </summary>
        [UrlController(URL = "/auth/setLanguage")]
        public void HttpPostModifyInitialLanguage(Http.Server httpServer, Browser browser, string[] parameters)
        {
            string newLanguage = parameters.FirstOrDefault();
            if (string.IsNullOrEmpty(newLanguage)) return;

            httpServer.Session.ClientSessions.FirstOrDefault().Value.Locale = newLanguage;
        }

        /// <summary>
        /// This method is fired when the user has changed the language of the application.
        /// This method is only fired when the user is logged on (see also HttpPostModifyInitialLanguage)
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/auth/language")]
        public JsonMessage HttpPostModifyLanguage(Http.Server httpServer, Browser browser, string[] parameters)
        {
            if (browser.InvalidCSRF(browser.Request.RawUrl)) throw new Phabrico.Exception.InvalidCSRFException();

            string newLanguage = browser.Session.FormVariables[browser.Request.RawUrl]["newLanguage"];
            Storage.Account accountStorage = new Storage.Account();

            SessionManager.Token token = SessionManager.GetToken(browser);

            browser.Session.Locale = newLanguage;
            browser.SetCookie("language", newLanguage, false);

            // save new language into the database
            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                database.SetSessionVariable(browser, "language", newLanguage);
            }
            
            string jsonData = JsonConvert.SerializeObject(new
            {
                Status = "OK"
            });

            return new JsonMessage(jsonData);
        }

        /// <summary>
        /// This method is fired when the user has confirmed its new password
        /// </summary>
        /// <param name="httpServer"></param>
        /// <param name="browser"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [UrlController(URL = "/auth/password")]
        public JsonMessage HttpPostModifyPassword(Http.Server httpServer, Browser browser, string[] parameters)
        {
            string currentPassword = browser.Session.FormVariables[browser.Request.RawUrl]["oldPassword"];
            string newPassword = browser.Session.FormVariables[browser.Request.RawUrl]["newPassword1"];

            Storage.Account accountStorage = new Storage.Account();

            SessionManager.Token token = SessionManager.GetToken(browser);
            if (token == null) throw new Phabrico.Exception.AccessDeniedException(browser.Request.RawUrl, "session expired");

            using (Storage.Database database = new Storage.Database(EncryptionKey))
            {
                string jsonData;

                // set private encryption key
                database.PrivateEncryptionKey = token.PrivateEncryptionKey;
                

                Phabricator.Data.Account existingAccount = accountStorage.Get(database, token);
                if (existingAccount != null)
                {
                    // verify if old password is correct
                    string currentTokenHash = Encryption.GenerateTokenKey(existingAccount.UserName, currentPassword);

                    bool noUserConfigured;
                    UInt64[] publicXorCipher = database.ValidateLogIn(currentTokenHash, out noUserConfigured);
                    if (publicXorCipher != null)
                    {
                        // old password is correct

                        // create new token and encryption keys
                        string newTokenHash = Encryption.GenerateTokenKey(existingAccount.UserName, newPassword);
                        string newPublicEncryptionKey = Encryption.GenerateEncryptionKey(existingAccount.UserName, newPassword);
                        string newPrivateEncryptionKey = Encryption.GeneratePrivateEncryptionKey(existingAccount.UserName, newPassword);
                        
                        UInt64[] newPublicXorValue = GetXorValue(EncryptionKey, newPublicEncryptionKey);
                        UInt64[] newPrivateXorValue = GetXorValue(database.PrivateEncryptionKey, newPrivateEncryptionKey);

                        if (accountStorage.UpdateToken(database, currentTokenHash, newTokenHash, newPublicXorValue, newPrivateXorValue))
                        {
                            // recreate session token
                            httpServer.Session.CancelToken(TokenId);
                            SessionManager.Token newToken = httpServer.Session.CreateToken(newPublicEncryptionKey, browser);
                            browser.SetCookie("token", newToken.ID, true);

                            // password was successfully changed
                            jsonData = JsonConvert.SerializeObject(new
                            {
                                Status = "OK"
                            });

                            JsonMessage jsonMessage = new JsonMessage(jsonData);
                            return jsonMessage;
                        }
                    }
                }


                // unable to change password
                jsonData = JsonConvert.SerializeObject(new
                {
                    Status = "NOK"
                });

                return new JsonMessage(jsonData);
            }
        }

        /// <summary>
        /// This function will return the XOR value between 2 strings of the same length
        /// It is used to calculate the XOR mask between the database encryption key and the (new) password.
        /// The database encryption key is generated once, based on the first password.
        /// When the password is changed, the database will not be re-encrypted.
        /// The XOR-mask is stored in the AccountInfo table
        /// </summary>
        /// <param name="decodedString"></param>
        /// <param name="encodedString"></param>
        /// <returns></returns>
        internal static UInt64[] GetXorValue(string decodedString, string encodedString)
        {
            UInt64[] result = new UInt64[4];

            for (int i = decodedString.Length - 1; i >= 0; i--)
            {
                int xorCharacter = decodedString[i] ^ encodedString[i];

                result[i / 8] <<= 8;
                result[i / 8] += (UInt64)xorCharacter;
            }

            return result;
        }
    }
}