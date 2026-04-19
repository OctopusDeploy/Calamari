#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD
{
    public record FileHash(string FilePath, string Hash);

    public record FileJsonPatch(string FilePath, string JsonPatch);

    public record TrackedSourceDetail(
        string? CommitSha,
        DateTimeOffset? CommitTimestamp,
        int SourceIndex,
        List<FileHash> ReplacedFiles,
        List<FileJsonPatch> PatchedFiles);

    public class ProcessApplicationResult(
        string gatewayId,
        ApplicationName applicationName,
        int totalSourceCount,
        int matchingSourceCount,
        List<TrackedSourceDetail> trackedSourceDetails,
        HashSet<string> updatedImages,
        HashSet<string> gitReposUpdated)
    {
        public string GatewayId { get; } = gatewayId;
        public ApplicationName ApplicationName { get; } = applicationName;
        public int TotalSourceCount { get; } = totalSourceCount;
        public int MatchingSourceCount { get; } = matchingSourceCount;
        public List<TrackedSourceDetail> TrackedSourceDetails { get; } = trackedSourceDetails;
        public HashSet<string> UpdatedImages { get; } = updatedImages;
        public HashSet<string> GitReposUpdated { get; } = gitReposUpdated;
        public int UpdatedSourceCount => TrackedSourceDetails.Count(s => !string.IsNullOrEmpty(s.CommitSha));
        public bool Tracked => TrackedSourceDetails.Any();
        public bool Updated => UpdatedSourceCount > 0;
    }
}
