using Calamari.Common.Integration.Scripting;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

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