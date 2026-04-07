#nullable enable
using System;
using System.Collections.Generic;
using Calamari.ArgoCD.Git;

namespace Calamari.ArgoCD.Conventions;
    
public record RepositoryUpdates(HashSet<string> ImagesUpdated, PushResult? PushResult, List<FileHash> ReplacedFiles, List<FileJsonPatch> PatchedFiles)
{
    public bool Updated => PushResult != null;
}
