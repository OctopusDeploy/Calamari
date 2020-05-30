using Calamari.Common.Features.Scripting;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Deployment.Conventions
{
    /// <summary>
    /// This convention is used to detect PreDeploy.ps1, Deploy.ps1 and PostDeploy.ps1 scripts.
    /// </summary>
    public class PackagedScriptConvention : IInstallConvention
    {
        readonly PackagedScriptService service;
        readonly string scriptFilePrefix;

        public PackagedScriptConvention(PackagedScriptService service, string scriptFilePrefix)
        {
            this.service = service;
            this.scriptFilePrefix = scriptFilePrefix;
        }

        public void Install(RunningDeployment deployment)
        {
            service.Install(deployment, scriptFilePrefix);
        }
    }

    public class PackagedScriptService : PackagedScriptRunner
    {
        readonly ILog log;

        public PackagedScriptService(ILog log, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner) :
            base(log, fileSystem, scriptEngine, commandLineRunner)
        {
            this.log = log;
        }

        public void Install(RunningDeployment deployment, string scriptFilePrefix)
        {
            var runScripts = deployment.Variables.GetFlag(SpecialVariables.Package.RunPackageScripts, true);
            if (!runScripts)
            {
                log.Verbose($"Skipping the running of packaged scripts as {SpecialVariables.Package.RunPackageScripts} is set to false");
                return;
            }

            RunPreferredScript(deployment, scriptFilePrefix);
            if (deployment.Variables.GetFlag(SpecialVariables.DeleteScriptsOnCleanup, true))
            {
                DeleteScripts(deployment, scriptFilePrefix);
            }
        }
    }
}