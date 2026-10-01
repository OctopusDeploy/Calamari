using System;

namespace Calamari.ArgoCD.Domain
{
    public enum SourceType
    {
        Directory,
        Helm,
        Kustomize,
        Plugin
    }
}