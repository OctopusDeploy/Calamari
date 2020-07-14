using System.Threading.Tasks;
using Calamari.Common.Features.Scripting;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.CommonTemp
{
    internal class PackagedScriptBehaviour : PackagedScriptRunner, IBehaviour
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