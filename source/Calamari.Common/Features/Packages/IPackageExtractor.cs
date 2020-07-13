using System;

namespace Calamari.Common.Features.Packages
{
    public interface IPackageExtractor
    {
        string[] Extensions { get; }
        int Extract(string packageFile, string directory);
    }
}