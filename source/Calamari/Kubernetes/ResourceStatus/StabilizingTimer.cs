namespace Calamari.Kubernetes.ResourceStatus
{
    public interface IStabilizingTimer
    {
        void Start();
        bool ShouldContinue(DeploymentStatus oldStatus, DeploymentStatus newStatus);
        bool IsStabilizing();
    }
    
    public class StabilizingTimer: IStabilizingTimer
    {
        private readonly ICountdownTimer deploymentTimer;
        private readonly ICountdownTimer stabilizationTimer;
        
        public StabilizingTimer(ICountdownTimer deploymentTimer, ICountdownTimer stabilizationTimer)
        {
            this.deploymentTimer = deploymentTimer;
            this.stabilizationTimer = stabilizationTimer;
        }

        public void Start() => deploymentTimer.Start();
        
        public bool ShouldContinue(
            DeploymentStatus oldDeploymentStatus,
            DeploymentStatus newDeploymentStatus)
        {
            if (deploymentTimer.HasCompleted() || stabilizationTimer.HasCompleted())
            {
                return false;
            }

            if (stabilizationTimer.HasStarted())
            {
                if (newDeploymentStatus != oldDeploymentStatus)
                {
                    stabilizationTimer.Reset();

                    if (newDeploymentStatus != DeploymentStatus.InProgress)
                    {
                        stabilizationTimer.Start();
                    }
                }
            }
            else if (newDeploymentStatus != DeploymentStatus.InProgress)
            {
                stabilizationTimer.Start();
            }

            return true;
        }

        public bool IsStabilizing() => stabilizationTimer.HasStarted() && !stabilizationTimer.HasCompleted();
    }
}

