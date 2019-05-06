using System.Collections.Generic;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Octostache;

namespace Calamari.Integration.Packages.Java
{
    public class JavaPackageExtractor : GenericPackageExtractor
    {
         JarTool JarTool { get; }
        public JavaPackageExtractor(JarTool jarTool)
        {
            JarTool = jarTool;
        }

        protected override IList<IPackageExtractor> Extractors => new List<IPackageExtractor>
        {
            new JarExtractor(JarTool)
        }; 
    }
}