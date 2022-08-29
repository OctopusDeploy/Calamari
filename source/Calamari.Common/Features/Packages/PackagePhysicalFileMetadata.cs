using System;
using System.IO;
using Calamari.Common.Plumbing.Extensions;

namespace Calamari.Common.Features.Packages
{
    public class PackagePhysicalFileMetadata : PackageFileNameMetadata
    {
        public PackagePhysicalFileMetadata(PackageFileNameMetadata identity, string fullFilePath, string hash, long size)
            : base(identity.PackageId, identity.Version, identity.Extension)
        {
            FullFilePath = fullFilePath;
            Hash = hash;
            Size = size;
        }

        public string FullFilePath { get; }
        public string Hash { get; }
        public long Size { get; }

        public static PackagePhysicalFileMetadata? Build(string fullFilePath)
        {
            return Build(fullFilePath, PackageName.FromFile(fullFilePath));
        }

        public static PackagePhysicalFileMetadata? Build(string fullFilePath, PackageFileNameMetadata? identity)
        {
            if (identity == null)
                return null;
            try
            {
                using (var stream = new FileStream(fullFilePath, FileMode.Open, FileAccess.Read))
                {
                    return new PackagePhysicalFileMetadata(identity, fullFilePath, HashCalculator.Hash(stream), stream.Length);
                }
            }
            catch (IOException)
            {
                return null;
            }
        }
    }
}