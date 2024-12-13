using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Scripting.FSharp
{
    public class FSharpExecutor : ScriptExecutor
    {
        readonly ILog log;
        
        public FSharpExecutor(ILog log)
        {
            this.log = log;
        }
        
        protected override IEnumerable<ScriptExecution> PrepareExecution(Script script,
            IVariables variables,
            Dictionary<string, string>? environmentVars = null)
        {
            //if the feature toggle to disable FSharp scripts is enabled, just blow up
            if (FeatureToggle.DisableFSharpScriptExecutionFeatureToggle.IsEnabled(variables))
            {
                throw new CommandException("FSharp scripts are no longer supported");
            }
            
            LogFSharpDeprecationWarning(variables);
            
            var workingDirectory = Path.GetDirectoryName(script.File);
            var executable = FSharpBootstrapper.FindExecutable();
            var configurationFile = FSharpBootstrapper.PrepareConfigurationFile(workingDirectory, variables);
            var (bootstrapFile, otherTemporaryFiles) = FSharpBootstrapper.PrepareBootstrapFile(script.File, configurationFile, workingDirectory, variables);
            var arguments = FSharpBootstrapper.FormatCommandArguments(bootstrapFile, script.Parameters);

            yield return new ScriptExecution(
                new CommandLineInvocation(executable, arguments)
                {
                    WorkingDirectory = workingDirectory,
                    EnvironmentVars = environmentVars
                },
                otherTemporaryFiles.Concat(new[] { bootstrapFile, configurationFile })
            );
        }
        
        void LogFSharpDeprecationWarning(IVariables variables)
        {
            if (FeatureToggle.FSharpDeprecationFeatureToggle.IsEnabled(variables))
            {
                log.Warn($"Executing FSharp scripts will soon be deprecated. Please read our deprecation {log.FormatLink("https://oc.to/fsharp-deprecation", "blog post")} for more details.");
            }
        }
    }
}