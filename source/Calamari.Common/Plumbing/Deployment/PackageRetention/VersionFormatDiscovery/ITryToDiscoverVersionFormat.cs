using Calamari.Common.Plumbing.Variables;
using Octopus.Versioning;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention.VersionFormatDiscovery
{
    public interface ITryToDiscoverVersionFormat
    {
        bool TryDiscoverVersionFormat(IManagePackageUse journal, IVariables variables, string[] commandLineArguments, out VersionFormat format, VersionFormat defaultFormat = VersionFormat.Semver);
    }
}