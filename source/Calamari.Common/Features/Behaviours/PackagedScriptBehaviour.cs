using System;
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
    public abstract class PackagedScriptBehaviour : PackagedScriptRunner, IBehaviour
    {
        protected PackagedScriptBehaviour(ILog log, string scriptFilePrefix, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner) :
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

    public class PreDeployPackagedScriptBehaviour : PackagedScriptBehaviour, IPreDeployBehaviour
    {
        public PreDeployPackagedScriptBehaviour(ILog log, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner) :
            base(log, DeploymentStages.PreDeploy, fileSystem, scriptEngine, commandLineRunner)
        { }
    }

    public class DeployPackagedScriptBehaviour : PackagedScriptBehaviour, IDeployBehaviour
    {
        public DeployPackagedScriptBehaviour(ILog log, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner) :
            base(log, DeploymentStages.Deploy, fileSystem, scriptEngine, commandLineRunner)
        { }
    }

    public class PostDeployPackagedScriptBehaviour : PackagedScriptBehaviour, IPostDeployBehaviour
    {
        public PostDeployPackagedScriptBehaviour(ILog log, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner) :
            base(log, DeploymentStages.PostDeploy, fileSystem, scriptEngine, commandLineRunner)
        { }
    }
}