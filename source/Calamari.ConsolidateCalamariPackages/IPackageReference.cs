using System;
using System.Collections.Generic;
using Serilog;

namespace Octopus.Calamari.ConsolidatedPackage
{
    interface IPackageReference
    {
        string Name { get; }
        string Version { get; }
        string PackagePath { get; }
        IReadOnlyList<SourceFile> GetSourceFiles(ILogger log);
    }
}