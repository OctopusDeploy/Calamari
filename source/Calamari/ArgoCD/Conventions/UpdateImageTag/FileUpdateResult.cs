using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public record FileUpdateResult(bool hasChanges, HashSet<string> UpdatedImages, List<FilePathContent> PatchedFileContent, string[]? FilesRemoved = null);