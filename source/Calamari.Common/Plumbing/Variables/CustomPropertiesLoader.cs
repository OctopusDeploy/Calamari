using System;
using System.Security.Cryptography;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Newtonsoft.Json;

namespace Calamari.Common.Plumbing.Variables
{
    public interface ICustomPropertiesLoader
    {
        T Load<T>();
    }

    public class CustomPropertiesLoader : ICustomPropertiesLoader
    {
        readonly ICalamariFileSystem fileSystem;
        readonly string customPropertiesFile;
        readonly string password;
        readonly JsonSerializerSettings serializerSettings;

        public CustomPropertiesLoader(ICalamariFileSystem fileSystem, string customPropertiesFile, string password, params JsonConverter[] converters)
        {
            this.fileSystem = fileSystem;
            this.customPropertiesFile = customPropertiesFile;
            this.password = password;

            serializerSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                DateParseHandling = DateParseHandling.None,
            };
            foreach (var converter in converters)
            {
                serializerSettings.Converters.Add(converter);
            }
        }

        public T Load<T>()
        {
            var json = Decrypt(fileSystem.ReadAllBytes(customPropertiesFile), password);

            try
            {
                return JsonConvert.DeserializeObject<T>(json, serializerSettings);
            }
            catch (JsonReaderException)
            {
                throw new CommandException("Unable to parse custom properties as valid JSON.");
            }
        }

        static string Decrypt(byte[] encryptedJson, string encryptionPassword)
        {
            try
            {
                return AesEncryption.ForServerVariables(encryptionPassword).Decrypt(encryptedJson);
            }
            catch (CryptographicException)
            {
                throw new CommandException("Cannot decrypt custom properties. Check your password is correct.");
            }
        }
    }
}