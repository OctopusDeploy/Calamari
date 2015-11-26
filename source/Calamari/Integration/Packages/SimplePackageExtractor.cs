using System.IO;
using System.Linq;

namespace Calamari.Integration.Packages
{
    public abstract class SimplePackageExtractor : IPackageExtractor
    {
        public PackageMetadata GetMetadata(string packageFile)
        {
            var pkg = new PackageMetadata();
            var fileName = Path.GetFileName(packageFile);

            var matchingExtension = Extensions.FirstOrDefault(ext => fileName.EndsWith(ext));

            if (string.IsNullOrEmpty(matchingExtension))
                return pkg;

            var metaData = fileName.Substring(0, fileName.Length - matchingExtension.Length);
            var nameParts = metaData.Split('.');
            for (var i = 0; i < nameParts.Length; i++)
            {
                int num;
                if (int.TryParse(nameParts[i], out num))
                {
                    pkg.Id = string.Join(".", nameParts.Take(i));
                    pkg.Version = string.Join(".", nameParts.Skip(i));
                    break;
                }
            }

            return pkg;
        }

        public abstract int Extract(string packageFile, string directory, bool suppressNestedScriptWarning);
        public abstract string[] Extensions { get; }
    }
}