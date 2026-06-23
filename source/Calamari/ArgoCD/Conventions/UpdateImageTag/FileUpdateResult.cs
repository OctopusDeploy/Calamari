using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public record FileUpdateResult(HashSet<string> UpdatedImages, List<FileHash> ReplacedFiles, List<FileJsonPatch> PatchedFiles, string[] FilesRemoved)
{
    public static FileUpdateResult EmptyFileUpdateResult => new([], [], [], []);
    public bool HasChanges()
    {
        return ReplacedFiles.Count > 0 || PatchedFiles.Count > 0 || FilesRemoved.Length > 0;
    }

    // Combines the file changes from several sources so they can be described in a single commit
    // (used when committing all of an application's sources in one repo+branch group together).
    public static FileUpdateResult Merge(IEnumerable<FileUpdateResult> results)
    {
        var materialised = results.ToList();
        return new FileUpdateResult(
                                    materialised.SelectMany(r => r.UpdatedImages).ToHashSet(),
                                    materialised.SelectMany(r => r.ReplacedFiles).ToList(),
                                    materialised.SelectMany(r => r.PatchedFiles).ToList(),
                                    materialised.SelectMany(r => r.FilesRemoved).ToArray());
    }
}


