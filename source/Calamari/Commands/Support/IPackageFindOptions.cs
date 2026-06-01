using Octopus.Versioning;

namespace Calamari.Commands.Support;

public interface IPackageFindOptions
{
    public string PackageId { get; set; }
    public string PackageVersion { get; set; }
    public string PackageHash { get; set; }
    public bool ExactMatchOnly { get; set; }
    public VersionFormat VersionFormat { get; set; }
}