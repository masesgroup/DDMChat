/*
* MIT License
* 
* Copyright(c) 2021 S4I s.r.l. (a MASES Group company)
* www.s4i.it www.masesgroup.com
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*/

using System.Text;
using System.Security.Cryptography;
using System.IO;

namespace MASES.S4I.ChatLib
{
    public class SecureMe
    {
        RSA rsa = RSA.Create(Constants.keySize);
        AesManaged aes = new AesManaged();

        /// <summary>
        /// Constructor
        /// Verify the existence of a private key or create a new one
        /// </summary>
        public SecureMe()
        {
            //aes.Padding = PaddingMode.PKCS7;
            if (File.Exists(Constants.privateKeyFile))
            {
                string parameters = File.ReadAllText(Constants.privateKeyFile);
                rsa.FromXmlString(parameters);
            }
            else
            {
                File.WriteAllText(Constants.privateKeyFile, rsa.ToXmlString(true));
            }
        }

        /// <summary>
        /// Return the AES IV parameter in unse
        /// </summary>
        public byte[] AesIV
        {
            get
            {
                return aes.IV;
            }
        }

        /// <summary>
        /// Return the AES Key parameter in unse
        /// </summary>
        public byte[] AesKey
        {
            get
            {
                return aes.Key;
            }
        }

        /// <summary>
        /// Return the public key in use
        /// </summary>
        public string PublicKey
        {
            get
            {
                return rsa.ToXmlString(false);
            }
        }

        /// <summary>
        /// Encript a message using the passed public key
        /// </summary>
        /// <param name="publicKey">public key to be used to encrypt</param>
        /// <param name="message">message to be encrypted</param>
        /// <returns>The encrypted message</returns>
        public byte[] Encrypt(string publicKey, byte[] message)
        {
            var localRsa = RSA.Create();
            localRsa.FromXmlString(publicKey);
            return localRsa.Encrypt(message, RSAEncryptionPadding.Pkcs1);
        }

        /// <summary>
        /// Decrypt a message using the stored private key
        /// </summary>
        /// <param name="message">encrypted message to be decrypted</param>
        /// <returns>the decrypted message</returns>
        public byte[] Decrypt(byte[] message)
        {
            byte[] res = null;
            try
            {
                res = rsa.Decrypt(message, RSAEncryptionPadding.Pkcs1);
            }
            catch { };
            return res;
        }

        /// <summary>
        /// Encrypt a message using the passed public key
        /// the public key is used to encrypt the AES keys
        /// newly generated AES keys are used every time
        /// the message is crypted using AES
        /// </summary>
        /// <param name="publicKey">the public key used to encrypt AES keys</param>
        /// <param name="message">the message to be encrypted with AES</param>
        /// <returns>An <see cref="EncryptedMessage"/> 
        /// containig the encrypted AES keys and the message encrypted with these keys</returns>
        public EncryptedMessage EncryptMessage(string publicKey, byte[] message)
        {
            byte[] encrypted;
            using (MemoryStream msEncrypt = new MemoryStream())
            {
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        //Write all data to the stream.
                        swEncrypt.Write(Encoding.ASCII.GetString(message));
                    }
                    encrypted = msEncrypt.ToArray();
                }
            }

            return new EncryptedMessage()
            {
                AesIV = Encrypt(publicKey, aes.IV),
                AesKey = Encrypt(publicKey, aes.Key),
                DataContent = encrypted,
            };
        }

        /// <summary>
        /// Decrypta the message contained in an <see cref="EncryptedMessage"/> instance
        /// </summary>
        /// <param name="message">the <see cref="EncryptedMessage"/> message to be decrypted</param>
        /// <returns></returns>
        public byte[] DecryptMessage(EncryptedMessage message)
        {

            byte[] aesIV = Decrypt(message.AesIV);
            byte[] aesKey = Decrypt(message.AesKey);
            if (aesIV == null || aesKey == null) return null;
            string plaintext;
            // Create the streams used for decryption.
            using (MemoryStream msDecrypt = new MemoryStream(message.DataContent))
            {
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, aes.CreateDecryptor(aesKey, aesIV), CryptoStreamMode.Read))
                {
                    using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                    {

                        // Read the decrypted bytes from the decrypting stream
                        // and place them in a string.
                        plaintext = srDecrypt.ReadToEnd();
                    }
                }
            }

            return Encoding.ASCII.GetBytes(plaintext);
        }

        internal byte[] Sign(string jsonMessage)
        {
            byte[] data = Encoding.ASCII.GetBytes(jsonMessage);
            return rsa.SignData(data, HashAlgorithmName.MD5, RSASignaturePadding.Pkcs1);

        }

        internal bool Verify(string jsonMessage, byte[] signature, string publicKey)
        {
            var localRsa = RSA.Create();
            localRsa.FromXmlString(publicKey);
            byte[] data = Encoding.ASCII.GetBytes(jsonMessage);
            return localRsa.VerifyData(data, signature, HashAlgorithmName.MD5, RSASignaturePadding.Pkcs1);
        }
    }

    /// <summary>
    /// Class to contain the encrypted messages
    /// </summary>
    public class EncryptedMessage
    {
        public byte[] DataContent;
        public byte[] AesIV;
        public byte[] AesKey;
    }
}
