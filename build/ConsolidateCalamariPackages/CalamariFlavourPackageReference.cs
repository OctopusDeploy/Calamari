using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Serilog;

namespace Calamari.Build.ConsolidateCalamariPackages
{
    class CalamariFlavourPackageReference : IPackageReference
    {
        private readonly Hasher hasher;
        public string Name { get; }
        public string Version { get; }
        public string PackagePath { get; }

        public CalamariFlavourPackageReference(Hasher hasher, BuildPackageReference packageReference)
        {
            this.hasher = hasher;
            Name = packageReference.Name;
            Version = packageReference.Version;
            PackagePath = packageReference.PackagePath;
        }
        
        public IReadOnlyList<SourceFile> GetSourceFiles(ILogger log)
        {
            using (var zip = ZipFile.OpenRead(PackagePath))
                return zip.Entries
                          .Where(e => !string.IsNullOrEmpty(e.Name))
                          .Select(entry =>
                                  {
                                      var parts = entry.FullName.Split('/');
                                      return new SourceFile
                                      {
                                          PackageId = Path.GetFileNameWithoutExtension(PackagePath),
                                          Version = Version,
                                          Platform = parts[0],
                                          ArchivePath = PackagePath,
                                          IsNupkg = false,
                                          FullNameInDestinationArchive = string.Join("/", parts.Skip(1)),
                                          FullNameInSourceArchive = entry.FullName,
                                          Hash = hasher.Hash(entry)
                                      };
                                  })
                          .ToArray();
        }
    }
}