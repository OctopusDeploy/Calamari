using Calamari.Integration.FileSystem;
using Calamari.Shared.FileSystem;

namespace Calamari.Integration.Packages
{
    /// <summary>
    /// The GenericPackageExtractor is used in a number of scenarios, such as finding packages
    /// on the machine and using those packages for delta copies etc.
    /// 
    /// The kinds of packages that are processed during these opertions can be specific to
    /// the skip of operation being performed. In some cases, we only need to deal with
    /// standard package types like zip, tar.gz and nuget. In other cases we need to support
    /// language specific packges like jars.
    /// 
    /// This Factory will create a GenericPackageExtractor with the required extractors
    /// for a given scenario.
    /// </summary>
    public interface IGenericPackageExtractorFactory
    {
        /// <returns>A GenericPackageExtractor that processes the "standard" package types</returns>
        GenericPackageExtractor createStandardGenericPackageExtractor();
       
//        GenericPackageExtractor createJavaGenericPackageExtractor(ICalamariFileSystem fileSystem);
    }
}