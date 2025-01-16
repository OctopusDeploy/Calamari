using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Serilog;

namespace Octopus.Calamari.ConsolidatedPackage
{
    class CalamariPackageReference : IPackageReference
    {
        private readonly Hasher hasher;
        public string Name { get; }
        public string Version { get; }
        public string PackagePath { get; }

        public CalamariPackageReference(Hasher hasher, BuildPackageReference packageReference)
        {
            this.hasher = hasher;
            Name = packageReference.Name;
            Version = packageReference.Version;
            PackagePath = packageReference.PackagePath;
        }

        public IReadOnlyList<SourceFile> GetSourceFiles(ILogger log)
        {
            var isNetFx = Name == "Calamari";
            var isCloud = Name == "Calamari.Cloud";
            var platform = isNetFx || isCloud
                ? "netfx"
                : Name.Split('.')[1];

            if (!File.Exists(PackagePath))
                throw new Exception($"Could not find the source NuGet package {PackagePath} does not exist");

            using (var zip = ZipFile.OpenRead(PackagePath))
                return zip.Entries
                          .Where(e => !string.IsNullOrEmpty(e.Name))
                          .Where(e => e.FullName != "[Content_Types].xml")
                          .Where(e => !e.FullName.StartsWith("_rels"))
                          .Where(e => !e.FullName.StartsWith("package/services"))
                          .Select(entry => new SourceFile
                          {
                              PackageId = isCloud ? "Calamari.Cloud" : "Calamari",
                              Version = Version,
                              Platform = platform,
                              ArchivePath = PackagePath,
                              IsNupkg = true,
                              FullNameInDestinationArchive = entry.FullName,
                              FullNameInSourceArchive = entry.FullName,
                              Hash = hasher.Hash(entry),
                              FileName = entry.FullName.Split("/").Last(),
                          })
                          .ToArray();
        }
    }
}