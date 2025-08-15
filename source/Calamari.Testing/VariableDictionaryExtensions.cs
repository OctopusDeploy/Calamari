using System;
using System.IO;
using System.Linq;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Variables;
using Octostache;

namespace Calamari.Testing
{
    public static class VariableDictionaryExtensions
    {
        public static string SaveAsEncryptedExecutionVariables(this VariableDictionary variable, string filePath)
        {
            var encryptionKey = AesEncryption.RandomString(10);
            SaveAsEncryptedExecutionVariables(variable, filePath, encryptionKey);
            return encryptionKey;
        }

        public static void SaveAsEncryptedExecutionVariables(this VariableDictionary variable, string filePath, string encryptionKey)
        {
            var collection = new CalamariExecutionVariableCollection();
            collection.AddRange(variable.Select(kvp => new CalamariExecutionVariable(kvp.Key, kvp.Value, false /* Assume all are non-sensitive */)));
            
            var encryptedContent = AesEncryption.ForServerVariables(encryptionKey).Encrypt(collection.ToJsonString());
            File.WriteAllBytes(filePath, encryptedContent);
        }

        public static string SaveAsEncryptedExecutionVariables(this IVariables variable, string filePath)
        {
            var encryptionKey = AesEncryption.RandomString(10);
            SaveAsEncryptedExecutionVariables(variable, filePath, encryptionKey);
            return encryptionKey;
        }

        public static void SaveAsEncryptedExecutionVariables(this IVariables variable, string filePath, string encryptionKey)
        {
            var collection = new CalamariExecutionVariableCollection();
            collection.AddRange(variable.Select(kvp => new CalamariExecutionVariable(kvp.Key, kvp.Value, false /* Assume all are non-sensitive */)));
            
            var encryptedContent = AesEncryption.ForServerVariables(encryptionKey).Encrypt(collection.ToJsonString());
            File.WriteAllBytes(filePath, encryptedContent);
        }
    }
}
