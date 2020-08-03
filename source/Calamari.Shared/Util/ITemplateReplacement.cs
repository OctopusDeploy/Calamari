using System;
using Calamari.Common.Plumbing.Variables;

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
        /// <param name="fetch">Callback to fetch the content given the absolute template path</param>
        /// <param name="relativeFilePath">The relative path to the file to process</param>
        /// <param name="inPackage">True if the file is in a package, and false otherwise</param>
        /// <param name="variables">The variables used for the replacement values</param>
        /// <returns>The contents of the source file with the variables replaced</returns>
        string ResolveAndSubstituteFile(
            Func<string, string> fetch,
            string relativeFilePath, 
            bool inPackage, 
            IVariables variables);

        string ResolveAndSubstituteFile(
            Func<string> resolve,
            Func<string, string> fetch,
            IVariables variables);
    }
}