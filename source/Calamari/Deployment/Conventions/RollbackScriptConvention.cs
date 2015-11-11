using System;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

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
        }
    }
}