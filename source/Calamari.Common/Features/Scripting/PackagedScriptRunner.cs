using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Scripting
{
    public class PackagedScriptRunner
    {
        readonly ILog log;
        readonly string scriptFilePrefix;
        readonly ICalamariFileSystem fileSystem;
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;

        protected PackagedScriptRunner(ILog log, string scriptFilePrefix, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner)
        {
            this.log = log;
            this.scriptFilePrefix = scriptFilePrefix;
            this.fileSystem = fileSystem;
            this.scriptEngine = scriptEngine;
            this.commandLineRunner = commandLineRunner;
        }

        protected void RunPreferredScript(RunningDeployment deployment)
        {
            var script = FindPreferredScript(deployment);

            if (!string.IsNullOrEmpty(script))
            {
                log.VerboseFormat("Executing '{0}'", script);
                var result = scriptEngine.Execute(new Script(script), deployment.Variables, commandLineRunner);
                if (result.ExitCode != 0)
                {
                    throw new CommandException(string.Format("Script '{0}' returned non-zero exit code: {1}. Deployment terminated.", script, result.ExitCode));
                }

                if (result.HasErrors && deployment.Variables.GetFlag(KnownVariables.Action.FailScriptOnErrorOutput, false))
                {
                    throw new CommandException($"Script '{script}' returned zero exit code but had error output. Deployment terminated.");
                }
            }
        }

        protected void DeleteScripts(RunningDeployment deployment)
        {
            var scripts = FindScripts(deployment);

            foreach (var script in scripts)
            {
                fileSystem.DeleteFile(script, FailureOptions.IgnoreFailure);
            }
        }

        string FindPreferredScript(RunningDeployment deployment)
        {
            var supportedScriptExtensions = scriptEngine.GetSupportedTypes();

            var files = (from file in FindScripts(deployment)
                let preferenceOrdinal = Array.IndexOf(supportedScriptExtensions, file.ToScriptType())
                orderby preferenceOrdinal
                select file).ToArray();

            var numFiles = files.Count();
            var selectedFile = files.FirstOrDefault();

            if (numFiles > 1)
            {
                var preferenceOrderDisplay = string.Join(", ", supportedScriptExtensions);
                log.Verbose($"Found {numFiles} {scriptFilePrefix} scripts. Selected {selectedFile} based on OS preferential ordering: {preferenceOrderDisplay}");
            }

            return selectedFile;
        }

        IEnumerable<string> FindScripts(RunningDeployment deployment)
        {
            var supportedScriptExtensions = scriptEngine.GetSupportedTypes();
            var searchPatterns = supportedScriptExtensions.Select(e => "*." + e.FileExtension()).ToArray();

            return from file in fileSystem.EnumerateFiles(deployment.CurrentDirectory, searchPatterns)
                let nameWithoutExtension = Path.GetFileNameWithoutExtension(file)
                where nameWithoutExtension.Equals(scriptFilePrefix, StringComparison.OrdinalIgnoreCase)
                select file;
        }
    }
}