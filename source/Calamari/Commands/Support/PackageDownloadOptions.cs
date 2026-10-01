using System;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.Commands.Options;
using Octopus.Versioning;

namespace Calamari.Commands.Support
{
    public class PackageDownloadOptions : IPackageDownloadOptions
    {
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public bool ForcePackageDownload { get; set; }
        public string FeedId { get; set; }
        public string FeedUri { get; set; }
        public string FeedUsername { get; set; }
        public string FeedPassword { get; set; }
        public string MaxDownloadAttempts { get; set; } = "5";
        public string AttemptBackoffSeconds { get; set; } = "10";
        public FeedType FeedType { get; set; } = FeedType.NuGet;
        public VersionFormat VersionFormat { get; set; } = VersionFormat.Semver;

        public static void ConfigureOptions(OptionSet options, IPackageDownloadOptions downloadOptions)
        {
            options.Add("packageId=", "Package ID to download", v => downloadOptions.PackageId = v);
            options.Add("packageVersion=", "Package version to download", v => downloadOptions.PackageVersion = v);
            options.Add("packageVersionFormat=", $"[Optional] Format of version. Options {string.Join(", ", Enum.GetNames(typeof(VersionFormat)))}. Defaults to `{VersionFormat.Semver}`.",
                v =>
                {
                    if (!Enum.TryParse(v, out VersionFormat format))
                    {
                        throw new CommandException($"The provided version format `{format}` is not recognised.");
                    }
                    downloadOptions.VersionFormat = format;
                });
            options.Add("feedId=", "Id of the feed", v => downloadOptions.FeedId = v);
            options.Add("feedUri=", "URL to feed", v => downloadOptions.FeedUri = v);
            options.Add("feedUsername=", "[Optional] Username to use for an authenticated feed", v => downloadOptions.FeedUsername = v);
            options.Add("feedPassword=", "[Optional] Password to use for an authenticated feed", v => downloadOptions.FeedPassword = v);
            options.Add("feedType=", $"[Optional] Type of feed. Options {string.Join(", ", Enum.GetNames(typeof(FeedType)))}. Defaults to `{FeedType.NuGet}`.",
                v =>
                {
                    if (!Enum.TryParse(v, out FeedType type))
                    {
                        throw new CommandException($"The provided feed type `{type}` is not recognised.");
                    }

                    downloadOptions.FeedType = type;
                });
            options.Add("attempts=", $"[Optional] The number of times to attempt downloading the package. Default: {downloadOptions.MaxDownloadAttempts}", v => downloadOptions.MaxDownloadAttempts = v);
            options.Add("attemptBackoffSeconds=", $"[Optional] The number of seconds to apply as a linear backoff between each download attempt. Default: {downloadOptions.AttemptBackoffSeconds}", v => downloadOptions.AttemptBackoffSeconds = v);
            options.Add("forcePackageDownload", "[Optional, Flag] if specified, the package will be downloaded even if it is already in the package cache", v => downloadOptions.ForcePackageDownload = true);
        }
    }
}
