namespace Octopus.Deploy.PackageInstaller
{
    public class DeployScriptConvention : IInstallConvention
    {
        readonly string scriptFilePrefix;

        public DeployScriptConvention(string scriptFilePrefix)
        {
            this.scriptFilePrefix = scriptFilePrefix;
        }

        public void Install(RunningDeployment deployment)
        {
            // Find the scripts by name, 
            // Based on the extension, call the appropriate script runner?
        }
    }
}