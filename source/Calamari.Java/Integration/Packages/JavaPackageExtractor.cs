using System.Collections.Generic;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;

namespace Calamari.Java.Integration.Packages
{
    public class JavaPackageExtractor : GenericPackageExtractor
    {
        private readonly ICommandLineRunner commandLineRunner;
        private readonly ICalamariFileSystem fileSystem;

        public JavaPackageExtractor(ICommandLineRunner commandLineRunner, ICalamariFileSystem fileSystem)
        {
            this.commandLineRunner = commandLineRunner;
            this.fileSystem = fileSystem;
        }

        protected override IList<IPackageExtractor> Extractors => new List<IPackageExtractor>
        {
            new JarExtractor(commandLineRunner, fileSystem)
        }; 
    }
}