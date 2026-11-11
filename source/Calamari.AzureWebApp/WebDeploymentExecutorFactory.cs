using System;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureWebApp
{
    public class WebDeploymentExecutorFactory
    {
        readonly Lazy<NetCoreWebDeploymentExecutor> netCoreWebDeploymentExecutor;
        readonly Lazy<PowerShellWebDeploymentExecutor> powerShellWebDeploymentExecutor;

        public WebDeploymentExecutorFactory(
            Lazy<NetCoreWebDeploymentExecutor> netCoreWebDeploymentExecutor,
            Lazy<PowerShellWebDeploymentExecutor> powerShellWebDeploymentExecutor)
        {
            this.netCoreWebDeploymentExecutor = netCoreWebDeploymentExecutor;
            this.powerShellWebDeploymentExecutor = powerShellWebDeploymentExecutor;
        }
        
        public IWebDeploymentExecutor Create(IVariables variables)
        {
            if (OctopusFeatureToggles.AzureWebAppPowerShellScript.IsEnabled(variables))
            {
                return powerShellWebDeploymentExecutor.Value;
            }

            return netCoreWebDeploymentExecutor.Value;
        }
    }
}