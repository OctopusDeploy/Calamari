namespace Calamari.Integration.Packages.Download
{
    /// <summary>
    /// Defines some common utility functions for package downloaders
    /// </summary>
    public interface IPackageDownloaderUtils
    {
        string TentacleHome { get; }
        string RootDirectory { get; }
        string GetPackageRoot(string prefix);
    }
}