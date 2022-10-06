using System;

namespace Calamari.Common.Features.Packages.Java
{
    public class JarPackageExtractor : IPackageExtractor
    {
        public static readonly string[] SupportedExtensions = { ".jar", ".war", ".ear", ".rar", ".zip" };

        readonly JarTool jarTool;

        public JarPackageExtractor(JarTool jarTool)
        {
            this.jarTool = jarTool;
        }

        public string[] Extensions => SupportedExtensions;

        public int Extract(string packageFile, string directory)
        {
            return jarTool.ExtractJar(packageFile, directory);
        }
    }
}