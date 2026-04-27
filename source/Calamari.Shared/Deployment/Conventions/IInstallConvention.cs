using Calamari.Common.Commands;

namespace Calamari.Deployment.Conventions
{
    public interface IInstallConvention : IConvention
    {
        void Install(RunningDeployment deployment);
    }
}