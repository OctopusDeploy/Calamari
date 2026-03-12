using System.Collections.Generic;

namespace Calamari.ArgoCD.Git;

public record FileUpdateResult(HashSet<string> UpdatedImages, List<FilePathContent> PatchedFileContent);