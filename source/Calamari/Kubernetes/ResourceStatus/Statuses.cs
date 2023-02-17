namespace Calamari.Kubernetes.ResourceStatus;

// TODO Do we need the status of a step to be separate from the status of individual resources?
public enum ActionStatus
{
    InProgress, Successful, Failed, Stabilizing, Recovering
}

public enum ResourceStatus
{
    InProgress, Successful, Failed
}