using System;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Behaviours
{
    class PackagedScriptBehaviour : PackagedScriptRunner, IBehaviour
    {
        public PackagedScriptBehaviour(ILog log, string scriptFilePrefix, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner) :
            base(log, scriptFilePrefix, fileSystem, scriptEngine, commandLineRunner)
        {
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return context.Variables.GetFlag(KnownVariables.Package.RunPackageScripts, true);
        }

        public Task Execute(RunningDeployment context)
        {
            RunPreferredScript(context);
            if (context.Variables.GetFlag(KnownVariables.DeleteScriptsOnCleanup, true))
            {
                DeleteScripts(context);
            }

            return this.CompletedTask();
        }
    }
}