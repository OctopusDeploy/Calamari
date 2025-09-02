using System;
using System.Linq;
using System.Security.Cryptography;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Newtonsoft.Json;

namespace Calamari.Common.Plumbing.Variables
{
    public interface ICustomPropertiesFactory
    {
        T Create<T>(string customPropertiesFile, string password);
    }

    public class CustomPropertiesFactory : ICustomPropertiesFactory
    {
        readonly ICalamariFileSystem fileSystem;

        static readonly object LoaderLock = new object();

        public CustomPropertiesFactory(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public T Create<T>(string customPropertiesFile, string password)
        {
            lock (LoaderLock)
            {
                return LoadExecutionVariablesFromFile<T>(customPropertiesFile, password);
            }
        }

        T LoadExecutionVariablesFromFile<T>(string customPropertiesFile, string password)
        {
            var json = Decrypt(fileSystem.ReadAllBytes(customPropertiesFile), password);

            try
            {
                return JsonConvert.DeserializeObject<T>(json, SerializerSettings);
            }
            catch (JsonReaderException)
            {
                throw new CommandException("Unable to parse variables as valid JSON.");
            }
        }

        static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            DateParseHandling = DateParseHandling.None
        };

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