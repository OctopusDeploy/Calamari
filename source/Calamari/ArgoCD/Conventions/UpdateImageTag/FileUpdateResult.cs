using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public record FileUpdateResult(HashSet<string> UpdatedImages, List<FileHash> ReplacedFiles, List<FileJsonPatch> PatchedFiles, string[] FilesRemoved)
{
    public bool HasChanges()
    {
        return ReplacedFiles.Count > 0 || PatchedFiles.Count > 0 || FilesRemoved.Length > 0;
    }
}
