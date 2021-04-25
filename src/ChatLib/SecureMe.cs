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

        public byte[] AesIV
        {
            get
            {
                return aes.IV;
            }
        }

        public byte[] AesKey
        {
            get
            {
                return aes.Key;
            }
        }

        public string PublicKey
        {
            get
            {
                return rsa.ToXmlString(false);
            }
        }

        public byte[] Encrypt(string publicKey, byte[] message)
        {
            var localRsa = RSA.Create();
            localRsa.FromXmlString(publicKey);
            return localRsa.Encrypt(message, RSAEncryptionPadding.Pkcs1);
        }

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

    public class EncryptedMessage
    {
        public byte[] DataContent;
        public byte[] AesIV;
        public byte[] AesKey;
    }
}
