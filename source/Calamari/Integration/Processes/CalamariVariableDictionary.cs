using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Newtonsoft.Json;
using Octostache;

namespace Calamari.Integration.Processes
{
    public class CalamariVariableDictionary : VariableDictionary
    {
        // Changing these values will be break decryption
        const int PasswordSaltIterations = 1000;
        static readonly byte[] PasswordPaddingSalt = Encoding.UTF8.GetBytes("Octopuss");

        protected List<string> SensitiveVariableNames = new List<string>();

        public CalamariVariableDictionary() { }

        public CalamariVariableDictionary(string storageFilePath) : base(storageFilePath) { }

        public CalamariVariableDictionary(string storageFilePath, string sensitiveFilePath, string sensitiveFilePassword, string sensitiveFileSalt)
        {
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            if (!string.IsNullOrEmpty(storageFilePath))
            {
                if (!fileSystem.FileExists(storageFilePath))
                    throw new CommandException("Could not find variables file: " + storageFilePath);

                Log.Info("Using variables from: " + storageFilePath);
                var nonSensitiveVariables =  new VariableDictionary(storageFilePath);
                nonSensitiveVariables.GetNames().ForEach(name => Set(name, nonSensitiveVariables[name]));
            }

            if (!string.IsNullOrEmpty(sensitiveFilePath))
            {
                if (!fileSystem.FileExists(sensitiveFilePath))
                    throw new CommandException("Could not find variables file: " + sensitiveFilePath);

                if (string.IsNullOrWhiteSpace(sensitiveFilePassword))
                    throw new CommandException("sensitiveVariablesPassword option must be supplied if sensitiveVariables option is supplied.");

                if (string.IsNullOrWhiteSpace(sensitiveFileSalt))
                    throw new CommandException("sensitiveVariablesSalt option must be supplied if sensitiveVariables option is supplied.");

                Log.Info("Using sensitive variables from: " + sensitiveFilePath);

                var sensitiveVariables = Decrypt(fileSystem.ReadFile(sensitiveFilePath), sensitiveFilePassword, sensitiveFileSalt);
                foreach (var variable in sensitiveVariables)
                {
                    SetSensitive(variable.Key, variable.Value);
                }
            }
        }

        public void SetSensitive(string name, string value)
        {
            if (name == null) return;
            Set(name, value);
            SensitiveVariableNames.Add(name);
        }

        public bool IsSensitive(string name)
        {
            return name != null && SensitiveVariableNames.Contains(name);
        }


        static Dictionary<string, string> Decrypt(string cipherText, string encryptionPassword, string salt)
        {
            using (var algorithm = new AesCryptoServiceProvider
            {
                Key = GetEncryptionKey(encryptionPassword),
                IV = Convert.FromBase64String(salt)
            })
            using (var decryptor = algorithm.CreateDecryptor())
            using (var decryptedTextStream = new MemoryStream())
            using (var stringReader = new StreamReader(decryptedTextStream, Encoding.UTF8))
            using (var jsonReader = new JsonTextReader(stringReader))
            {
                try
                {
                    using (var cryptoStream = new CryptoStream(decryptedTextStream, decryptor, CryptoStreamMode.Write))
                    {
                        var cipherTextBytes = Convert.FromBase64String(cipherText);
                        cryptoStream.Write(cipherTextBytes, 0, cipherTextBytes.Length);
                        cryptoStream.FlushFinalBlock();

                        var dictionary = new Dictionary<string, string>();
                        var serializer = new JsonSerializer();
                        decryptedTextStream.Position = 0;
                        serializer.Populate(jsonReader, dictionary);
                        return dictionary;
                    }
                }
                catch (CryptographicException cryptoException)
                {
                    throw new CommandException(
                        "Cannot decrypt sensitive-variables. Check your password is correct.\nError message: " +
                        cryptoException.Message);
                }
            }
        }

        public static byte[] GetEncryptionKey(string encryptionPassword)
        {
            var passwordGenerator = new Rfc2898DeriveBytes(encryptionPassword, PasswordPaddingSalt, PasswordSaltIterations);
            return passwordGenerator.GetBytes(16);
        }
    }
}