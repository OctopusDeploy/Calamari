using System;
using System.IO;

namespace Calamari.Integration.Packages.Download
{
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