#nullable enable
using System;
using System.Collections.Generic;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.ArgoCD.Conventions.ManifestTemplating;

public record ManifestUpdateResult(bool Updated, string? CommitSha, DateTimeOffset? CommitTimestamp, List<FileHash> ReplacedFiles);
