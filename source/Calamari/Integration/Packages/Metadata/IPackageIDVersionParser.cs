namespace Calamari.Integration.Packages.Metadata
{
    public interface IPackageIDParser
    {
        BasePackageMetadata GetyMetadata(string packageID);
    }
}