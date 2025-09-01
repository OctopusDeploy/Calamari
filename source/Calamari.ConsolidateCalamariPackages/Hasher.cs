using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Octopus.Calamari.ConsolidatedPackage
{
    class Hasher : IDisposable {

        private readonly MD5 md5 = MD5.Create();
        
        
        public string GetPackageCombinationHash(string assemblyVersion, IReadOnlyList<IPackageReference> packagesToScan)
        {
            var uniqueString = string.Join(",", packagesToScan.OrderBy(p => p.Name).Select(p => p.Name + p.Version));
            uniqueString += assemblyVersion;
            return Hash(uniqueString);
        }
        
        public string Hash(ZipArchiveEntry entry)
        {
            using (var entryStream = entry.Open())
                return BitConverter.ToString(md5.ComputeHash(entryStream)).Replace("-", "").ToLower();
        }

        public string Hash(string str)
            => Hash(Encoding.UTF8.GetBytes(str));

        public string Hash(byte[] bytes)
            => BitConverter.ToString(md5.ComputeHash(bytes)).Replace("-", "").ToLower();

        public void Dispose()
            => md5?.Dispose();
    }
}