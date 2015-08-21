using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Calamari.Util
{
    public interface IScriptVariableEncryptor
    {
        string Encrypt(string text);

        string Decrypt(string text);
    }

    public class ScriptVariableEncryptor : IScriptVariableEncryptor
    {
        byte[] InitializationVector { get; }
        byte[] Key { get; }

        public ScriptVariableEncryptor(string passphrase)
        {
            var salt = Encoding.UTF8.GetBytes("SaltCrypto");
            var vector = Encoding.UTF8.GetBytes("IV_Password");
            var pass = Encoding.UTF8.GetBytes(passphrase);

            // Passwords are one time use so not concerned about salting
            InitializationVector = (new SHA1Managed()).ComputeHash(vector).Take(16).ToArray();
            Key = new PasswordDeriveBytes(pass, salt, "SHA1", 5).GetBytes(32);
        }

        public string Encrypt(string text)
        {
            using (var r = new RijndaelManaged() {Key = Key, IV = InitializationVector})
            using (var cryptoTransform = r.CreateEncryptor())
            using (var ms = new MemoryStream())
            {
                using (var cs = new CryptoStream(ms, cryptoTransform, CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs))
                {
                    sw.Write(text);
                }
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        public string Decrypt(string encryptedText)
        {
            var textBytes = Convert.FromBase64String(encryptedText);
            using (var r = new RijndaelManaged() {Key = Key, IV = InitializationVector})
            using (var dec = r.CreateDecryptor())
            using (var ms = new MemoryStream(textBytes))
            using (var cs = new CryptoStream(ms, dec, CryptoStreamMode.Read))
            using (var sw = new StreamReader(cs))
            {
                return sw.ReadToEnd();
            }
        }
    }
}
