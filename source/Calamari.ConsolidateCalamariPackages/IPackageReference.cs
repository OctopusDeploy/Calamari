using System;
using System.Collections.Generic;
using Serilog;

namespace Calamari.ConsolidateCalamariPackages
{
    interface IPackageReference
    {
        string Name { get; }
        string Version { get; }
        string PackagePath { get; }
        IReadOnlyList<SourceFile> GetSourceFiles(ILogger log);
    }
}