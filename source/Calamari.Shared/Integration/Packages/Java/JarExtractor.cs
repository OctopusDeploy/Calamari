using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Octostache;

namespace Calamari.Integration.Packages.Java
{
    public class JarExtractor : SimplePackageExtractor
    {
        public static readonly string[] EXTENSIONS = {".jar", ".war", ".ear", ".rar", ".zip"};
        readonly JarTool jarTool;

        public JarExtractor(JarTool jarTool)
        {
            this.jarTool = jarTool;
        }

        public override string[] Extensions => EXTENSIONS;

        public override int Extract(string packageFile, string directory, bool suppressNestedScriptWarning)
        {
            return jarTool.ExtractJar(packageFile, directory);
        }
    }
}