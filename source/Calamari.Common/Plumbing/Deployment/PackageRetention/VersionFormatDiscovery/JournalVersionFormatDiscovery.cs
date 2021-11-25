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
            //TODO: should this include server task id too?

            if (packageStr != null && versionStr != null)
            {
                success = journal.TryGetVersionFormat(new PackageId(packageStr), packageStr, out formatFromJournal);
            }

            format = success ? formatFromJournal : defaultFormat;
            return success;
        }
    }
}