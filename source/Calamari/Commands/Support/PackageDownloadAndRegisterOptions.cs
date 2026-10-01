using System;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.Commands.Options;
using Octopus.Versioning;

namespace Calamari.Commands.Support
{
    public class PackageDownloadAndRegisterOptions : IPackageDownloadOptions
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
        public string TaskId { get; set; }

        public static void ConfigureOptions(OptionSet options, PackageDownloadAndRegisterOptions downloadOptions)
        {
            PackageDownloadOptions.ConfigureOptions(options, downloadOptions);
            options.Add("taskId=", "No task ID was specified.", v => downloadOptions.TaskId = v);
        }
    }
}
