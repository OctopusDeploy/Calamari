using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention;
using Octopus.Versioning;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention.VersionFormatDiscovery
{
    public class JournalVersionFormatDiscovery : ITryToDiscoverVersionFormat
    {
        public bool TryDiscoverVersionFormat(IManagePackageUse journal, IVariables variables, string[] commandLineArguments, out VersionFormat format, VersionFormat defaultFormat = VersionFormat.Semver)
        {
            var success = false;
            var formatFromJournal = VersionFormat.Semver;

            var packageStr = variables.Get(PackageVariables.PackageId);
            var versionStr = variables.Get(PackageVariables.PackageVersion);
            var serverTaskIdStr = variables.Get(KnownVariables.ServerTask.Id);

            if (packageStr != null)
            {
                success = serverTaskIdStr != null && journal.TryGetVersionFormat(new PackageId(packageStr), new ServerTaskId(serverTaskIdStr), defaultFormat, out formatFromJournal)
                            || versionStr != null && journal.TryGetVersionFormat(new PackageId(packageStr), versionStr, defaultFormat, out formatFromJournal) ;

            }

            format = success ? formatFromJournal : defaultFormat;
            return success;
        }
    }
}