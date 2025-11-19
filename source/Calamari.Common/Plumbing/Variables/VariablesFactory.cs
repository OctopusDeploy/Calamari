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

        static readonly object LoaderLock = new object();
        CalamariExecutionVariableCollection? executionVariables;

        public VariablesFactory(ICalamariFileSystem fileSystem, ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
        }

        public IVariables Create(CommonOptions options) => Create(new CalamariVariables(), options, _ => true);
        public INonSensitiveVariables CreateNonSensitiveVariables(CommonOptions options) => Create(new NonSensitiveCalamariVariables(), options, cev => !cev.IsSensitive);

        T Create<T>(T variables, CommonOptions options, Func<CalamariExecutionVariable, bool> predicate) where T : CalamariVariables
        {
            LoadExecutionVariablesFromFiles(options);

            //we load _all_ variables from the execution variables into the CalamariVariables dictionary
            ImportExecutionVariablesIntoVariableCollection(variables, predicate);

            AddEnvironmentVariables(variables);
            variables.Set(TentacleVariables.Agent.InstanceName, "#{env:TentacleInstanceName}");
            ReadAdditionalVariablesFromFile(variables);
            DeploymentJournalVariableContributor.Contribute(fileSystem, variables, log);

            return variables;
        }

        void LoadExecutionVariablesFromFiles(CommonOptions options)
        {
            lock (LoaderLock)
            {
                //if the execution variables have already been loaded, we don't need to load them again
                if (executionVariables is { })
                    return;
                
                executionVariables = new CalamariExecutionVariableCollection();

                executionVariables.AddRange(LoadExecutionVariablesFromFile(options));

                // This exists as the V2 pipeline stores both the parameters and the contents of the variables files for resiliency
                // This should be removed once the first version this is deployed to has rolled out to most cloud customers
                executionVariables.AddRange(ReadDeprecatedVariablesFormatFromFiles(options));

                executionVariables.AddRange(ReadOutputVariablesFromOfflineDropPreviousSteps(options));
            }
        }

        IEnumerable<CalamariExecutionVariable> LoadExecutionVariablesFromFile(CommonOptions options)
        {
            var results = new List<CalamariExecutionVariable>();
            foreach (var variableFilePath in options.InputVariables.VariableFiles.Where(f => !string.IsNullOrEmpty(f)))
            {
                var sensitiveFilePassword = options.InputVariables.VariablesPassword;
                var json = string.IsNullOrWhiteSpace(sensitiveFilePassword)
                    ? fileSystem.ReadFile(variableFilePath)
                    : Decrypt(fileSystem.ReadAllBytes(variableFilePath), sensitiveFilePassword);

                try
                {
                    //deserialize the target variables from the json
                    var targetVariables = CalamariExecutionVariableCollection.FromJson(json);
                    results.AddRange(targetVariables);
                }
                catch (JsonReaderException)
                {
                    throw new CommandException("Unable to parse variables as valid JSON.");
                }
            }

            return results;
        }

        IEnumerable<CalamariExecutionVariable> ReadOutputVariablesFromOfflineDropPreviousSteps(CommonOptions options)
        {
            var outputVariablesFilePath = options.InputVariables.OutputVariablesFile;
            if (string.IsNullOrEmpty(outputVariablesFilePath))
                return new CalamariExecutionVariableCollection();

            var rawVariables = DecryptWithMachineKey(fileSystem.ReadFile(outputVariablesFilePath), options.InputVariables.OutputVariablesPassword);
            try
            {
                var variables = CalamariExecutionVariableCollection.FromJson(rawVariables);
                return variables;
            }
            catch (JsonReaderException)
            {
                throw new CommandException("Unable to parse output variables as valid JSON.");
            }
        }

        IEnumerable<CalamariExecutionVariable> ReadDeprecatedVariablesFormatFromFiles(CommonOptions options)
        {
            var results = new List<CalamariExecutionVariable>();
            foreach (var variableFilePath in options.InputVariables.DeprecatedFormatVariableFiles.Where(f => !string.IsNullOrEmpty(f)))
            {
                var sensitiveFilePassword = options.InputVariables.DeprecatedVariablesPassword;
                var json = string.IsNullOrWhiteSpace(sensitiveFilePassword)
                    ? fileSystem.ReadFile(variableFilePath)
                    : Decrypt(fileSystem.ReadAllBytes(variableFilePath), sensitiveFilePassword);

                try
                {
                    var outputVariables = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

                    // We don't know if the previous variables were sensitive or not, so treat them as non-sensitive */
                    results.AddRange(outputVariables.Select(ov => new CalamariExecutionVariable(ov.Key, ov.Value, false)));
                }
                catch (JsonReaderException)
                {
                    throw new CommandException("Unable to parse variables as valid JSON.");
                }
            }

            return results;
        }

        void ImportExecutionVariablesIntoVariableCollection(IVariables variables, Func<CalamariExecutionVariable, bool> targetVariablePredicate)
        {
            if(executionVariables == null)
            {
                throw new InvalidOperationException("The execution variable collection is null, meaning it has not been loaded yet.");
            }
            
            //for each variable, load it into the variable collection
            foreach (var tv in executionVariables.Where(targetVariablePredicate))
            {
                variables.Set(tv.Key, tv.Value);
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
                throw new CommandException("Cannot decrypt variables. Check your password is correct.");
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
