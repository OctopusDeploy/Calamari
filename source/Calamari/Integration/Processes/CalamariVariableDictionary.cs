using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Util;
using Newtonsoft.Json;
using Octostache;

namespace Calamari.Integration.Processes
{
    public class CalamariVariableDictionary : VariableDictionary
    {
        protected HashSet<string> SensitiveVariableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public CalamariVariableDictionary() { }

        public CalamariVariableDictionary(string storageFilePath) : base(storageFilePath) { }

        public CalamariVariableDictionary(string storageFilePath, string sensitiveFilePath, string sensitiveFilePassword)
        {
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            if (!string.IsNullOrEmpty(storageFilePath))
            {
                if (!fileSystem.FileExists(storageFilePath))
                    throw new CommandException("Could not find variables file: " + storageFilePath);

                var nonSensitiveVariables =  new VariableDictionary(storageFilePath);
                nonSensitiveVariables.GetNames().ForEach(name => Set(name, nonSensitiveVariables.GetRaw(name)));
            }

            if (!string.IsNullOrEmpty(sensitiveFilePath))
            {
                var rawVariables = string.IsNullOrWhiteSpace(sensitiveFilePassword)
                    ? fileSystem.ReadFile(sensitiveFilePath)
                    : Decrypt(fileSystem.ReadAllBytes(sensitiveFilePath), sensitiveFilePassword);


                try
                {
                    var sensitiveVariables = JsonConvert.DeserializeObject<Dictionary<string, string>>(rawVariables);
                    foreach (var variable in sensitiveVariables)
                    {
                        SetSensitive(variable.Key, variable.Value);
                    }
                }
                catch (JsonReaderException)
                {
                    throw new CommandException("Unable to parse sensitive-variables as valid JSON.");
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

        static string Decrypt(byte[] encryptedVariables, string encryptionPassword)
        {
            try
            {
                return new AesEncryption(encryptionPassword).Decrypt(encryptedVariables);
            }
            catch (CryptographicException)
            {
                throw new CommandException("Cannot decrypt sensitive-variables. Check your password is correct.");
            }
        }

        public string GetEvironmentExpandedPath(string variableName, string defaultValue = null)
        {
            return CrossPlatform.ExpandPathEnvironmentVariables(Get(variableName, defaultValue));
        }

        public bool IsSet(string name)
        {
            return this[name] != null;
        }
    }
}