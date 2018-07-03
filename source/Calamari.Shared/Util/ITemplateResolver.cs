using Octostache;

namespace Calamari.Util
{
    public interface ITemplateResolver
    {
        /// <summary>
        /// Gets the path to the supplied file
        /// </summary>
        /// <param name="relativeFilePath">The relative path to the file to process</param>
        /// <param name="inPackage">True if the file is in a package, and false otherwise</param>
        /// /// <param name="variables">The variables that contain the deployment locations</param>
        /// <returns>The path to the supplied file</returns>
        string ResolveAbsolutePath(string relativeFilePath, bool inPackage, VariableDictionary variables);
    }
}