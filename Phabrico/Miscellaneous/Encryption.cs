using Phabrico.Exception;
using Phabrico.Http;
using Phabrico.Parsers.Base64;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace Phabrico.Miscellaneous
{
    /// <summary>
    /// Helper class for encryption/decryption functions
    /// </summary>
    public class Encryption
    {
        /// <summary>
        /// Decrypts an encrypted string
        /// </summary>
        /// <param name="encryptionKey">key to use for decryption</param>
        /// <param name="encryptedData">data to be decrypted</param>
        /// <returns>Decrypted string</returns>
        public static string Decrypt(string encryptionKey, byte[] encryptedData)
        {
            if (encryptionKey == null) throw new AuthorizationException();  // timeout occurred -> login again
            if (encryptionKey.Length != 32)
            {
                encryptionKey = GenerateEncryptionKey(encryptionKey);
            }

            // Declare the string used to hold 
            // the decrypted text. 
            string plaintext = null;

            // Create a RijndaelManaged object 
            // with the specified key and IV. 
            using (RijndaelManaged rijAlg = new RijndaelManaged())
            {
                rijAlg.Key = ASCIIEncoding.ASCII.GetBytes(encryptionKey);
                rijAlg.IV = ASCIIEncoding.ASCII.GetBytes(encryptionKey.Substring(0, 16).ToUpper());
                rijAlg.Padding = PaddingMode.PKCS7;
                Array.Reverse(rijAlg.IV);

                // Create a decrytor to perform the stream transform.
                ICryptoTransform decryptor = rijAlg.CreateDecryptor(rijAlg.Key, rijAlg.IV);

                // Create the streams used for decryption. 
                using (MemoryStream msDecrypt = new MemoryStream(encryptedData))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            // Read the decrypted bytes from the decrypting stream 
                            // and place them in a string.
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }
            }

            return plaintext;
        }

        /// <summary>
        /// Decrypts an encrypted stream of Base64 data
        /// </summary>
        /// <param name="encryptionKey">key to use for decryption</param>
        /// <param name="encryptedStream">stream to be decrypted</param>
        /// <returns>Decrypted stream</returns>
        public static Base64EIDOStream Decrypt(string encryptionKey, Stream encryptedStream)
        {
            if (encryptionKey == null) throw new AuthorizationException();  // timeout occurred -> login again

            // Declare the stream used to hold the decrypted data. 
            Base64EIDOStream base64EIDOStream = new Base64EIDOStream();

            // Create a RijndaelManaged object 
            // with the specified key and IV. 
            using (RijndaelManaged rijAlg = new RijndaelManaged())
            {
                rijAlg.Key = ASCIIEncoding.ASCII.GetBytes(encryptionKey);
                rijAlg.IV = ASCIIEncoding.ASCII.GetBytes(encryptionKey.Substring(0, 16).ToUpper());
                rijAlg.Padding = PaddingMode.PKCS7;
                Array.Reverse(rijAlg.IV);

                // Create a decrytor to perform the stream transform.
                ICryptoTransform decryptor = rijAlg.CreateDecryptor(rijAlg.Key, rijAlg.IV);

                // Create the streams used for decryption. 
                using (CryptoStream csDecrypt = new CryptoStream(encryptedStream, decryptor, CryptoStreamMode.Read))
                {
                    csDecrypt.CopyTo(base64EIDOStream);
                }
            }

            return base64EIDOStream;
        }

        /// <summary>
        /// Encrypts a plain-text string
        /// </summary>
        /// <param name="encryptionKey">key to use for encryption</param>
        /// <param name="decryptedData">text to be encrypted</param>
        /// <returns>Encrypted data</returns>
        public static byte[] Encrypt(string encryptionKey, string decryptedData)
        {
            if (encryptionKey == null) throw new AuthorizationException();

            if (decryptedData == null) decryptedData = "";

            return Encrypt(encryptionKey, UTF8Encoding.UTF8.GetBytes(decryptedData));
        }

        /// <summary>
        /// Encrypts a byte array
        /// </summary>
        /// <param name="encryptionKey">key to use for encryption</param>
        /// <param name="decryptedData">text to be encrypted</param>
        /// <returns>Encrypted data</returns>
        public static byte[] Encrypt(string encryptionKey, byte[] decryptedData)
        {
            if (encryptionKey == null) throw new AuthorizationException();  // timeout occurred -> login again
            if (encryptionKey.Length != 32)
            {
                encryptionKey = GenerateEncryptionKey(encryptionKey);
            }

            if (decryptedData == null) decryptedData = new byte[0];

            byte[] encrypted;
            // Create a RijndaelManaged object 
            // with the specified key and IV. 
            using (RijndaelManaged rijAlg = new RijndaelManaged())
            {
                rijAlg.Key = ASCIIEncoding.ASCII.GetBytes(encryptionKey);
                rijAlg.IV = ASCIIEncoding.ASCII.GetBytes(encryptionKey.Substring(0, 16).ToUpper());
                rijAlg.Padding = PaddingMode.PKCS7;
                Array.Reverse(rijAlg.IV);

                // Create a decryptor to perform the stream transform.
                ICryptoTransform encryptor = rijAlg.CreateEncryptor(rijAlg.Key, rijAlg.IV);

                // Create the streams used for encryption. 
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (BinaryWriter swEncrypt = new BinaryWriter(csEncrypt))
                        {
                            //Write all data to the stream.
                            swEncrypt.Write(decryptedData);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }
            
            // Return the encrypted bytes from the memory stream. 
            return encrypted;
        }

        /// <summary>
        /// Generates a password that will be used for encrypting the data in the SQLite database
        /// The given invalidEncryptionKeyString contains a password which is not 32 characters long
        /// </summary>
        /// <param name="invalidEncryptionKeyString"></param>
        /// <returns></returns>
        private static string GenerateEncryptionKey(string invalidEncryptionKeyString)
        {
            byte[] invalidEncryptionKey = ASCIIEncoding.ASCII.GetBytes(invalidEncryptionKeyString);
            byte[] invalidEncryptionKeyReversed = ASCIIEncoding.ASCII.GetBytes(invalidEncryptionKeyString);
            Array.Reverse(invalidEncryptionKeyReversed);

            byte[] firstPart, lastPart;
            using (MD5 firstMD5 = MD5.Create())
            {
                firstPart = firstMD5.ComputeHash(invalidEncryptionKey);
            }

            using (MD5 lastMD5 = MD5.Create())
            {
                lastPart = lastMD5.ComputeHash(invalidEncryptionKeyReversed);
            }

            return ASCIIEncoding.ASCII.GetString(firstPart.Concat(lastPart).ToArray());
        }

        /// <summary>
        /// Generates a password that will be used for encrypting the data in the SQLite database which needs to be encrypted in EncryptionMode.Default
        /// </summary>
        /// <param name="accountDataUserName"></param>
        /// <param name="accountDataPassword"></param>
        /// <returns></returns>
        public static string GenerateEncryptionKey(string accountDataUserName, string accountDataPassword)
        {
            using (SHA256 sha256 = new SHA256Managed())
            {
                string accountDataUserNameLowerCase = accountDataUserName.ToLower();  // to make sure that the username is case insensitive

                // generate some string based on username and password
                string accountData = string.Format("{0}:{1}", accountDataPassword, accountDataUserNameLowerCase);
                for (int index = 0; index < 26 - accountDataUserNameLowerCase.Length; index++)
                {
                    accountData = (char)('A' + index) + accountData;
                }
                for (int index = 0; index < 26 - accountDataPassword.Length; index++)
                {
                    accountData = accountData + (char)('a' + index);
                }

                // encrypt string
                byte[] data = ASCIIEncoding.ASCII.GetBytes(accountData);
                data = sha256.ComputeHash(data);

                // make sure string contains only printable characters
                for (int index = 0; index < data.Length; index++)
                {
                    data[index] &= 127;
                    data[index] |= 32;

                    if (data[index] == 127) data[index] = 126;
                }

                return ASCIIEncoding.ASCII.GetString(data);
            }
        }

        /// <summary>
        /// Generates a password that will be used for encrypting the data in the SQLite database which needs to be encrypted in EncryptionMode.Private
        /// </summary>
        /// <param name="accountDataUserName"></param>
        /// <param name="accountDataPassword"></param>
        /// <returns></returns>
        public static string GeneratePrivateEncryptionKey(string accountDataUserName, string accountDataPassword)
        {
            using (SHA256 sha256 = new SHA256Managed())
            {
                string accountDataUserNameUpperCase = accountDataUserName.ToUpper();  // to make sure that the username is case insensitive

                // generate some string based on username and password
                string accountData = string.Format("{0}:{1}", accountDataUserNameUpperCase, accountDataPassword);
                for (int index = 0; index < 26 - accountDataUserNameUpperCase.Length; index++)
                {
                    accountData = (char)('z' - index) + accountData;
                }
                for (int index = 0; index < 26 - accountDataPassword.Length; index++)
                {
                    accountData = accountData + (char)('Z' - index);
                }

                // encrypt string
                byte[] data = ASCIIEncoding.ASCII.GetBytes(accountData);
                data = sha256.ComputeHash(data);

                // make sure string contains only printable characters
                for (int index = 0; index < data.Length; index++)
                {
                    data[index] &= 127;
                    data[index] |= 32;

                    if (data[index] == 127) data[index] = 126;
                }

                return ASCIIEncoding.ASCII.GetString(data);
            }
        }

        /// <summary>
        /// Generates a hash-key that will be used for validating the user and the password. This data is not stored in the SQLite database
        /// </summary>
        /// <param name="accountDataUserName"></param>
        /// <param name="accountDataPassword"></param>
        /// <returns></returns>
        public static string GenerateTokenKey(string accountDataUserName, string accountDataPassword)
        {
            using (SHA256 sha256 = new SHA256Managed())
            {
                string accountDataUserNameLowerCase = accountDataUserName.ToLower();  // to make sure that the username is case insensitive

                // generate some string based on username and password
                string accountData = string.Format("{0}:{1}", accountDataUserNameLowerCase, accountDataPassword);
                for (int index = 0; index < 26 - accountDataPassword.Length; index++)
                {
                    accountData = (char)('a' + index) + accountData;
                }
                for (int index = 0; index < 26 - accountDataPassword.Length; index++)
                {
                    accountData = accountData + (char)('A' + index);
                }

                // encrypt string
                byte[] data = ASCIIEncoding.ASCII.GetBytes(accountData);
                data = sha256.ComputeHash(data);

                // make sure string contains only printable characters
                for (int index = 0; index < data.Length; index++)
                {
                    data[index] &= 127;
                    data[index] |= 32;

                    if (data[index] == 127) data[index] = 126;
                }

                return ASCIIEncoding.ASCII.GetString(data);
            }
        }

        /// <summary>
        /// Decrypts a DPAPI encrypted byte array
        /// </summary>
        /// <param name="encryptedData">Encrypted byte array</param>
        /// <returns>Decrypted byte array</returns>
        public static byte[] DecryptDPAPI(byte[] encryptedData)
        {
            System.Security.Principal.WindowsImpersonationContext windowsImpersonationContext = null;

            try
            {
                // impersonate if needed
                IntPtr currentUserToken = ImpersonationHelper.GetCurrentUserToken();
                if (currentUserToken != IntPtr.Zero)
                {
                    using (WindowsIdentity currentUser = new WindowsIdentity(currentUserToken))
                    {
                        windowsImpersonationContext = currentUser.Impersonate();
                    }
                }

                // decrypt...
                return ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
            }
            finally
            {
                // un-impersonate if needed
                if (windowsImpersonationContext != null)
                {
                    // Undo impersonation
                    windowsImpersonationContext.Undo();

                    windowsImpersonationContext.Dispose();
                }
            }
        }

        /// <summary>
        /// Generates a DPAPI encrypted byte array
        /// </summary>
        /// <param name="unencryptedData">Unencrypted byte array</param>
        /// <returns>Encrypted byte array</returns>
        public static byte[] EncryptDPAPI(byte[] unencryptedData)
        {
            System.Security.Principal.WindowsImpersonationContext windowsImpersonationContext = null;

            try
            {
                // impersonate if needed
                IntPtr currentUserToken = ImpersonationHelper.GetCurrentUserToken();
                if (currentUserToken != IntPtr.Zero)
                {
                    using (WindowsIdentity currentUser = new WindowsIdentity(currentUserToken))
                    {
                        windowsImpersonationContext = currentUser.Impersonate();
                    }
                }

                // encrypt...
                return ProtectedData.Protect(unencryptedData, null, DataProtectionScope.CurrentUser);
            }
            finally
            {
                // un-impersonate if needed
                if (windowsImpersonationContext != null)
                {
                    // Undo impersonation
                    windowsImpersonationContext.Undo();

                    windowsImpersonationContext.Dispose();
                }
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
        public static UInt64[] GetXorValue(string decodedString, string encodedString)
        {
            if (decodedString == null)
            {
                return null;
            }

            UInt64[] result = new UInt64[4];

            for (int i = decodedString.Length - 1; i >= 0; i--)
            {
                int xorCharacter = decodedString[i] ^ encodedString[i];

                result[i / 8] <<= 8;
                result[i / 8] += (UInt64)xorCharacter;
            }

            return result;
        }

        /// <summary>
        /// This function will XOR a string with a 64-bit value and return the result.
        /// It is used to XOR the database encryption key, which is constructed by means of the username and password.
        /// In case the user wants to change his password, only the XOR value will be changed; the database encryption
        /// key remains the same
        /// </summary>
        /// <param name="decodedString">string to be encoded</param>
        /// <param name="xorValue">encryption key</param>
        /// <returns>XOR-encoded string</returns>
        public static string XorString(string decodedString, UInt64[] xorValue)
        {
            if (decodedString == null)
            {
                return null;
            }

            string encodedString = "";
            UInt64 xorIndex = 0x01;

            for (int c = 0; c < decodedString.Length && c < xorValue.Length * 8; c++)
            {
                char decodedChar = decodedString[c];
                encodedString += (char)(decodedChar ^ ((xorValue[c / 8] / xorIndex) & 0xFF));
                xorIndex <<= 8;
                if (xorIndex == 0)
                {
                    xorIndex = 1;
                }
            }

            return encodedString;
        }
    }
}
