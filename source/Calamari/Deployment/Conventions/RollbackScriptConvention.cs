using Calamari.Common.Features.Scripting;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Deployment.Conventions
{
    public class RollbackScriptConvention : PackagedScriptRunner, IRollbackConvention, ICleanupConvention
    {
        readonly string scriptFilePrefix;

        public RollbackScriptConvention(ILog log, string scriptFilePrefix, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner) :
            base(log, fileSystem, scriptEngine, commandLineRunner)
        {
            this.scriptFilePrefix = scriptFilePrefix;
        }

        public void Rollback(RunningDeployment deployment)
        {
            RunPreferredScript(deployment, scriptFilePrefix);
        }

        public void Cleanup(RunningDeployment deployment)
        {
            if (deployment.Variables.GetFlag(SpecialVariables.DeleteScriptsOnCleanup, true))
            {
                DeleteScripts(deployment, scriptFilePrefix);
            }
        }
    }
}