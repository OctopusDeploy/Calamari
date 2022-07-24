using System;

namespace Calamari.Build.ConsolidateCalamariPackages
{
    public class BuildPackageReference
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string PackagePath { get; set; }
    }
}