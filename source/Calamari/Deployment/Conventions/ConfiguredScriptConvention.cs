using System;
using System.IO;
using System.Linq;
using System.Text;
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

        public ConfiguredScriptConvention(string deploymentStage, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner)
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

            foreach (ScriptType scriptType in Enum.GetValues(typeof(ScriptType)))
            {
                var scriptName = GetScriptName(deploymentStage, scriptType);
                
                string error;
                var scriptBody = deployment.Variables.Get(scriptName, out error);
                if (!string.IsNullOrEmpty(error))
                    Log.VerboseFormat("Parsing script for phase {0} with Octostache returned the following error: `{1}`", deploymentStage, error);

                if (string.IsNullOrWhiteSpace(scriptBody))
                    continue;

                if (!scriptEngine.GetSupportedTypes().Contains(scriptType))
                    throw new CommandException($"{scriptType} scripts are not supported on this platform ({deploymentStage})");

                var scriptFile = Path.Combine(deployment.CurrentDirectory, scriptName);

                fileSystem.OverwriteFile(scriptFile, scriptBody, Encoding.UTF8);

                // Execute the script
                Log.VerboseFormat("Executing '{0}'", scriptFile);
                var result = scriptEngine.Execute(new Script(scriptFile), deployment.Variables, commandLineRunner);

                if (result.ExitCode != 0)
                {
                    throw new CommandException($"{deploymentStage} script returned non-zero exit code: {result.ExitCode}");
                }

                if (result.HasErrors && deployment.Variables.GetFlag(SpecialVariables.Action.TreatScriptWarningsAsErrors, false))
                {
                    throw new CommandException($"{deploymentStage} script returned zero exit code but had error output.");
                }

                if (deployment.Variables.GetFlag(SpecialVariables.DeleteScriptsOnCleanup, true))
                {
                    // And then delete it (this means if the script failed, it will persist, which may assist debugging)
                    fileSystem.DeleteFile(scriptFile, FailureOptions.IgnoreFailure);
                }
            }
        }

        public static string GetScriptName(string deploymentStage, ScriptType scriptType)
        {
            return "Octopus.Action.CustomScripts." + deploymentStage + "." + scriptType.FileExtension();
        }


    }
}