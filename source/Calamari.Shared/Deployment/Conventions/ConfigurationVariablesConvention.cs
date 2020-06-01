﻿using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Variables;
using Calamari.Integration.ConfigurationVariables;
using Calamari.Integration.FileSystem;

namespace Calamari.Deployment.Conventions
{
    public class ConfigurationVariablesConvention : IInstallConvention
    {
        readonly ConfigurationVariablesService service;

        public ConfigurationVariablesConvention(ConfigurationVariablesService service)
        {
            this.service = service;
        }

        public void Install(RunningDeployment deployment)
        {
            service.Install(deployment, deployment.Variables.GetStrings(SpecialVariables.AppliedXmlConfigTransforms, '|'));
        }
    }

    public class ConfigurationVariablesService
    {
        readonly ICalamariFileSystem fileSystem;
        readonly IConfigurationVariablesReplacer replacer;

        public ConfigurationVariablesService(ICalamariFileSystem fileSystem, IConfigurationVariablesReplacer replacer)
        {
            this.fileSystem = fileSystem;
            this.replacer = replacer;
        }

        public void Install(RunningDeployment deployment, List<string> appliedAsTransforms)
        {
            if (deployment.Variables.GetFlag(SpecialVariables.Package.AutomaticallyUpdateAppSettingsAndConnectionStrings) == false)
            {
                return;
            }

            Log.Verbose("Looking for appSettings, applicationSettings, and connectionStrings in any .config files");

            if (deployment.Variables.GetFlag(SpecialVariables.Package.IgnoreVariableReplacementErrors))
                Log.Info("Variable replacement errors are supressed because the variable Octopus.Action.Package.IgnoreVariableReplacementErrors has been set.");

            foreach (var configurationFile in MatchingFiles(deployment))
            {
                if (appliedAsTransforms.Contains(configurationFile))
                {
                    Log.VerboseFormat("File '{0}' was interpreted as an XML configuration transform; variable substitution won't be attempted.", configurationFile);
                    continue;
                }

                replacer.ModifyConfigurationFile(configurationFile, deployment.Variables);
            }
        }

        private string[] MatchingFiles(RunningDeployment deployment)
        {
            var files = fileSystem.EnumerateFilesRecursively(deployment.CurrentDirectory, "*.config");

            var additional = deployment.Variables.GetStrings(ActionVariables.AdditionalPaths)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .SelectMany(p => fileSystem.EnumerateFilesRecursively(p, "*.config"));


            return files.Concat(additional).Distinct().ToArray();
        }
    }
}