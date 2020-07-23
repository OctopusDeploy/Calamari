using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Calamari.Common.Commands;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Features;

namespace Calamari.Deployment.Conventions
{
    public class FeatureConvention : FeatureConventionBase, IInstallConvention
    {
        public FeatureConvention(string deploymentStage, IEnumerable<IFeature> featureClasses, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner, ICalamariEmbeddedResources embeddedResources)
            : base(deploymentStage, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources)
        {
        }

        public void Install(RunningDeployment deployment)
        {
            Run(deployment);
        }
    }

    public class FeatureRollbackConvention : FeatureConventionBase, IRollbackConvention
    {
        public FeatureRollbackConvention(string deploymentStage, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner, ICalamariEmbeddedResources embeddedResources) : base(deploymentStage, null, fileSystem, scriptEngine, commandLineRunner, embeddedResources)
        {
        }

        public void Rollback(RunningDeployment deployment)
        {
            Run(deployment);
        }

        public void Cleanup(RunningDeployment deployment)
        {

        }
    }

    public abstract class FeatureConventionBase
    {
        readonly string deploymentStage;
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;
        const string scriptResourcePrefix = "Calamari.Scripts.";
        readonly ICollection<IFeature> featureClasses;
        static readonly Assembly Assembly = typeof(FeatureConventionBase).Assembly;

        protected FeatureConventionBase(string deploymentStage, IEnumerable<IFeature> featureClasses, ICalamariFileSystem fileSystem,
            IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner, ICalamariEmbeddedResources embeddedResources)
        {
            this.deploymentStage = deploymentStage;
            this.fileSystem = fileSystem;
            this.embeddedResources = embeddedResources;
            this.scriptEngine = scriptEngine;
            this.commandLineRunner = commandLineRunner;
            this.featureClasses = featureClasses?.ToList();
        }

        protected void Run(RunningDeployment deployment)
        {
            var features = deployment.Variables.GetStrings(KnownVariables.Package.EnabledFeatures).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            if (!features.Any())
                return;

            var embeddedResourceNames = new HashSet<string>(embeddedResources.GetEmbeddedResourceNames(Assembly));

            foreach (var feature in features)
            {
                // Features can be implemented as either classes or scripts (or both)
                ExecuteFeatureClasses(deployment, feature);
                ExecuteFeatureScripts(deployment, feature, embeddedResourceNames);
            }

        }

        void ExecuteFeatureClasses(RunningDeployment deployment, string feature)
        {
            var compiledFeature = featureClasses?.FirstOrDefault(f =>
                f.Name.Equals(feature, StringComparison.OrdinalIgnoreCase) &&
                f.DeploymentStage.Equals(deploymentStage, StringComparison.OrdinalIgnoreCase));

            if (compiledFeature == null)
                return;

            Log.Verbose($"Executing feature-class '{compiledFeature.GetType()}'");
            compiledFeature.Execute(deployment);
        }

        void ExecuteFeatureScripts(RunningDeployment deployment, string feature, HashSet<string> embeddedResourceNames)
        {
            foreach (var featureScript in GetScriptNames(feature))
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
                    fileSystem.OverwriteFile(scriptFile, embeddedResources.GetEmbeddedResourceText(Assembly, scriptEmbeddedResource));
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
                    throw new CommandException(string.Format("Script '{0}' returned non-zero exit code: {1}", scriptFile, result.ExitCode));
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
            return scriptEngine.GetSupportedTypes()
                .Select(type => GetScriptName(feature, deploymentStage, type.FileExtension()));
        }

    }
}
