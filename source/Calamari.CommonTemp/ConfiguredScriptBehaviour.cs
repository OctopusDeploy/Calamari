using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Commands.Support;
using Calamari.Common.Features.Scripting;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Util;

namespace Calamari.CommonTemp
{
    internal class ConfiguredScriptBehaviour : IBehaviour
    {
        readonly string deploymentStage;
        readonly ILog log;
        readonly IScriptEngine scriptEngine;
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;

        public ConfiguredScriptBehaviour(string deploymentStage, ILog log, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner)
        {
            this.deploymentStage = deploymentStage;
            this.log = log;
            this.scriptEngine = scriptEngine;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            var features = context.Variables.GetStrings(KnownVariables.Package.EnabledFeatures)
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            return features.Contains(KnownVariables.Features.CustomScripts);
        }

        public Task Execute(RunningDeployment context)
        {
            foreach (ScriptSyntax scriptType in Enum.GetValues(typeof(ScriptSyntax)))
            {
                var scriptName = KnownVariables.Action.CustomScripts.GetCustomScriptStage(deploymentStage, scriptType);
                string error;
                var scriptBody = context.Variables.Get(scriptName, out error);
                if (!string.IsNullOrEmpty(error))
                    log.VerboseFormat(
                        "Parsing script for phase {0} with Octostache returned the following error: `{1}`",
                        deploymentStage,
                        error);

                if (string.IsNullOrWhiteSpace(scriptBody))
                    continue;

                if (!scriptEngine.GetSupportedTypes().Contains(scriptType))
                    throw new CommandException($"{scriptType} scripts are not supported on this platform ({deploymentStage})");

                var scriptFile = Path.Combine(context.CurrentDirectory, scriptName);
                var scriptBytes = scriptType == ScriptSyntax.Bash
                    ? scriptBody.EncodeInUtf8NoBom()
                    : scriptBody.EncodeInUtf8Bom();
                fileSystem.WriteAllBytes(scriptFile, scriptBytes);

                // Execute the script
                log.VerboseFormat("Executing '{0}'", scriptFile);
                var result = scriptEngine.Execute(new Script(scriptFile), context.Variables, commandLineRunner);

                if (result.ExitCode != 0)
                {
                    throw new CommandException($"{deploymentStage} script returned non-zero exit code: {result.ExitCode}");
                }

                if (result.HasErrors && context.Variables.GetFlag(KnownVariables.Action.FailScriptOnErrorOutput, false))
                {
                    throw new CommandException($"{deploymentStage} script returned zero exit code but had error output.");
                }

                if (context.Variables.GetFlag(KnownVariables.DeleteScriptsOnCleanup, true))
                {
                    // And then delete it (this means if the script failed, it will persist, which may assist debugging)
                    fileSystem.DeleteFile(scriptFile, FailureOptions.IgnoreFailure);
                }
            }

            return this.CompletedTask();
        }

        public static string GetScriptName(string deploymentStage, ScriptSyntax scriptSyntax)
        {
            return "Octopus.Action.CustomScripts." + deploymentStage + "." + scriptSyntax.FileExtension();
        }
    }
}