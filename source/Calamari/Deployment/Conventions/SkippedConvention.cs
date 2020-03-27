namespace Calamari.Deployment.Conventions
{
    public class SkippedConvention<T> : IInstallConvention
    {
        public void Install(RunningDeployment deployment)
        {
        }

        public override string ToString()
            => $"Skipped: {typeof(T)}";

    }
}