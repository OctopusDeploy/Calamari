using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Octopus.Versioning.Metadata;

namespace Calamari.Integration.Packages.Java
{
    public class JarExtractor : SimplePackageExtractor
    {
        public static readonly string[] EXTENSIONS = {".jar", ".war", ".ear", ".rar", ".zip"};
        static readonly IPackageIDParser mavenPackageIdParser = new MavenPackageIDParser();
        readonly JarTool jarTool;        
        
        public JarExtractor(ICommandLineRunner commandLineRunner, ICommandOutput commandOutput,
            ICalamariFileSystem fileSystem)
        {
            jarTool = new JarTool(commandLineRunner, commandOutput, fileSystem);
        }

        public override string[] Extensions => EXTENSIONS;

        public override int Extract(string packageFile, string directory, bool suppressNestedScriptWarning)
        {
            return jarTool.ExtractJar(packageFile, directory);
        }

        /// <summary>
        /// Java packages are in the unqiue situation where they can be sourced from the built-in library
        /// (which follows NuGet packaging and versioning formats) or from a Maven feed. We have no way 
        /// of knowing which feed the package was sourced from originally, so we we have to assume
        /// it could be either one.
        /// 
        /// The NuGet and Maven filenames are incompatible. This is by design; see 
        /// Octopus.Core.Packages.Maven.MavenPackageID.DELIMITER. NuGet parsing routines will not succeed 
        /// for a Maven package, and Maven parsing will not succeed for a NuGet package.
        /// 
        /// We take advantage of this to fallback through the various parsing routines until one works.
        /// </summary>
        /// <param name="packageFile">Filename to process</param>
        /// <returns>The package metadata</returns>
        public override PackageMetadata GetMetadata(string packageFile)
        {
            try
            {
                return base.GetMetadata(packageFile);
            }
            catch
            {
                return mavenPackageIdParser.GetMetadataFromPackageName(packageFile, EXTENSIONS);
            }           
        }      
    }
}