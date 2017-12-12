using Calamari.Integration.FileSystem;
using Octostache;

namespace Calamari.Util
{
    /// <summary>
    /// Defines a service for replacing template markup in files
    /// </summary>
    public interface ITemplateReplacement
    {
        /// <summary>
        /// Reads a file and replaces any template markup with the values from the supplied variables
        /// </summary>
        /// <param name="fileSystem">The calamari file system</param>
        /// <param name="relativeFilePath">The relative path to the file to process</param>
        /// <param name="inPackage">True if the file is in a package, and false otherwise</param>
        /// <param name="variables">The variables used for the replacement values</param>
        /// <returns>The contents of the source file with the variables replaced</returns>
        string ResolveAndSubstituteFile(ICalamariFileSystem fileSystem, string relativeFilePath, bool inPackage, VariableDictionary variables);
    }
}