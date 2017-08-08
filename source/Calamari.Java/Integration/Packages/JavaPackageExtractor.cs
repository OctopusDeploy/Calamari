using System.Collections.Generic;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;

namespace Calamari.Java.Integration.Packages
{
    public class JavaPackageExtractor : GenericPackageExtractor
    {
        private readonly ICommandLineRunner commandLineRunner;

        public JavaPackageExtractor(ICommandLineRunner commandLineRunner)
        {
            this.commandLineRunner = commandLineRunner;
        }

        protected override IList<IPackageExtractor> Extractors => new List<IPackageExtractor>
        {
            new JarExtractor(commandLineRunner)
        }; 
    }
}