using System;

namespace Octopus.Calamari.Contracts.ArgoCD;

public static class ServiceMessages
{
    public static class ArgoCDFilesUpdated
    {
        public const string Name = "argocd-files-updated";

        public static class Attributes
        {
            public const string GatewayId = "gatewayId";
            public const string ApplicationName = "applicationName";
            public const string KubernetesNamespace = "kubernetesNamespace";
            public const string Sources = "sources";
        }
    }
}