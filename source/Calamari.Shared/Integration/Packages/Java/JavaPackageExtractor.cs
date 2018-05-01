using System.Collections.Generic;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Packages.Java
{
    public class JavaPackageExtractor : GenericPackageExtractor
    {
        private readonly ICommandLineRunner commandLineRunner;
        private readonly ICalamariFileSystem fileSystem;
        private readonly ICommandOutput commandOutput;

        public JavaPackageExtractor(ICommandLineRunner commandLineRunner, ICommandOutput commandOutput, ICalamariFileSystem fileSystem)
        {
            this.commandLineRunner = commandLineRunner;
            this.fileSystem = fileSystem;
            this.commandOutput = commandOutput;
        }

        protected override IList<IPackageExtractor> Extractors => new List<IPackageExtractor>
        {
            new JarExtractor(commandLineRunner, commandOutput, fileSystem)
        }; 
    }
}