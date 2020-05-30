using System;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Commands.Support;
using Calamari.Common.Features.Scripting;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Util;

namespace Calamari.Deployment.Conventions
{
    public class ConfiguredScriptConvention : IInstallConvention
    {
        readonly ConfiguredScriptService service;
        readonly string deploymentStage;

        public ConfiguredScriptConvention(ConfiguredScriptService service, string deploymentStage)
        {
            this.service = service;
            this.deploymentStage = deploymentStage;
        }

        public void Install(RunningDeployment deployment)
        {
            service.Install(deployment, deploymentStage);
        }

        public static string GetScriptName(string deploymentStage, ScriptSyntax scriptSyntax)
        {
            return "Octopus.Action.CustomScripts." + deploymentStage + "." + scriptSyntax.FileExtension();
        }


    }

        public class ConfiguredScriptService
    {
        readonly IScriptEngine scriptEngine;
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;

        public ConfiguredScriptService(ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner)
        {
            this.scriptEngine = scriptEngine;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
        }

        public void Install(RunningDeployment deployment, string deploymentStage)
        {
            var features = deployment.Variables.GetStrings(SpecialVariables.Package.EnabledFeatures)
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            if (!features.Contains(SpecialVariables.Features.CustomScripts))
                return;

            foreach (ScriptSyntax scriptType in Enum.GetValues(typeof(ScriptSyntax)))
            {
                var scriptName = SpecialVariables.Action.CustomScripts.GetCustomScriptStage(deploymentStage, scriptType);
                string error;
                var scriptBody = deployment.Variables.Get(scriptName, out error);
                if (!string.IsNullOrEmpty(error))
                    Log.VerboseFormat(
                        "Parsing script for phase {0} with Octostache returned the following error: `{1}`",
                        deploymentStage,
                        error);

                if (string.IsNullOrWhiteSpace(scriptBody))
                    continue;

                if (!scriptEngine.GetSupportedTypes().Contains(scriptType))
                    throw new CommandException($"{scriptType} scripts are not supported on this platform ({deploymentStage})");

                var scriptFile = Path.Combine(deployment.CurrentDirectory, scriptName);
                var scriptBytes = scriptType == ScriptSyntax.Bash
                    ? scriptBody.EncodeInUtf8NoBom()
                    : scriptBody.EncodeInUtf8Bom();
                fileSystem.WriteAllBytes(scriptFile, scriptBytes);

                // Execute the script
                Log.VerboseFormat("Executing '{0}'", scriptFile);
                var result = scriptEngine.Execute(new Script(scriptFile), deployment.Variables, commandLineRunner);

                if (result.ExitCode != 0)
                {
                    throw new CommandException($"{deploymentStage} script returned non-zero exit code: {result.ExitCode}");
                }

                if (result.HasErrors && deployment.Variables.GetFlag(SpecialVariables.Action.FailScriptOnErrorOutput, false))
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

        public static string GetScriptName(string deploymentStage, ScriptSyntax scriptSyntax)
        {
            return "Octopus.Action.CustomScripts." + deploymentStage + "." + scriptSyntax.FileExtension();
        }


    }

}