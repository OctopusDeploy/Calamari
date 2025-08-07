using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Newtonsoft.Json;
using Octostache;

namespace Calamari.Common.Plumbing.Variables
{
    public class VariablesFactory
    {
        public const string AdditionalVariablesPathVariable = "AdditionalVariablesPath";

        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;

        public VariablesFactory(ICalamariFileSystem fileSystem, ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
        }

        public IVariables Create(CommonOptions options)
        {
            var encryptedVariableFileNames = new List<string>
            {
                options.InputVariables.VariablesFile,
                options.InputVariables.PlatformVariablesFiles,
            };
            encryptedVariableFileNames.AddRange(options.InputVariables.SensitiveVariablesFiles);

            return Create(new CalamariVariables(), options, encryptedVariableFileNames);
        }

        public INonSensitiveOnlyVariables CreateNonSensitiveOnlyVariables(CommonOptions options)
        {
            var encryptedVariableFileNames = new List<string>
            {
                options.InputVariables.VariablesFile,
                options.InputVariables.PlatformVariablesFiles,
            };

            return Create(new NonSensitiveOnlyCalamariVariables(), options, encryptedVariableFileNames);
        }

        T Create<T>(T variables, CommonOptions options, IEnumerable<string> encryptedVariableFileNames) where T : CalamariVariables
        {
            ReadEncryptedVariablesFromFiles(encryptedVariableFileNames, options, variables);

            ReadOutputVariablesFromOfflineDropPreviousSteps(options, variables);

            AddEnvironmentVariables(variables);
            variables.Set(TentacleVariables.Agent.InstanceName, "#{env:TentacleInstanceName}");
            ReadAdditionalVariablesFromFile(variables);
            DeploymentJournalVariableContributor.Contribute(fileSystem, variables, log);

            return variables;
        }

        void ReadEncryptedVariablesFromFiles(IEnumerable<string?> filenames, CommonOptions options, IVariables variables)
        {
            foreach (var sensitiveFilePath in filenames.Where(f => !string.IsNullOrEmpty(f)))
            {
                var sensitiveFilePassword = options.InputVariables.VariableEncryptionPassword;
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

        void ReadOutputVariablesFromOfflineDropPreviousSteps(CommonOptions options, IVariables variables)
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
                return $"Could not read additional variables from JSON file at '{path}'. " + $"{reason} Make sure the file can be read or remove the " + $"'{AdditionalVariablesPathVariable}' environment variable. " + "See inner exception for details.";
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
                return AesEncryption.ForServerVariables(encryptionPassword).Decrypt(encryptedVariables);
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