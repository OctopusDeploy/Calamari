#if NET

namespace Octopus.Core.Features.Kubernetes.ArgoCD.Models
{
    public record HelmValueFileReference(string Path, string FullReference, ArgoCDRefApplicationSource RefSource);    
}
#endif
