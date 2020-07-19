using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Deployment.Conventions
{
    public class RollbackScriptConvention : PackagedScriptRunner, IRollbackConvention
    {
        public RollbackScriptConvention(ILog log, string scriptFilePrefix, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner) :
            base(log, scriptFilePrefix, fileSystem, scriptEngine, commandLineRunner)
        {
        }

        public void Rollback(RunningDeployment deployment)
        {
            RunPreferredScript(deployment);
        }

        public void Cleanup(RunningDeployment deployment)
        {
            if (deployment.Variables.GetFlag(SpecialVariables.DeleteScriptsOnCleanup, true))
            {
                DeleteScripts(deployment);
            }
        }
    }
}