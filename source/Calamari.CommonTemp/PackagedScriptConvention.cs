using System.Threading.Tasks;
using Calamari.Common.Features.Scripting;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.CommonTemp
{
    internal class PackagedScriptConvention : PackagedScriptRunner, IBehaviour
    {
        readonly ILog log;

        public PackagedScriptConvention(ILog log, string scriptFilePrefix, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner) :
            base(log, scriptFilePrefix, fileSystem, scriptEngine, commandLineRunner)
        {
            this.log = log;
        }

        public Task Execute(RunningDeployment deployment)
        {
            var runScripts = deployment.Variables.GetFlag(KnownVariables.Package.RunPackageScripts, true);
            if (!runScripts)
            {
                log.Verbose($"Skipping the running of packaged scripts as {KnownVariables.Package.RunPackageScripts} is set to false");
                return this.CompletedTask();
            }

            RunPreferredScript(deployment);
            if (deployment.Variables.GetFlag(KnownVariables.DeleteScriptsOnCleanup, true))
            {
                DeleteScripts(deployment);
            }

            return this.CompletedTask();
        }
    }
}