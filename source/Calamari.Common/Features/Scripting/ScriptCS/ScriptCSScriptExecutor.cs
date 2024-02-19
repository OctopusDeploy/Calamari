using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Scripting.ScriptCS
{
    public class ScriptCSScriptExecutor : ScriptExecutor
    {
        protected override IEnumerable<ScriptExecution> PrepareExecution(Script script,
                                                                         IVariables variables,
                                                                         ICommandLineRunner commandLineRunner,
                                                                         Dictionary<string, string>? environmentVars = null)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);
            var executable = ScriptCSBootstrapper.FindExecutable();
            var configurationFile = ScriptCSBootstrapper.PrepareConfigurationFile(workingDirectory, variables);
            var (bootstrapFile, otherTemporaryFiles) = ScriptCSBootstrapper.PrepareBootstrapFile(script.File, configurationFile, workingDirectory, variables);
            var arguments = ScriptCSBootstrapper.FormatCommandArguments(bootstrapFile, script.Parameters);

            yield return new ScriptExecution(
                                             new CommandLineInvocation(executable, arguments)
                                             {
                                                 WorkingDirectory = workingDirectory,
                                                 EnvironmentVars = environmentVars
                                             },
                                             otherTemporaryFiles.Concat(new[] { bootstrapFile, configurationFile })
                                            );
        }
    }
}