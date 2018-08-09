using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;
using Calamari.Shared.Scripting;

namespace Calamari.Deployment.Conventions
{
    public class RollbackScriptConvention : PackagedScriptRunner, Shared.Commands.IConvention
    {
        public RollbackScriptConvention(string scriptFilePrefix, ICalamariFileSystem fileSystem, IScriptRunner scriptEngine) :
            base(scriptFilePrefix, fileSystem, scriptEngine)
        {            
        }

        public void Cleanup(IExecutionContext deployment)
        {
            if (deployment.Variables.GetFlag(SpecialVariables.DeleteScriptsOnCleanup, true))
            {
                DeleteScripts(deployment);
            }
        }
        public void Run(IExecutionContext context)
        {
            this.RunScripts(context);
        }
    }
}