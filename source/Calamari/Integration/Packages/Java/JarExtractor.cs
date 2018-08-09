//using Calamari.Integration.FileSystem;
//using Calamari.Integration.Processes;
//using Calamari.Shared.FileSystem;
//
//namespace Calamari.Integration.Packages.Java
//{
//    public class JarExtractor : SimplePackageExtractor
//    {
//        public static readonly string[] EXTENSIONS = {".jar", ".war", ".ear", ".rar", ".zip"};
//        readonly JarTool jarTool;
//
//        public JarExtractor(ICommandLineRunner commandLineRunner, ICommandOutput commandOutput,
//            ICalamariFileSystem fileSystem)
//        {
//            jarTool = new JarTool(commandLineRunner, commandOutput, fileSystem);
//        }
//
//        public override string[] Extensions => EXTENSIONS;
//
//        public override int Extract(string packageFile, string directory, bool suppressNestedScriptWarning)
//        {
//            return jarTool.ExtractJar(packageFile, directory);
//        }
//    }
//}