using System;
using System.Collections.Generic;
using System.Linq;
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
        public CalamariVariableDictionary() { }

        public CalamariVariableDictionary(string storageFilePath) : base(storageFilePath) { }

        public CalamariVariableDictionary(string storageFilePath, List<string> sensitiveFilePaths, string sensitiveFilePassword, string outputVariablesFilePath = null, string outputVariablesFilePassword = null)
        {
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            if (!string.IsNullOrEmpty(storageFilePath))
            {
                if (!fileSystem.FileExists(storageFilePath))
                    throw new CommandException("Could not find variables file: " + storageFilePath);

                var nonSensitiveVariables =  new VariableDictionary(storageFilePath);
                nonSensitiveVariables.GetNames().ForEach(name => Set(name, nonSensitiveVariables.GetRaw(name)));
            }

            if (sensitiveFilePaths.Any())
            {
                foreach (var sensitiveFilePath in sensitiveFilePaths)
                {
                    if (string.IsNullOrEmpty(sensitiveFilePath)) continue;

                    var rawVariables = string.IsNullOrWhiteSpace(sensitiveFilePassword)
                        ? fileSystem.ReadFile(sensitiveFilePath)
                        : Decrypt(fileSystem.ReadAllBytes(sensitiveFilePath), sensitiveFilePassword);

                    try
                    {
                        var sensitiveVariables = JsonConvert.DeserializeObject<Dictionary<string, string>>(rawVariables);
                        foreach (var variable in sensitiveVariables)
                        {
                            Set(variable.Key, variable.Value);
                        }
                    }
                    catch (JsonReaderException)
                    {
                        throw new CommandException("Unable to parse sensitive-variables as valid JSON.");
                    }
                }
            }

            if (!string.IsNullOrEmpty(outputVariablesFilePath))
            {
                var rawVariables = DecryptWithMachineKey(fileSystem.ReadFile(outputVariablesFilePath), outputVariablesFilePassword);
                try
                {
                    var outputVariables = JsonConvert.DeserializeObject<Dictionary<string, string>>(rawVariables);
                    foreach (var variable in outputVariables)
                    {
                        Set(variable.Key, variable.Value);
                    }
                }
                catch (JsonReaderException)
                {
                    throw new CommandException("Unable to parse output variables as valid JSON.");
                }
            }
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

        static string DecryptWithMachineKey(string base64EncodedEncryptedVariables, string password)
        {
            try
            {
                var encryptedVariables = Convert.FromBase64String(base64EncodedEncryptedVariables);
                var bytes = ProtectedData.Unprotect(encryptedVariables, Convert.FromBase64String(password ?? string.Empty), DataProtectionScope.CurrentUser);
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