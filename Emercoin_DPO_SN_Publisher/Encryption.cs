namespace EmercoinDPOSNP
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

    public class Encryption
    {
        private static byte[] key = new byte[] { 0x12, 0xA1, 0x13, 0xC6, 0x84, 0x50, 0xAB, 0x23, 0x62, 0x1B, 0xC3, 0xFF, 0x11, 0x07, 0x15, 0x13 };
        private static byte[] iv = new byte[] { 0x11, 0x10, 0x05, 0x33, 0x14, 0x30, 0x07, 0x1D, 0x20, 0xCB, 0xA3, 0x61, 0x44, 0x03, 0xA5, 0x53 };

        internal static string EncryptString_Aes(string plainText)
        {
            byte[] rawPlaintextArr = System.Text.Encoding.Unicode.GetBytes(plainText);

            using (Aes aes = new AesManaged())
            {
                aes.Padding = PaddingMode.PKCS7;
                aes.KeySize = 128;          // in bits
                aes.Key = key; // 16 bytes for 128 bit encryption
                aes.IV = iv; // AES needs a 16-byte IV
                // Should set Key and IV here.  Good approach: derive them from 
                // a password via Cryptography.Rfc2898DeriveBytes 
                byte[] cipherArr = null;

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(rawPlaintextArr, 0, rawPlaintextArr.Length);
                    }

                    cipherArr = ms.ToArray();
                }
                string cipherText = System.Convert.ToBase64String(cipherArr);
                return cipherText;
            }
        }

        internal static string DecryptString_Aes(string cipherText)
        {
            byte[] cipherArr = System.Convert.FromBase64String(cipherText);
            byte[] plainArr = null;

            using (Aes aes = new AesManaged())
            {
                aes.Padding = PaddingMode.PKCS7;
                aes.KeySize = 128;          // in bits
                aes.Key = key; // 16 bytes for 128 bit encryption
                aes.IV = iv; // AES needs a 16-byte IV
                // Should set Key and IV here.  Good approach: derive them from 
                // a password via Cryptography.Rfc2898DeriveBytes 

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(cipherArr, 0, cipherArr.Length);
                    }

                    plainArr = ms.ToArray();
                }
                string s = System.Text.Encoding.Unicode.GetString(plainArr);
                return s;
            }
        }
    }
}
