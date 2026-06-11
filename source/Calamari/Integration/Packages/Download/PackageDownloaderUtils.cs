using System;
using System.IO;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Integration.Packages.Download
{
    /// <summary>
    /// Some common implementations used by package downloaders to find paths
    /// to store and search for artifacts.
    /// </summary>
    public class PackageDownloaderUtils : IPackageDownloaderUtils
    {
        public string RootDirectory => Path.Combine(TentacleHome, "Files");

        public string TentacleHome
        {
            get
            {
                var tentacleHome = Environment.GetEnvironmentVariable("TentacleHome");
                if (tentacleHome == null)
                {
                    Log.Error("Environment variable 'TentacleHome' has not been set.");
                    throw new InvalidOperationException("Environment variable 'TentacleHome' has not been set.");
                }
                return tentacleHome;
            }
        }

        public string GetPackageRoot(string prefix)
        {
            return string.IsNullOrWhiteSpace(prefix) ? RootDirectory : Path.Combine(RootDirectory, prefix);
        }
    }
}