namespace Calamari.Conventions
{
    public interface IInstallConvention : IConvention
    {
        void Install(RunningDeployment deployment);
    }
}