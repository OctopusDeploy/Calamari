using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
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

        static readonly object LoaderLock = new object();

        public CustomPropertiesLoader(ICalamariFileSystem fileSystem, string customPropertiesFile, string password)
        {
            this.fileSystem = fileSystem;
            this.customPropertiesFile = customPropertiesFile;
            this.password = password;
        }

        public T Load<T>()
        {
            lock (LoaderLock)
            {
                return LoadFromFile<T>();
            }
        }

        T LoadFromFile<T>()
        {
            var json = Decrypt(fileSystem.ReadAllBytes(customPropertiesFile), password);

            try
            {
                return JsonConvert.DeserializeObject<T>(json, SerializerSettings);
            }
            catch (JsonReaderException)
            {
                throw new CommandException("Unable to parse custom properties as valid JSON.");
            }
        }

        static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            DateParseHandling = DateParseHandling.None,
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