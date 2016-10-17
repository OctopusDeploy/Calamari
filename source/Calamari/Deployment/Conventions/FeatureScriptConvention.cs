using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Util;
using System.Reflection;

namespace Calamari.Deployment.Conventions
{
    public class FeatureScriptConvention : IInstallConvention
    {
        readonly string deploymentStage;
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;
        const string scriptResourcePrefix = "Calamari.Scripts.";

        public FeatureScriptConvention(string deploymentStage, ICalamariFileSystem fileSystem, 
            IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner, ICalamariEmbeddedResources embeddedResources)
        {
            this.deploymentStage = deploymentStage;
            this.fileSystem = fileSystem;
            this.embeddedResources = embeddedResources;
            this.scriptEngine = scriptEngine;
            this.commandLineRunner = commandLineRunner;
        }

        public void Install(RunningDeployment deployment)
        {
            var features = deployment.Variables.GetStrings(SpecialVariables.Package.EnabledFeatures).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            if (!features.Any())
                return;

            var assembly = typeof(FeatureScriptConvention).GetTypeInfo().Assembly;
            var embeddedResourceNames = new HashSet<string>(embeddedResources.GetEmbeddedResourceNames(assembly));

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
                    Log.VerboseFormat("Creating '{0}' from embedded resource", scriptFile);
                    fileSystem.OverwriteFile(scriptFile, embeddedResources.GetEmbeddedResourceText(assembly, scriptEmbeddedResource));
                }
                else
                {
                    Log.WarnFormat("Did not overwrite '{0}', it was already on disk", scriptFile);
                }

                // Execute the script
                Log.VerboseFormat("Executing '{0}'", scriptFile);
                var result = scriptEngine.Execute(new Script(scriptFile), deployment.Variables, commandLineRunner);

                // And then delete it
                Log.VerboseFormat("Deleting '{0}'", scriptFile);
                fileSystem.DeleteFile(scriptFile, FailureOptions.IgnoreFailure);

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
            return  feature + "_" + suffix + "." + extension;
        }

        /// <summary>
        /// Generates possible script names using the supplied suffix, and all supported extensions
        /// </summary>
        private IEnumerable<string> GetScriptNames(string feature)
        {
            return scriptEngine.GetSupportedExtensions() 
                .Select(extension => GetScriptName(feature, deploymentStage, extension ));
        }

    }
}