using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.ConfigurationVariables;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Common.Features.Behaviours
{
    public class ConfigurationVariablesBehaviour : IBehaviour
    {
        readonly ICalamariFileSystem fileSystem;
        readonly IVariables variables;
        readonly IConfigurationVariablesReplacer replacer;
        readonly ILog log;
        private readonly string subdirectory;

        public ConfigurationVariablesBehaviour(
            ICalamariFileSystem fileSystem,
            IVariables variables,
            IConfigurationVariablesReplacer replacer,
            ILog log,
            string? subdirectory = "")
        {
            this.fileSystem = fileSystem;
            this.variables = variables;
            this.replacer = replacer;
            this.log = log;
            this.subdirectory = subdirectory;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return context.Variables.IsFeatureEnabled(KnownVariables.Features.ConfigurationVariables) &&
                context.Variables.GetFlag(KnownVariables.Package.AutomaticallyUpdateAppSettingsAndConnectionStrings);
        }

        public Task Execute(RunningDeployment context)
        {
            DoTransforms(Path.Combine(context.CurrentDirectory, subdirectory));

            return this.CompletedTask();
        }

        public void DoTransforms(string currentDirectory)
        {
            var appliedAsTransforms = variables.GetStrings(KnownVariables.AppliedXmlConfigTransforms, '|');

            log.Verbose("Looking for appSettings, applicationSettings, and connectionStrings in any .config files");

            if (variables.GetFlag(KnownVariables.Package.IgnoreVariableReplacementErrors))
                log.Info("Variable replacement errors are suppressed because the variable Octopus.Action.Package.IgnoreVariableReplacementErrors has been set.");

            foreach (var configurationFile in MatchingFiles(currentDirectory))
            {
                if (appliedAsTransforms.Contains(configurationFile))
                {
                    log.VerboseFormat("File '{0}' was interpreted as an XML configuration transform; variable substitution won't be attempted.", configurationFile);
                    continue;
                }

                replacer.ModifyConfigurationFile(configurationFile, variables);
            }
        }
        string[] MatchingFiles(string currentDirectory)
        {
            var files = fileSystem.EnumerateFilesRecursively(currentDirectory, "*.config");

            var additional = variables.GetStrings(ActionVariables.AdditionalPaths)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .SelectMany(p => fileSystem.EnumerateFilesRecursively(p, "*.config"));


            return files.Concat(additional).Distinct().ToArray();
        }
    }
}