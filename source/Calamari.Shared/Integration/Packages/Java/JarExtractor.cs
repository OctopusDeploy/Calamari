using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Octostache;

namespace Calamari.Integration.Packages.Java
{
    public class JarExtractor : IPackageExtractor
    {
        readonly JarTool jarTool;

        public JarExtractor(JarTool jarTool)
        {
            this.jarTool = jarTool;
        }

        public string[] Extensions => new[] {".jar", ".war", ".ear", ".rar", ".zip"};

        public int Extract(string packageFile, string directory, bool suppressNestedScriptWarning)
        {
            return jarTool.ExtractJar(packageFile, directory);
        }
    }
}