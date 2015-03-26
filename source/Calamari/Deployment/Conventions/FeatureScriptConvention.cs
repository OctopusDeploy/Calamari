using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Deployment.Conventions
{
    public class FeatureScriptConvention : IInstallConvention
    {
        readonly string scriptSuffix;
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;
        readonly IScriptEngineSelector scriptEngineSelector;
        readonly ICommandLineRunner commandLineRunner;
        const string featureScriptNamePrefix = "Octopus.Features."; 
        const string scriptResourcePrefix = "Calamari.Scripts.";

        public FeatureScriptConvention(string scriptSuffix, ICalamariFileSystem fileSystem, ICalamariEmbeddedResources embeddedResources, 
            IScriptEngineSelector scriptEngineSelector, ICommandLineRunner commandLineRunner)
        {
            this.scriptSuffix = scriptSuffix;
            this.fileSystem = fileSystem;
            this.embeddedResources = embeddedResources;
            this.scriptEngineSelector = scriptEngineSelector;
            this.commandLineRunner = commandLineRunner;
        }

        public void Install(RunningDeployment deployment)
        {
            var features = deployment.Variables.GetStrings(SpecialVariables.Package.EnabledFeatures).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            if (!features.Any())
                return;

            var embeddedResourceNames = new HashSet<string>(embeddedResources.GetEmbeddedResourceNames());

            foreach (var featureScript in features.SelectMany(GetScriptNames))
            {
                // Determine the embedded-resource name
                var scriptEmbeddedResource = GetEmbeddedResourceName(featureScript);

                // If there is a matching embedded resource
                if (!embeddedResourceNames.Contains(scriptEmbeddedResource))
                    continue;

                var scriptFile = Path.Combine(deployment.CurrentDirectory, featureScript);

                // To execute the script, we need a physical file on disk. 
                // If one already exists, we don't recreate it, as this provides a handy
                // way to override behaviour.
                if (!fileSystem.FileExists(scriptFile))
                {
                    fileSystem.OverwriteFile(scriptFile, embeddedResources.GetEmbeddedResourceText(scriptEmbeddedResource));
                }

                // Execute the script
                Log.VerboseFormat("Executing '{0}'", scriptFile);
                var result = scriptEngineSelector.SelectEngine(scriptFile).Execute(scriptFile, deployment.Variables, commandLineRunner);

                // And then delete it
                fileSystem.DeleteFile(scriptFile, DeletionOptions.TryThreeTimes);

                if (result.ExitCode != 0)
                {
                    throw new CommandException(string.Format("Script '{0}' returned non-zero exit code: {1}", scriptFile,
                        result.ExitCode));
                }

            }
        }

        public static string GetEmbeddedResourceName(string featureScriptName)
        {
            return scriptResourcePrefix + featureScriptName;
        }

        public static string GetScriptName(string feature, string suffix, string extension)
        {
            return featureScriptNamePrefix + feature + "_" + suffix + "." + extension;
        }

        /// <summary>
        /// Generates possible script names using the supplied suffix, and all supported extensions
        /// </summary>
        private IEnumerable<string> GetScriptNames(string feature)
        {
            return scriptEngineSelector.GetSupportedExtensions()
                .Select(extension => GetScriptName(feature, scriptSuffix, extension ));
        }

    }
}