#nullable enable
using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Conventions;

public record SourceUpdateResult(HashSet<string> ImagesUpdated, string CommitSha, List<FilePathContent> PatchedFiles);