using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public record FileUpdateResult(HashSet<string> UpdatedImages, List<FilePathContent> PatchedFileContent, string[]? FilesRemoved = null)
{
    public bool HasChanges() 
    {
        return PatchedFileContent.Count != 0 || FilesRemoved == null ? false : FilesRemoved.Length != 0;
    }
}
