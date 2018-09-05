using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
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

        public CalamariVariableDictionary(string storageFilePath, string sensitiveFilePath, string sensitiveFilePassword, string outputVariablesFilePath = null)
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

            if (!string.IsNullOrEmpty(outputVariablesFilePath))
            {
                var rawVariables = DecryptWithMachineKey(fileSystem.ReadFile(outputVariablesFilePath));
                try
                {
                    var outputVariables = JsonConvert.DeserializeObject<Dictionary<string, string>>(rawVariables);
                    foreach (var variable in outputVariables)
                    {
                        Set(variable.Key, variable.Value);
                    }

                }
                catch (JsonReaderException e)
                {
                    throw new CommandException("Unable to parse output variables as valid JSON.");
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

        static string DecryptWithMachineKey(string base64EncodedEncryptedVariables)
        {
            try
            {
                var encryptedVariables = Convert.FromBase64String(base64EncodedEncryptedVariables);
                var bytes = ProtectedData.Unprotect(encryptedVariables, null, DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (CryptographicException)
            {
                throw new CommandException("Cannot decrypt output variables.");
            }
        }

        public string GetEnvironmentExpandedPath(string variableName, string defaultValue = null)
        {
            return CrossPlatform.ExpandPathEnvironmentVariables(Get(variableName, defaultValue));
        }

        public bool IsSet(string name)
        {
            return this[name] != null;
        }
    }
}