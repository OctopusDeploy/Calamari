namespace Octopus.Deploy.PackageInstaller
{
    public interface IRollbackConvention : IConvention
    {
        void Rollback(RunningDeployment deployment);
        void Cleanup(RunningDeployment deployment);
    }
}