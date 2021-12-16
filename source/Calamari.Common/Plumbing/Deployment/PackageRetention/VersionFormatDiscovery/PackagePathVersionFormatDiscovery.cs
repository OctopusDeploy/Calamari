using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.Variables;
using Octopus.Versioning;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention.VersionFormatDiscovery
{
    public class PackagePathVersionFormatDiscovery : ITryToDiscoverVersionFormat
    {
        public bool TryDiscoverVersionFormat(IManagePackageUse journal, IVariables variables, string[] commandLineArguments, out VersionFormat format, VersionFormat defaultFormat = VersionFormat.Semver)
        {
            var success = false;
            var formatFromPath = VersionFormat.Semver;

            //Use package path info
            var packagePath = variables.Get(TentacleVariables.CurrentDeployment.PackageFilePath);
            if (packagePath != null && PackageName.TryFromFile(packagePath, out var packageFileNameMetadata))
            {
                formatFromPath = packageFileNameMetadata!.Version.Format;
                success = true;
            }

            format = success ? formatFromPath : defaultFormat;
            return success;
        }

        public int Priority => 1;
    }
}