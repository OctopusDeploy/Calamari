using System;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.ConfigurationVariables;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Behaviours
{
    public class ConfigurationVariablesBehaviour : IBehaviour
    {
        readonly ICalamariFileSystem fileSystem;
        readonly IConfigurationVariablesReplacer replacer;
        readonly ILog log;

        public ConfigurationVariablesBehaviour(ICalamariFileSystem fileSystem, IConfigurationVariablesReplacer replacer, ILog log)
        {
            this.fileSystem = fileSystem;
            this.replacer = replacer;
            this.log = log;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return context.Variables.IsFeatureEnabled(KnownVariables.Features.ConfigurationVariables) &&
                context.Variables.GetFlag(KnownVariables.Package.AutomaticallyUpdateAppSettingsAndConnectionStrings);
        }

        public Task Execute(RunningDeployment context)
        {
            var appliedAsTransforms = context.Variables.GetStrings(KnownVariables.AppliedXmlConfigTransforms, '|');

            log.Verbose("Looking for appSettings, applicationSettings, and connectionStrings in any .config files");

            if (context.Variables.GetFlag(KnownVariables.Package.IgnoreVariableReplacementErrors))
                log.Info("Variable replacement errors are suppressed because the variable Octopus.Action.Package.IgnoreVariableReplacementErrors has been set.");

            foreach (var configurationFile in MatchingFiles(context))
            {
                if (appliedAsTransforms.Contains(configurationFile))
                {
                    log.VerboseFormat("File '{0}' was interpreted as an XML configuration transform; variable substitution won't be attempted.", configurationFile);
                    continue;
                }

                replacer.ModifyConfigurationFile(configurationFile, context.Variables);
            }

            return this.CompletedTask();
        }

        string[] MatchingFiles(RunningDeployment deployment)
        {
            var files = fileSystem.EnumerateFilesRecursively(deployment.CurrentDirectory, "*.config");

            var additional = deployment.Variables.GetStrings(ActionVariables.AdditionalPaths)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .SelectMany(p => fileSystem.EnumerateFilesRecursively(p, "*.config"));


            return files.Concat(additional).Distinct().ToArray();
        }
    }
}