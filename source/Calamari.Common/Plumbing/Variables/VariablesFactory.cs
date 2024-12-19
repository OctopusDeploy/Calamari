using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Newtonsoft.Json;
using Octostache;

namespace Calamari.Common.Plumbing.Variables
{
    public class VariablesFactory
    {
        public static readonly string AdditionalVariablesPathVariable = "AdditionalVariablesPath";
        readonly ICalamariFileSystem fileSystem;

        public VariablesFactory(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public IVariables Create(CommonOptions options)
        {
            var variables = new CalamariVariables();

            ReadUnencryptedVariablesFromFile(options, variables);
            ReadEncryptedVariablesFromFile(options, variables);
            ReadOutputVariablesFromOfflineDropPreviousSteps(options, variables);

            AddEnvironmentVariables(variables);
            variables.Set(TentacleVariables.Agent.InstanceName, "#{env:TentacleInstanceName}");
            ReadAdditionalVariablesFromFile(variables);
            DeploymentJournalVariableContributor.Contribute(fileSystem, variables);

            return variables;
        }

        void ReadUnencryptedVariablesFromFile(CommonOptions options, CalamariVariables variables)
        {
            var variablesFile = options.InputVariables.VariablesFile;
            if (string.IsNullOrEmpty(variablesFile))
                return;

            if (!fileSystem.FileExists(variablesFile))
                throw new CommandException("Could not find variables file: " + variablesFile);

            var readVars = new VariableDictionary(variablesFile);
            variables.Merge(readVars);
        }

        void ReadEncryptedVariablesFromFile(CommonOptions options, CalamariVariables variables)
        {
            foreach (var sensitiveFilePath in options.InputVariables.SensitiveVariablesFiles.Where(f => !string.IsNullOrEmpty(f)))
            {
                var sensitiveFilePassword = options.InputVariables.SensitiveVariablesPassword;
                var rawVariables = string.IsNullOrWhiteSpace(sensitiveFilePassword)
                    ? fileSystem.ReadFile(sensitiveFilePath)
                    : Decrypt(fileSystem.ReadAllBytes(sensitiveFilePath), sensitiveFilePassword);

                try
                {
                    var sensitiveVariables = JsonConvert.DeserializeObject<Dictionary<string, string>>(rawVariables);
                    foreach (var variable in sensitiveVariables)
                        variables.Set(variable.Key, variable.Value);
                }
                catch (JsonReaderException)
                {
                    throw new CommandException("Unable to parse sensitive-variables as valid JSON.");
                }
            }
        }

        void ReadOutputVariablesFromOfflineDropPreviousSteps(CommonOptions options, CalamariVariables variables)
        {
            var outputVariablesFilePath = options.InputVariables.OutputVariablesFile;
            if (string.IsNullOrEmpty(outputVariablesFilePath))
                return;

            var rawVariables = DecryptWithMachineKey(fileSystem.ReadFile(outputVariablesFilePath), options.InputVariables.OutputVariablesPassword);
            try
            {
                var outputVariables = JsonConvert.DeserializeObject<Dictionary<string, string>>(rawVariables);
                foreach (var variable in outputVariables)
                    variables.Set(variable.Key, variable.Value);
            }
            catch (JsonReaderException)
            {
                throw new CommandException("Unable to parse output variables as valid JSON.");
            }
        }

        static void AddEnvironmentVariables(IVariables variables)
        {
            var environmentVariables = Environment.GetEnvironmentVariables();
            foreach (var name in environmentVariables.Keys)
                variables["env:" + name] = (environmentVariables[name] ?? string.Empty).ToString();
        }

        void ReadAdditionalVariablesFromFile(CalamariVariables variables)
        {
            var path = variables.Get(AdditionalVariablesPathVariable) ?? variables.Get($"env:{AdditionalVariablesPathVariable}");

            string BuildExceptionMessage(string reason)
            {
                return $"Could not read additional variables from JSON file at '{path}'. " +
                    $"{reason} Make sure the file can be read or remove the " +
                    $"'{AdditionalVariablesPathVariable}' environment variable. " +
                    "See inner exception for details.";
            }

            if (string.IsNullOrEmpty(path))
                return;

            if (!fileSystem.FileExists(path))
                throw new CommandException(BuildExceptionMessage("File does not exist."));

            try
            {
                var readVars = new VariableDictionary(path);
                variables.Merge(readVars);
            }
            catch (Exception e)
            {
                throw new CommandException(BuildExceptionMessage("The file could not be read."), e);
            }
        }

        static string Decrypt(byte[] encryptedVariables, string encryptionPassword)
        {
            try
            {
                return new AesEncryption(encryptionPassword, AesEncryption.VariablesFileKeySize).Decrypt(encryptedVariables);
            }
            catch (CryptographicException)
            {
                throw new CommandException("Cannot decrypt sensitive-variables. Check your password is correct.");
            }
        }

        static string DecryptWithMachineKey(string base64EncodedEncryptedVariables, string? password)
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