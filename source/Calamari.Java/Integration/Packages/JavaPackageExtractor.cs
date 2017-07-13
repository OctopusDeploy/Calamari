using System.Collections.Generic;
using Calamari.Integration.Packages;

namespace Calamari.Java.Integration.Packages
{
    public class JavaPackageExtractor : GenericPackageExtractor
    {
        protected override IList<IPackageExtractor> Extractors => new List<IPackageExtractor>
        {
            new JarExtractor()
        }; 
    }
}