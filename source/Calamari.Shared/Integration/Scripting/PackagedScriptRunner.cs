using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting
{
    public class PackagedScriptRunner
    {
        readonly string scriptFilePrefix;
        readonly ICalamariFileSystem fileSystem;
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;

        public PackagedScriptRunner(string scriptFilePrefix, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner)
        {
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
                Log.VerboseFormat("Executing '{0}'", script);
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

        protected void DeletePreferredScript(RunningDeployment deployment)
        {
            var script = FindPreferredScript(deployment);

            if (!string.IsNullOrEmpty(script))
            {
                fileSystem.DeleteFile(script, FailureOptions.IgnoreFailure);
            }
        }

        string FindPreferredScript(RunningDeployment deployment)
        {
            var supportedScriptExtensions = scriptEngine.GetSupportedTypes();
            var searchPatterns = supportedScriptExtensions.Select(e => "*." + e.FileExtension()).ToArray();

            var files = from file in fileSystem.EnumerateFiles(deployment.CurrentDirectory, searchPatterns)
                        let nameWithoutExtension = Path.GetFileNameWithoutExtension(file)
                        let preferenceOrdinal = Array.IndexOf(supportedScriptExtensions, file.ToScriptType())
                        where nameWithoutExtension.Equals(scriptFilePrefix, StringComparison.OrdinalIgnoreCase)
                        orderby preferenceOrdinal
                        select file;

            var numFiles = files.Count();
            var selectedFile = files.FirstOrDefault();

            if (numFiles > 1)
            {
                var preferenceOrderDisplay = string.Join(" -> ", supportedScriptExtensions);
                Log.Verbose($"Found {numFiles} {scriptFilePrefix} scripts. Selected {selectedFile} based on OS preferential ordering: {preferenceOrderDisplay}");
            }

            return selectedFile;
        }
    }
}