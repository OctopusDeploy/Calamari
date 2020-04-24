using System.IO;
using Calamari.Util;
using Octostache;

namespace Calamari.Tests.Shared.Helpers
{
    public static class VariableDictionaryExtensions
    {
        public static void SaveEncrypted(this VariableDictionary variables, string key, string file)
        {
            var encryptedContent = new AesEncryption(key).Encrypt(variables.SaveAsString());
            File.WriteAllBytes(file, encryptedContent);
        }
    }
}
