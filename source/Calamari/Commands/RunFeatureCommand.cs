using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Calamari.Commands.Support;
using Calamari.Features;
using Calamari.Features.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Shared;
using Calamari.Shared.Convention;
using Calamari.Shared.Features;
using Calamari.Util;
using Newtonsoft.Json;
using Octostache;

namespace Calamari.Commands
{
    [Command("run-feature", Description = "Extracts and installs a deployment package")]
    public class RunFeatureCommand : Command
    {
        private string variablesFile;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;
        private string featureName;

        public RunFeatureCommand()
        {
            Options.Add("feature=",
                "The name of the feature that will be loaded from available assembelies and invoked.",
                v => featureName = v);
            Options.Add("variables=", "Path to a JSON file containing variables.",
                v => variablesFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.",
                v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.",
                v => sensitiveVariablesPassword = v);
        }
        
        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);
            Guard.NotNullOrWhiteSpace(featureName,
                "No feature was specified. Please pass a value for the `--feature` option.");

            var variables = new CustomCalamariVariableDictionary(variablesFile, sensitiveVariablesFile,
                sensitiveVariablesPassword);


            var container = new CalamariCalamariContainer();
            new MyModule().Register(container);

            container.RegisterInstance(new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables))));
            container.RegisterInstance<IVariableDictionary>(variables);

            var al = new AssemblyLoader();
            al.RegisterCompiled();
            al.RegisterAssembly(this.GetType().GetTypeInfo().Assembly);


            var featureLocator = new FeatureLocator(al);
            var diBuilder = new DepencencyInjectionBuilder(container);
            var sequence = new ConventionSequence(new ConventionLocator(al));

            sequence
                .Run<ContributeEnvironmentVariablesConvention>()
                .Run<LogVariablesConvention>();


            var type = featureLocator.GetFeatureType(featureName);
            var feature = (IFeature)diBuilder.BuildConvention(type, new object[0]);


            feature.ConfigureInstallSequence(variables, sequence);
            //////

            foreach (var s in sequence.Sequence)
            {
                if (!s.Condition(variables))
                    continue;

                foreach (var arg in s.Arguments(variables))
                {
                    if (s.LocalConvention != null)
                    {
                        s.LocalConvention(variables);
                    }
                    else
                    {
                        var conventionInstance = (s.ConventionType == null)
                            ? s.ConventionInstance
                            : (IInstallConvention) diBuilder.BuildConvention(s.ConventionType, arg);
                        conventionInstance.Install(variables);
                    }
                }
            }

            
            

            /*
            try
            {
                foreach (var convention in sequence.Sequence)
                {
                    if (!convention.Item1(ctx.Variables))
                        continue;

                    var installer = convention.Item2;

                    //if exits non zero;
                    installer.Install(ctx.Variables);
                }
            }
            catch (Exception)
            {
                var rollbackConvention = (feature as IRollbackConvention);
                if (rollbackConvention != null)
                {
                    // rollbackConvention.SetUpRollback();
                }
            }
            */
            return 0;
        }
    }





    public class CustomCalamariVariableDictionary : VariableDictionary, IVariableDictionary
    {
        protected HashSet<string> SensitiveVariableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void SetOutputVariable(string name, string value)
        {
            Set(name, value);

            // And set the output-variables.
            // Assuming we are running in a step named 'DeployWeb' and are setting a variable named 'Foo'
            // then we will set Octopus.Action[DeployWeb].Output.Foo
            var actionName = Get(SpecialVariables.Action.Name);

            if (string.IsNullOrWhiteSpace(actionName))
                return;

            var actionScopedVariable = SpecialVariables.GetOutputVariableName(actionName, name);

            Set(actionScopedVariable, value);

            // And if we are on a machine named 'Web01'
            // Then we will set Octopus.Action[DeployWeb].Output[Web01].Foo
            var machineName = Get(SpecialVariables.Machine.Name);

            if (string.IsNullOrWhiteSpace(machineName))
                return;

            var machineIndexedVariableName = SpecialVariables.GetMachineIndexedOutputVariableName(actionName, machineName, name);
            Set(machineIndexedVariableName, value);
        }

        public CustomCalamariVariableDictionary() { }

        public CustomCalamariVariableDictionary(string storageFilePath) : base(storageFilePath) { }

        public CustomCalamariVariableDictionary(string storageFilePath, string sensitiveFilePath, string sensitiveFilePassword)
        {
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            if (!string.IsNullOrEmpty(storageFilePath))
            {
                if (!fileSystem.FileExists(storageFilePath))
                    throw new CommandException("Could not find variables file: " + storageFilePath);

                var nonSensitiveVariables = new VariableDictionary(storageFilePath);
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
    }
    
  
}