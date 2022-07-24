using System;
using System.Collections.Generic;
using Serilog;

namespace Calamari.Build.ConsolidateCalamariPackages
{
    interface IPackageReference
    {
        string Name { get; }
        string Version { get; }
        string PackagePath { get; }
        IReadOnlyList<SourceFile> GetSourceFiles(ILogger log);
    }
}