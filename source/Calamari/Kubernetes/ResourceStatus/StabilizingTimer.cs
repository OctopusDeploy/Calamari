using Calamari.Common.Plumbing.Logging;

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
        private readonly ILog log;
        
        public StabilizingTimer(ICountdownTimer deploymentTimer, ICountdownTimer stabilizationTimer, ILog log)
        {
            this.deploymentTimer = deploymentTimer;
            this.stabilizationTimer = stabilizationTimer;
            this.log = log;
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
                    log.Verbose($"Resetting stabilization period because the deployment status has changed");
                    stabilizationTimer.Reset();

                    if (newDeploymentStatus != DeploymentStatus.InProgress)
                    {
                        log.Verbose($"Starting stabilization period for status {(newDeploymentStatus == DeploymentStatus.Succeeded ? "Succeeded" : "Failed")}");
                        stabilizationTimer.Start();
                    }
                }
            }
            else if (newDeploymentStatus != DeploymentStatus.InProgress)
            {
                log.Verbose($"Starting stabilization period for status {(newDeploymentStatus == DeploymentStatus.Succeeded ? "Succeeded" : "Failed")}");
                stabilizationTimer.Start();
            }

            return true;
        }

        public bool IsStabilizing() => stabilizationTimer.HasStarted() && !stabilizationTimer.HasCompleted();
    }
}

