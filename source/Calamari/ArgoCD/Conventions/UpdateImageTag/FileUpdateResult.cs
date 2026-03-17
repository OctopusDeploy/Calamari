using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public record FileUpdateResult(HashSet<string> UpdatedImages, List<FilePathContent> PatchedFileContent, string[] FilesRemoved)
{
    public bool HasChanges() 
    {
        return PatchedFileContent.Count > 0 || (FilesRemoved != null && FilesRemoved.Length > 0);
    }
}
