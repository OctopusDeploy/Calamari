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
        public GenericPackageExtractor createStandardGenericPackageExtractor()
        {
            return new GenericPackageExtractor();
        }
    }
}