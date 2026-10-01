using System;
using Calamari.Common.Features.Packages;
using Octopus.Versioning;

namespace Calamari.Commands.Support;

public interface IPackageDownloadOptions
{
    string PackageId { get; set; }
    string PackageVersion { get; set; }
    bool ForcePackageDownload { get; set; }
    string FeedId { get; set; }
    string FeedUri { get; set; }
    string FeedUsername { get; set; }
    string FeedPassword { get; set; }
    string MaxDownloadAttempts { get; set; }
    string AttemptBackoffSeconds { get; set; }
    FeedType FeedType { get; set; }
    VersionFormat VersionFormat { get; set; }
}