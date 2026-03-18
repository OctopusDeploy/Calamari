using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public record FileUpdateResult(HashSet<string> UpdatedImages, List<FilePathContent> PatchedFileContent);