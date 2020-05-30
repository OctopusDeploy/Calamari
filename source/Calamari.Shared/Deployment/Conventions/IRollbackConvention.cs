namespace Calamari.Deployment.Conventions
{
    public interface IRollbackConvention : IConvention
    {
        void Rollback(RunningDeployment deployment);
    }

    public interface ICleanupConvention : IConvention
    {
        void Cleanup(RunningDeployment deployment);
    }
}