using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Util;
using Newtonsoft.Json;
using Octostache;

namespace Calamari.Variables
{
    public class VariablesFactory
    {
        public static IVariables Create(ICalamariFileSystem fileSystem, CommonOptions options)
        {
            var variables = new CalamariVariables();
            
            var variablesFile = options.InputVariables.VariablesFile;
            if (!string.IsNullOrEmpty(variablesFile))
            {
                if (!fileSystem.FileExists(variablesFile))
                    throw new CommandException("Could not find variables file: " + variablesFile);

                var nonSensitiveVariables = new VariableDictionary(variablesFile);
                variables.Merge(nonSensitiveVariables);
            }

            foreach (var sensitiveFilePath in options.InputVariables.SensitiveVariablesFiles)
            {
                if (string.IsNullOrEmpty(sensitiveFilePath)) continue;

                var sensitiveFilePassword = options.InputVariables.SensitiveVariablesPassword;
                var rawVariables = string.IsNullOrWhiteSpace(sensitiveFilePassword)
                    ? fileSystem.ReadFile(sensitiveFilePath)
                    : Decrypt(fileSystem.ReadAllBytes(sensitiveFilePath), sensitiveFilePassword);
                
                try
                {
                    var sensitiveVariables = JsonConvert.DeserializeObject<Dictionary<string, string>>(rawVariables);
                    foreach (var variable in sensitiveVariables)
                    {
                        variables.SetSensitive(variable.Key, variable.Value);
                    }
                }
                catch (JsonReaderException)
                {
                    throw new CommandException("Unable to parse sensitive-variables as valid JSON.");
                }
            }

            var outputVariablesFilePath = options.InputVariables.OutputVariablesFile;
            if (!string.IsNullOrEmpty(outputVariablesFilePath))
            {
                var rawVariables = DecryptWithMachineKey(fileSystem.ReadFile(outputVariablesFilePath), options.InputVariables.OutputVariablesPassword);
                try
                {
                    var outputVariables = JsonConvert.DeserializeObject<Dictionary<string, string>>(rawVariables);
                    foreach (var variable in outputVariables)
                    {
                        variables.Set(variable.Key, variable.Value);
                    }

                }
                catch (JsonReaderException)
                {
                    throw new CommandException("Unable to parse output variables as valid JSON.");
                }
            }

            return variables;
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
    }
}