using Calamari.Common.Commands;

namespace Calamari.Deployment.Conventions
{
    public interface IRollbackConvention : IConvention
    {
        void Rollback(RunningDeployment deployment);
        void Cleanup(RunningDeployment deployment);
    }
}