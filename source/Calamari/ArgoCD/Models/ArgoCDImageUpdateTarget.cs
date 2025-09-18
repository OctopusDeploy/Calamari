#if NET
using System;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models;

public record ArgoCDImageUpdateTarget(
    string Name,
    string DefaultClusterRegistry,
    string Path,
    Uri RepoUrl,
    string TargetRevision);
#endif