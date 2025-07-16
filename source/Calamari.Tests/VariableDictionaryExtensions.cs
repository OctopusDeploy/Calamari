using System.IO;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Util;
using Octostache;

namespace Calamari.Tests
{
    public static class VariableDictionaryExtensions
    {
        public static void SaveEncrypted(this VariableDictionary variables, string key, string file)
        {
            var encryptedContent = AesEncryption.ForServerVariables(key).Encrypt(variables.SaveAsString());
            File.WriteAllBytes(file, encryptedContent);
        }
    }
}
