using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Calamari.Integration.FileSystem;
using Newtonsoft.Json;
using Octostache;

namespace Calamari.Deployment
{
    public class SensitiveVariables
    {
        // Changing these values will be break decryption
        const int PasswordSaltIterations = 1000;
        static readonly byte[] PasswordPaddingSalt = Encoding.UTF8.GetBytes("Octopuss");

        readonly ICalamariFileSystem fileSystem;

        public SensitiveVariables(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public VariableDictionary IncludeSensitiveVariables(string variablesFile, string encryptionPassword, string salt)
        {
            var variables = new VariableDictionary(variablesFile); 

            var sensitiveVariablesFile = Path.ChangeExtension(variablesFile, "secret");

            if (!fileSystem.FileExists(sensitiveVariablesFile))
            {
                Log.VerboseFormat("No sensitive-variables file was found. Looked for '{0}'", sensitiveVariablesFile);
                return variables;
            }

            var decryptedVariables = CreateDictionary(
                Decrypt(fileSystem.ReadFile(sensitiveVariablesFile), encryptionPassword, salt));

            AddVariables(variables, decryptedVariables);

            Log.VerboseFormat("Decrypted sensitive-variables from '{0}'", sensitiveVariablesFile);
            return variables;
        }

        static void AddVariables(VariableDictionary variableDictionary, IDictionary<string, string> items)
        {
            foreach (var variableName in items.Keys)
            {
               variableDictionary.Set(variableName, items[variableName]); 
            }
        }

        static string Decrypt(string cipherText, string encryptionPassword, string salt)
        {
            using (var algorithm = new AesCryptoServiceProvider{ Key =GetEncryptionKey(encryptionPassword), IV = Convert.FromBase64String(salt)})
            using (var decryptor = algorithm.CreateDecryptor())
            using (var decryptedTextStream = new MemoryStream())
            {
                using (var cryptoStream = new CryptoStream(decryptedTextStream, decryptor, CryptoStreamMode.Write))
                {
                    var cipherTextBytes = Convert.FromBase64String(cipherText);
                    cryptoStream.Write(cipherTextBytes, 0, cipherTextBytes.Length);
                }

                return Encoding.UTF8.GetString(decryptedTextStream.ToArray());
            }
        }

        public static byte[] GetEncryptionKey(string encryptionPassword)
        {
            var passwordGenerator = new Rfc2898DeriveBytes(encryptionPassword, PasswordPaddingSalt, PasswordSaltIterations);
            return passwordGenerator.GetBytes(16);
        }

        static Dictionary<string, string> CreateDictionary(string json)
        {
            var dictionary = new Dictionary<string, string>();

           using (var stringReader = new StringReader(json))
           using (var jsonReader = new JsonTextReader(stringReader))
           {
               var serializer = new JsonSerializer();
               serializer.Populate(jsonReader, dictionary);
               return dictionary;
           }
        }
    }
}