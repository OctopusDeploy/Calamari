using System;

namespace Octopus.Calamari.Contracts.ArgoCD;

public record SourceFileChanges(
    string? CommitSha,
    DateTimeOffset? CommitTimestamp,
    int SourceIndex,
    List<FileHash> ReplacedFiles,
    List<FileJsonPatch> PatchedFiles);