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
            
            byte[] firstPart = MD5.Create().ComputeHash(invalidEncryptionKey);
            byte[] lastPart = MD5.Create().ComputeHash(invalidEncryptionKeyReversed);

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
            SHA256 sha256 = new SHA256Managed();
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
        
        /// <summary>
        /// Generates a password that will be used for encrypting the data in the SQLite database which needs to be encrypted in EncryptionMode.Private
        /// </summary>
        /// <param name="accountDataUserName"></param>
        /// <param name="accountDataPassword"></param>
        /// <returns></returns>
        public static string GeneratePrivateEncryptionKey(string accountDataUserName, string accountDataPassword)
        {
            SHA256 sha256 = new SHA256Managed();
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

        /// <summary>
        /// Generates a hash-key that will be used for validating the user and the password. This data is not stored in the SQLite database
        /// </summary>
        /// <param name="accountDataUserName"></param>
        /// <param name="accountDataPassword"></param>
        /// <returns></returns>
        public static string GenerateTokenKey(string accountDataUserName, string accountDataPassword)
        {
            SHA256 sha256 = new SHA256Managed();
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

        /// <summary>
        /// Generates a DPAPI encrypted string based on the current computer name and user name
        /// </summary>
        /// <returns>DPAPI encrypted string which can be used as encryption/decyption key</returns>
        public static string GetDPAPIKey()
        {
            System.Security.Principal.WindowsImpersonationContext windowsImpersonationContext = null;

            try
            {
                // impersonate if needed
                IntPtr currentUserToken = ImpersonationHelper.GetCurrentUserToken();
                if (currentUserToken != IntPtr.Zero)
                {
                    WindowsIdentity currentUser = new WindowsIdentity(currentUserToken);
                    windowsImpersonationContext = currentUser.Impersonate();
                }

                // calculate DPAPI key
                string dpapiEncryptionKey = Environment.MachineName + "|" + Environment.UserName.ToUpperInvariant() + "abcdefghijklmnopqrstuvwxyz789012";
                dpapiEncryptionKey = dpapiEncryptionKey.Substring(0, 32 * (dpapiEncryptionKey.Length / 32));  // length of DPAPI encryption key should be 32
                byte[] data = UTF8Encoding.UTF8.GetBytes(dpapiEncryptionKey);
                byte[] encryptedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser)
                                                    .Concat(ASCIIEncoding.ASCII.GetBytes("abcdefghijklmnopqrstuvwxyz789012"))
                                                    .ToArray();

                // strip down DPAPI key to 32 bytes
                for (uint c = 0; c < 32; c++)
                {
                    byte encryptedByte = encryptedData[c + (encryptedData.Length / 32) + 4];
                    if (encryptedByte == 0) encryptedByte += (byte)((c+5)*3);
                    encryptedData[c] = encryptedByte;
                }
                encryptedData = encryptedData.Take(32).ToArray();

                // make sure string contains only printable characters
                for (int index = 0; index < encryptedData.Length; index++)
                {
                    encryptedData[index] &= 127;
                    encryptedData[index] |= 32;

                    if (encryptedData[index] == 127) encryptedData[index] = 126;
                }

                return UTF8Encoding.UTF8.GetString(encryptedData);
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

            for (int c = 0; c < decodedString.Length; c++)
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
