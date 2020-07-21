using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;

namespace Calamari.Deployment.Conventions
{
    /// <summary>
    /// This convention is used to detect PreDeploy.ps1, Deploy.ps1 and PostDeploy.ps1 scripts.
    /// </summary>
    public class PackagedScriptConvention : IInstallConvention
    {
        readonly PackagedScriptBehaviour packagedScriptBehaviour;

        public PackagedScriptConvention(PackagedScriptBehaviour packagedScriptBehaviour)
        {
            this.packagedScriptBehaviour = packagedScriptBehaviour;
        }

        public void Install(RunningDeployment deployment)
        {
            if (packagedScriptBehaviour.IsEnabled(deployment))
            {
                packagedScriptBehaviour.Execute(deployment).Wait();
            }
        }
    }
}