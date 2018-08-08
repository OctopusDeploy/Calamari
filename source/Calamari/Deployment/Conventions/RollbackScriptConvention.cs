using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Shared;
using Calamari.Shared.FileSystem;
using Calamari.Shared.Scripting;

namespace Calamari.Deployment.Conventions
{
    public class RollbackScriptConvention : PackagedScriptRunner, IRollbackConvention
    {
        public RollbackScriptConvention(string scriptFilePrefix, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner) :
            base(scriptFilePrefix, fileSystem, scriptEngine, commandLineRunner)
        {            
        }

        public void Rollback(RunningDeployment deployment)
        {
            RunScripts(deployment);
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