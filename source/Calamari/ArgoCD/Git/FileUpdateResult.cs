using System.Collections.Generic;

namespace Calamari.ArgoCD.Git;

public record FileUpdateResult(HashSet<string> UpdatedFiles, HashSet<string> UpdatedImages, List<FilePathContent> PatchedFileContent);