using System;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Behaviours
{
    public class RollbackScriptBehaviour : PackagedScriptRunner, IBehaviour
    {
        public RollbackScriptBehaviour(ILog log, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner) :
            base(log, DeploymentStages.DeployFailed, fileSystem, scriptEngine, commandLineRunner)
        {
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
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