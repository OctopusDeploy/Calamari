using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Calamari.Integration.Packages
{
    public class StoredPackage
    {
        public ExtendedPackageMetadata Metadata { get; set; }
        public string FullPath { get; set; }

        public StoredPackage(ExtendedPackageMetadata metadata, string fullPath)
        {
            Metadata = metadata;
            FullPath = fullPath;
        }
    }
}
