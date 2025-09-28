using System.Collections.Generic;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Common.Features.Scripting.DotnetScript
{
    public class DotnetScriptCompilerWarningWrapper : IScriptWrapper
    {
        public const string WarningLogLine = "dotnet-script failed to execute the script. This may be due to syntax differences between dotnet-script and ScriptCS. As of 2025.4, ScriptCS is [no longer supported](https://oc.to/scriptcs-deprecation).";
        
        readonly ILog log;
        readonly DotnetScriptCompilationWarningOutputSink outputSink;
        
        public int Priority => ScriptWrapperPriorities.DotnetScriptCompileWarning;

        public IScriptWrapper? NextWrapper { get; set; }

        public bool IsEnabled(ScriptSyntax syntax) => syntax == ScriptSyntax.CSharp;

        public DotnetScriptCompilerWarningWrapper(ILog log, DotnetScriptCompilationWarningOutputSink outputSink)
        {
            this.log = log;
            this.outputSink = outputSink;
        }

        public CommandResult ExecuteScript(Script script, ScriptSyntax scriptSyntax, ICommandLineRunner commandLineRunner, Dictionary<string, string>? environmentVars)
        {
            var result = NextWrapper!.ExecuteScript(script, scriptSyntax, commandLineRunner, environmentVars);

            //there was a failure
            if (result.ExitCode != 0 && !outputSink.SuccessfullyCompiled)
            {
                log.Warn(WarningLogLine);
            }

            return result;
        }
    }
}