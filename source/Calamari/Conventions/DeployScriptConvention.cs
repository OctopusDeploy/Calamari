using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Conventions
{
    /// <summary>
    /// This convention is used to detect PreDeploy.ps1, Deploy.ps1 and PostDeploy.ps1 scripts.
    /// </summary>
    public class DeployScriptConvention : IInstallConvention
    {
        readonly string scriptFilePrefix;
        readonly ICalamariFileSystem fileSystem;

        public DeployScriptConvention(string scriptFilePrefix, ICalamariFileSystem fileSystem)
        {
            this.scriptFilePrefix = scriptFilePrefix;
            this.fileSystem = fileSystem;
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
                var engine = ScriptEngineSelector.SelectEngine(script);
                var result = engine.Execute(script, deployment.Variables, new CommandLineRunner(new ConsoleCommandOutput()));

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
            var scripts = fileSystem.EnumerateFiles(deployment.CurrentDirectory, ScriptEngineSelector.GetSupportedExtensions().Select(e => "*." + e).ToArray()).ToArray();
            scripts = scripts.Where(s => Path.GetFileNameWithoutExtension(s).Equals(scriptFilePrefix, StringComparison.InvariantCultureIgnoreCase)).ToArray();
            return scripts;
        }
    }
}