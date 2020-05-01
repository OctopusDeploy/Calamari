using Calamari.Common.Features.Scripting;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Deployment.Conventions
{
    /// <summary>
    /// This convention is used to detect PreDeploy.ps1, Deploy.ps1 and PostDeploy.ps1 scripts.
    /// </summary>
    public class PackagedScriptConvention : PackagedScriptRunner, IInstallConvention
    {
        readonly ILog log;

        public PackagedScriptConvention(ILog log, string scriptFilePrefix, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner) : 
            base(log, scriptFilePrefix, fileSystem, scriptEngine, commandLineRunner)
        {
            this.log = log;
        }

        public void Install(RunningDeployment deployment)
        {
            var runScripts = deployment.Variables.GetFlag(SpecialVariables.Package.RunPackageScripts, true);
            if (!runScripts)
            {
                log.Verbose($"Skipping the running of packaged scripts as {SpecialVariables.Package.RunPackageScripts} is set to false");
                return;
            }

            RunPreferredScript(deployment);
            if (deployment.Variables.GetFlag(SpecialVariables.DeleteScriptsOnCleanup, true))
            {
                DeleteScripts(deployment);
            }
        }
    }
}