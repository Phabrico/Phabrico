using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Phabrico.Miscellaneous
{
    /// <summary>
    /// Encrypt/Decrypt class based on the Vigenère cipher.
    /// This encrypter is used for encrypting the keywordInfo data, which needs to be LIKE'ed in SELECT SQL statements.
    /// For example:
    ///   Encrypted('Hello') LIKE Encrypted('Hell')%
    ///   
    ///   => Encrypted('Hell') should have the same starting characters as Encrypted('Hello')
    /// </summary>
    public class PolyCharacterCipherEncryption
    {
        /// <summary>
        /// Translation table
        /// </summary>
        private Dictionary<byte, Dictionary<byte, byte>> tabulaRecta = new Dictionary<byte, Dictionary<byte, byte>>();

        /// <summary>
        /// The encrypted encryption/decryption key to be used
        /// </summary>
        private byte[] key;

        /// <summary>
        /// Initializes a new instance of the PolyCharacterCipherEncryption
        /// </summary>
        /// <param name="encryptionKey">Encryption/decryption key to be used</param>
        public PolyCharacterCipherEncryption(string encryptionKey)
        {
            for (int b1 = 0; b1 < 256; b1++)
            {
                tabulaRecta[(byte)b1] = new Dictionary<byte, byte>();

                for (int b2 = 0; b2 < 256; b2++)
                {
                    tabulaRecta[(byte)b1][(byte)b2] = (byte)(b1 + b2);
                }
            }

            // encrypt the key
            key = new byte[encryptionKey.Length * 2];
            for (int c = 0; c < encryptionKey.Length; c++)
            {
                byte keyByte = (byte)encryptionKey[c];
                key[c] = keyByte;
            }
            for (int c = encryptionKey.Length - 1; c >= 0; c--)
            {
                byte keyByte = (byte)encryptionKey[c];
                key[2 * encryptionKey.Length - c - 1] = keyByte;
                key[2 * encryptionKey.Length - c - 1] ^= key[c / 2];
            }

            // determine length of key based on content of key
            key = key.Take(32 + key.Sum(b => (int)b) % 32).ToArray();
        }

        /// <summary>
        /// Decrypts some data
        /// </summary>
        /// <param name="encryptedString">Data to be decrypted</param>
        /// <returns>Decrypted data</returns>
        public byte[] Decrypt(string encryptedString)
        {
            List<byte> result = new List<byte>();
            int k = 0;

            for (int e = 0; e < encryptedString.Length; e += 2)
            {
                byte encryptedByte = byte.Parse(encryptedString.Substring(e, 2), NumberStyles.HexNumber);

                result.Add(tabulaRecta[key[k]].FirstOrDefault(column => column.Value == encryptedByte).Key);
                k++;

                if (k >= key.Length)
                {
                    k = 0;
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Encrypts some data
        /// </summary>
        /// <param name="decryptedData">Data to be encrypted</param>
        /// <returns>Encrypted string</returns>
        public string Encrypt(byte[] decryptedData)
        {
            List<byte> encryptedBytes = new List<byte>();
            int k = 0;

            for (int d = 0; d < decryptedData.Length; d++)
            {
                encryptedBytes.Add(tabulaRecta[decryptedData[d]][key[k]]);
                k++;

                if (k >= key.Length)
                {
                    k = 0;
                }
            }

            string result = "";
            foreach (byte encryptedByte in encryptedBytes)
            {
                result += string.Format("{0:x2}", encryptedByte);
            }

            return result;
        }
    }
}
