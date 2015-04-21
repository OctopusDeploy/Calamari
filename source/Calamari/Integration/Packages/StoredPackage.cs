using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Calamari.Integration.Packages
{
    public class StoredPackage
    {
        public PackageMetadata Metadata { get; set; }
        public string FullPath { get; set; }

        public StoredPackage(PackageMetadata metadata, string fullPath)
        {
            Metadata = metadata;
            FullPath = fullPath;
        }
    }
}
