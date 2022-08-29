using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Behaviours
{
    public class ConfiguredScriptBehaviour : IBehaviour
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
            return context.Variables.IsFeatureEnabled(KnownVariables.Features.CustomScripts);
        }

        public Task Execute(RunningDeployment context)
        {
            foreach (ScriptSyntax scriptType in Enum.GetValues(typeof(ScriptSyntax)))
            {
                var scriptName = KnownVariables.Action.CustomScripts.GetCustomScriptStage(deploymentStage, scriptType);
                var scriptBody = context.Variables.Get(scriptName, out var error);
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
    }

    public class PreDeployConfiguredScriptBehaviour : ConfiguredScriptBehaviour, IPreDeployBehaviour
    {
        public PreDeployConfiguredScriptBehaviour(ILog log, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner) :
            base(DeploymentStages.PreDeploy, log, fileSystem, scriptEngine, commandLineRunner)
        { }
    }

    public class DeployConfiguredScriptBehaviour : ConfiguredScriptBehaviour, IDeployBehaviour
    {
        public DeployConfiguredScriptBehaviour(ILog log, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner) :
            base(DeploymentStages.Deploy, log, fileSystem, scriptEngine, commandLineRunner)
        { }
    }

    public class PostDeployConfiguredScriptBehaviour : ConfiguredScriptBehaviour, IPostDeployBehaviour
    {
        public PostDeployConfiguredScriptBehaviour(ILog log, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner) :
            base(DeploymentStages.PostDeploy, log, fileSystem, scriptEngine, commandLineRunner)
        { }
    }
}