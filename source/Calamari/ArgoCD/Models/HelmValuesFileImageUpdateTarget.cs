#if NET
using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Models;

public record HelmValuesFileImageUpdateTarget(
    string AppName,
    string DefaultClusterRegistry,
    string Path,
    Uri RepoUrl,
    string TargetRevision,
    string FileName,
    List<string> ImagePathDefinitions) : ArgoCDImageUpdateTarget(AppName, DefaultClusterRegistry, Path, RepoUrl, TargetRevision);

// Allows us to pass issues up the chain for logging without pushing an ITaskLog all the way down the stack
public record InvalidHelmValuesFileImageUpdateTarget(
    string AppName,
    string DefaultClusterRegistry,
    string Path,
    Uri RepoUrl,
    string TargetRevision,
    string FileName,
    string Alias)
    : HelmValuesFileImageUpdateTarget(AppName, DefaultClusterRegistry, Path, RepoUrl, TargetRevision,
        FileName, new List<string>());
#endif