using Calamari.Integration.Scripting;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;
using Calamari.Shared.Scripting;

namespace Calamari.Deployment.Conventions
{
    /// <summary>
    /// This convention is used to detect PreDeploy.ps1, Deploy.ps1 and PostDeploy.ps1 scripts.
    /// </summary>
    public class PackagedScriptConvention : PackagedScriptRunner, Calamari.Shared.Commands.IConvention
    {
        public PackagedScriptConvention(string scriptFilePrefix, ICalamariFileSystem fileSystem, IScriptRunner scriptEngine) : 
            base(scriptFilePrefix, fileSystem, scriptEngine)
        {
        }

//        public void Install(RunningDeployment deployment)
//        {
//            var runScripts = deployment.Variables.GetFlag(SpecialVariables.Package.RunPackageScripts, true);
//            if (!runScripts)
//            {
//                Log.Verbose($"Skipping the running of packaged scripts as {SpecialVariables.Package.RunPackageScripts} is set to false");
//                return;
//            }
//
//            RunScripts(deployment);
//            if (deployment.Variables.GetFlag(SpecialVariables.DeleteScriptsOnCleanup, true))
//            {
//                DeleteScripts(deployment);
//            }
//        }

        public void Run(IExecutionContext deployment)
        {
            var runScripts = deployment.Variables.GetFlag(SpecialVariables.Package.RunPackageScripts, true);
            if (!runScripts)
            {
                Log.Verbose($"Skipping the running of packaged scripts as {SpecialVariables.Package.RunPackageScripts} is set to false");
                return;
            }

            RunScripts(deployment);
            if (deployment.Variables.GetFlag(SpecialVariables.DeleteScriptsOnCleanup, true))
            {
                DeleteScripts(deployment);
            }
        }
    }
}