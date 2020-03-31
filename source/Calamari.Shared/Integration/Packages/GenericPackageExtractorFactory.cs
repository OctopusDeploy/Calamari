using System.Collections.Generic;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages.Java;
using Calamari.Integration.Processes;
using Calamari.Integration.ServiceMessages;
using Octostache;

namespace Calamari.Integration.Packages
{
    public class GenericPackageExtractorFactory : IGenericPackageExtractorFactory
    {
        readonly ILog log;

        public GenericPackageExtractorFactory(ILog log)
        {
            this.log = log;
        }
        
        public GenericPackageExtractor CreateStandardGenericPackageExtractor()
        {
            return new GenericPackageExtractor(log);
        }

        public GenericPackageExtractor CreateJavaGenericPackageExtractor(JarTool jarTool)
        {
            return new GenericPackageExtractor(log,new List<IPackageExtractor>
            {                            
                new JarExtractor(jarTool)
            });
        }
    }
}