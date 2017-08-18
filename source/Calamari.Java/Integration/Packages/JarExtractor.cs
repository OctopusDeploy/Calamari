using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;

namespace Calamari.Java.Integration.Packages
{
    public class JarExtractor : SimplePackageExtractor
    {
        readonly JarTool jarTool;

        public JarExtractor(ICommandLineRunner commandLineRunner, ICalamariFileSystem fileSystem)
        {
            this.jarTool = new JarTool(commandLineRunner, fileSystem);
        }

        public override string[] Extensions => new[] {".jar", ".war", ".ear", ".rar"}; 

        public override int Extract(string packageFile, string directory, bool suppressNestedScriptWarning)
        {
            return jarTool.ExtractJar(packageFile, directory);
        }

    }
}