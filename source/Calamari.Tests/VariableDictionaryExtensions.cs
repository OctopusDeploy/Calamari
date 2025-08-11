using System.IO;
using System.Linq;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Variables;
using Calamari.Util;
using Octostache;

namespace Calamari.Tests
{
    public static class VariableDictionaryExtensions
    {
        public static void SaveAsEncryptedExecutionVariables(this VariableDictionary variable, string encryptionKey, string filePath)
        {
            var collection = new CalamariExecutionVariableCollection();
            collection.AddRange(variable.Select(kvp => new CalamariExecutionVariable(kvp.Key, kvp.Value, false /* Assume all are non-sensitive */)));
            
            var encryptedContent = AesEncryption.ForServerVariables(encryptionKey).Encrypt(collection.ToJsonString());
            File.WriteAllBytes(filePath, encryptedContent);
        }
    }
}
