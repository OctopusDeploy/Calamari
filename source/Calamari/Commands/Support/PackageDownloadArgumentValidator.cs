using System;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Variables;
using Octopus.Versioning;

namespace Calamari.Commands.Support
{
    public static class PackageDownloadArgumentValidator
    {
        // ReSharper disable UnusedParameter.Local
        public static void CheckArguments(
            string packageId,
            string packageVersion,
            string feedId,
            string feedUri,
            string feedUsername,
            string feedPassword,
            string maxDownloadAttempts,
            string attemptBackoffSeconds,
            FeedType feedType,
            VersionFormat versionFormat,
            IVariables variables,
            out IVersion version,
            out Uri uri,
            out int parsedMaxDownloadAttempts,
            out TimeSpan parsedAttemptBackoff)
        {
            Guard.NotNullOrWhiteSpace(packageId, "No package ID was specified. Please pass --packageId YourPackage");
            Guard.NotNullOrWhiteSpace(packageVersion, "No package version was specified. Please pass --packageVersion 1.0.0.0");
            Guard.NotNullOrWhiteSpace(feedId, "No feed ID was specified. Please pass --feedId feed-id");

            var usingOidc = !string.IsNullOrWhiteSpace(variables.Get("Jwt"));
            if (feedType != FeedType.S3 && feedType != FeedType.AwsElasticContainerRegistry)
            {
                Guard.NotNullOrWhiteSpace(feedUri, "No feed URI was specified. Please pass --feedUri https://url/to/nuget/feed");
            }

            version = VersionFactory.TryCreateVersion(packageVersion, versionFormat);
            if (version == null)
            {
                throw new CommandException($"Package version '{packageVersion}' specified is not a valid {versionFormat.ToString()} version string");
            }

            if (feedType == FeedType.S3 || feedType == FeedType.AwsElasticContainerRegistry)
            {
                uri = null;
            }
            else if (!Uri.TryCreate(feedUri, UriKind.Absolute, out uri))
                throw new CommandException($"URI specified '{feedUri}' is not a valid URI");

            if (!String.IsNullOrWhiteSpace(feedUsername) && String.IsNullOrWhiteSpace(feedPassword) && !usingOidc)
                throw new CommandException("A username was specified but no password was provided. Please pass --feedPassword \"FeedPassword\"");

            if (!int.TryParse(maxDownloadAttempts, out parsedMaxDownloadAttempts))
                throw new CommandException($"The requested number of download attempts '{maxDownloadAttempts}' is not a valid integer number");

            if (parsedMaxDownloadAttempts <= 0)
                throw new CommandException("The requested number of download attempts should be more than zero");

            if (!int.TryParse(attemptBackoffSeconds, out var parsedAttemptBackoffSeconds))
                throw new CommandException($"Retry requested download attempt retry backoff '{attemptBackoffSeconds}' is not a valid integer number of seconds");

            if (parsedAttemptBackoffSeconds < 0)
                throw new CommandException("The requested download attempt retry backoff should be a positive integer number of seconds");

            parsedAttemptBackoff = TimeSpan.FromSeconds(parsedAttemptBackoffSeconds);
        }
    }
}
