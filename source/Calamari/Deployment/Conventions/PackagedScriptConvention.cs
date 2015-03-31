using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Deployment.Conventions
{
    /// <summary>
    /// This convention is used to detect PreDeploy.ps1, Deploy.ps1 and PostDeploy.ps1 scripts.
    /// </summary>
    public class PackagedScriptConvention : IInstallConvention
    {
        readonly string scriptFilePrefix;
        readonly ICalamariFileSystem fileSystem;
        readonly IScriptEngineSelector scriptEngineSelector;
        readonly ICommandLineRunner commandLineRunner;

        public PackagedScriptConvention(string scriptFilePrefix, ICalamariFileSystem fileSystem, IScriptEngineSelector scriptEngineSelector, ICommandLineRunner commandLineRunner)
        {
            this.scriptFilePrefix = scriptFilePrefix;
            this.fileSystem = fileSystem;
            this.scriptEngineSelector = scriptEngineSelector;
            this.commandLineRunner = commandLineRunner;
        }

        public void Install(RunningDeployment deployment)
        {
            RunScripts(deployment);
            DeleteScripts(deployment);
        }

        void RunScripts(RunningDeployment deployment)
        {
            var scripts = FindScripts(deployment);

            foreach (var script in scripts)
            {
                var engine = scriptEngineSelector.SelectEngine(script);
                var result = engine.Execute(script, deployment.Variables, commandLineRunner);
                if (result.ExitCode != 0)
                {
                    throw new CommandException(string.Format("Script '{0}' returned non-zero exit code: {1}. Deployment terminated.", script, result.ExitCode));
                }
            }
        }

        void DeleteScripts(RunningDeployment deployment)
        {
            var scripts = FindScripts(deployment);

            foreach (var script in scripts)
            {
                fileSystem.DeleteFile(script, DeletionOptions.TryThreeTimesIgnoreFailure);
            }
        }

        IEnumerable<string> FindScripts(RunningDeployment deployment)
        {
            var supportedScriptExtensions = scriptEngineSelector.GetSupportedExtensions();
            var searchPatterns = supportedScriptExtensions.Select(e => "*." + e).ToArray();
            return
                from file in fileSystem.EnumerateFiles(deployment.CurrentDirectory, searchPatterns)
                let nameWithoutExtension = Path.GetFileNameWithoutExtension(file)
                where nameWithoutExtension.Equals(scriptFilePrefix, StringComparison.OrdinalIgnoreCase)
                select file;
        }
    }
}