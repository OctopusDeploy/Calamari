using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;
using Calamari.Shared.Scripting;

namespace Calamari.Integration.Scripting
{
    public class PackagedScriptRunner
    {
        readonly string scriptFilePrefix;
        readonly ICalamariFileSystem fileSystem;
        readonly IScriptEngine scriptEngine;

        public PackagedScriptRunner(string scriptFilePrefix, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine)
        {
            this.scriptFilePrefix = scriptFilePrefix;
            this.fileSystem = fileSystem;
            this.scriptEngine = scriptEngine;
        }

        protected void RunScripts(IExecutionContext deployment)
        {
            var scripts = FindScripts(deployment);

            foreach (var script in scripts)
            {
                Log.VerboseFormat("Executing '{0}'", script);
                var result = scriptEngine.Execute(new Shared.Scripting.Script(script));
                if (result.ExitCode != 0)
                {
                    throw new CommandException(string.Format("Script '{0}' returned non-zero exit code: {1}. Deployment terminated.", script, result.ExitCode));
                }

                if (result.HasErrors && deployment.Variables.GetFlag(SpecialVariables.Action.FailScriptOnErrorOutput, false))
                {
                    throw new CommandException($"Script '{script}' returned zero exit code but had error output. Deployment terminated.");
                }
            }
        }

        protected void DeleteScripts(IExecutionContext deployment)
        {
            var scripts = FindScripts(deployment);

            foreach (var script in scripts)
            {
                fileSystem.DeleteFile(script, FailureOptions.IgnoreFailure);
            }
        }

        IEnumerable<string> FindScripts(IExecutionContext deployment)
        {
            var supportedScriptExtensions = scriptEngine.GetSupportedTypes();
            var searchPatterns = supportedScriptExtensions.Select(e => "*." + e.FileExtension()).ToArray();
            return
                from file in fileSystem.EnumerateFiles(deployment.CurrentDirectory, searchPatterns)
                let nameWithoutExtension = Path.GetFileNameWithoutExtension(file)
                where nameWithoutExtension.Equals(scriptFilePrefix, StringComparison.OrdinalIgnoreCase)
                select file;
        }
    }
}