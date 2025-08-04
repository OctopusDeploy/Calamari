using System;

namespace Calamari.ArgoCD.Commands.Executors.Models;

public record ArgoCDImageUpdateTarget(
    string Name,
    string DefaultClusterRegistry,
    string Path,
    Uri RepoUrl,
    string TargetRevision,
    ArgoCDGatewayId GatewayId)
{
    public static ArgoCDImageUpdateTarget FromAppSource(
        string appName,
        ArgoCDApplicationSource appSource,
        ArgoCDGatewayId gatewayId,
        string defaultClusterRegistry = ArgoCDConstants.DefaultContainerRegistry)
    {
        return new ArgoCDImageUpdateTarget(
            appName,
            defaultClusterRegistry,
            appSource.Path,
            new Uri(appSource.RepoUrl),
            appSource.TargetRevision, 
            gatewayId);
    }
};
