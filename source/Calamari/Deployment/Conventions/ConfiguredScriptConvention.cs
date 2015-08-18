using System.IO;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Deployment.Conventions
{
    public class ConfiguredScriptConvention : IInstallConvention
    {
        readonly string deploymentStage;
        readonly IScriptEngine scriptEngine;
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;

        public ConfiguredScriptConvention(string deploymentStage, IScriptEngine scriptEngine,
            ICalamariFileSystem fileSystem, ICommandLineRunner commandLineRunner)
        {
            this.deploymentStage = deploymentStage;
            this.scriptEngine = scriptEngine;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
        }

        public void Install(RunningDeployment deployment)
        {
            var features = deployment.Variables.GetStrings(SpecialVariables.Package.EnabledFeatures).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            if (!features.Contains(SpecialVariables.Features.CustomScripts))
                return;

            foreach (var scriptName in scriptEngine.GetSupportedExtensions() 
                .Select(extension => GetScriptName(deploymentStage, extension)))
            {
                var scriptBody = deployment.Variables.Get(scriptName);

                if (string.IsNullOrWhiteSpace(scriptBody))
                    continue;

                var scriptFile = Path.Combine(deployment.CurrentDirectory, scriptName);

                fileSystem.OverwriteFile(scriptFile, scriptBody);

                // Execute the script
                Log.VerboseFormat("Executing '{0}'", scriptFile);
                var result = scriptEngine.Execute(scriptFile, deployment.Variables, commandLineRunner);

                if (result.ExitCode != 0)
                {
                    throw new CommandException(string.Format("Script '{0}' returned non-zero exit code: {1}", scriptFile,
                        result.ExitCode));
                }

                // And then delete it (this means if the script failed, it will persist, which may assist debugging)
                fileSystem.DeleteFile(scriptFile, FailureOptions.IgnoreFailure);
            }
        }

        public static string GetScriptName(string deploymentStage, string extension)
        {
            return "Octopus.Action.CustomScripts." + deploymentStage + "." + extension;
        }


    }
}