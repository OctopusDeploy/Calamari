﻿using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Deployment.Conventions
{
    public class ExecuteScriptConvention : IInstallConvention
    {
        private readonly IScriptEngine scriptEngine;
        private readonly ICommandLineRunner commandLineRunner;
        readonly ILog log;

        public ExecuteScriptConvention(IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner, ILog log)
        {
            this.scriptEngine = scriptEngine;
            this.commandLineRunner = commandLineRunner;
            this.log = log;
        }

        public void Install(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            var scriptFile = Path.Combine(deployment.CurrentDirectory, variables.Get(ScriptVariables.ScriptFileName));
            var scriptParameters = variables.Get(SpecialVariables.Action.Script.ScriptParameters);
            log.VerboseFormat("Executing '{0}'", scriptFile);
            var result = scriptEngine.Execute(new Script(scriptFile, scriptParameters), variables, commandLineRunner, deployment.EnvironmentVariables);

            var exitCode =
                result.ExitCode == 0 && result.HasErrors && variables.GetFlag(SpecialVariables.Action.FailScriptOnErrorOutput)
                    ? -1
                    : result.ExitCode;

            log.SetOutputVariable(SpecialVariables.Action.Script.ExitCode, exitCode.ToString(), variables);
        }
    }
}