using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Octostache;

namespace Calamari.Integration.Packages.Java
{
    public class JarExtractor : IPackageExtractor
    {
        public static readonly string[] SupportedExtensions = {".jar", ".war", ".ear", ".rar", ".zip"};
        
        readonly JarTool jarTool;

        public JarExtractor(JarTool jarTool)
        {
            this.jarTool = jarTool;
        }

        public string[] Extensions => SupportedExtensions;

        public int Extract(string packageFile, string directory, bool suppressNestedScriptWarning)
        {
            return jarTool.ExtractJar(packageFile, directory);
        }
    }
}