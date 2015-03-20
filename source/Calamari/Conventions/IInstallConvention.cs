namespace Octopus.Deploy.PackageInstaller
{
    public interface IInstallConvention : IConvention
    {
        void Install(RunningDeployment deployment);
    }
}