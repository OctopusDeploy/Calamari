using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Common.Features.Scripting;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting
{
    public class PackagedScriptRunner
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;

        public PackagedScriptRunner(ILog log, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.scriptEngine = scriptEngine;
            this.commandLineRunner = commandLineRunner;
        }

        protected void RunPreferredScript(RunningDeployment deployment, string scriptFilePrefix)
        {
            var script = FindPreferredScript(deployment, scriptFilePrefix);

            if (!string.IsNullOrEmpty(script))
            {
                log.VerboseFormat("Executing '{0}'", script);
                var result = scriptEngine.Execute(new Script(script), deployment.Variables, commandLineRunner);
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

        protected void DeleteScripts(RunningDeployment deployment, string scriptFilePrefix)
        {
            var scripts = FindScripts(deployment, scriptFilePrefix);

            foreach (var script in scripts)
            {
                fileSystem.DeleteFile(script, FailureOptions.IgnoreFailure);
            }
        }

        string FindPreferredScript(RunningDeployment deployment, string scriptFilePrefix)
        {
            var supportedScriptExtensions = scriptEngine.GetSupportedTypes();

            var files = (from file in FindScripts(deployment, scriptFilePrefix)
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

        IEnumerable<string> FindScripts(RunningDeployment deployment, string scriptFilePrefix)
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