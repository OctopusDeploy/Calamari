using System;
using Calamari.Common.Plumbing.Variables;
using Octopus.CoreUtilities;

namespace Calamari.Common.Util
{
    public class ResolvedTemplatePath
    {
        public ResolvedTemplatePath(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public static explicit operator ResolvedTemplatePath(string value)
        {
            return new ResolvedTemplatePath(value);
        }

        public static explicit operator string(ResolvedTemplatePath value)
        {
            return value.Value;
        }

        public override string ToString()
        {
            return Value;
        }
    }

    public interface ITemplateResolver
    {
        /// <summary>
        /// Gets the path to the supplied template file and throw if it does not exist.
        /// </summary>
        /// <param name="relativeFilePath">The relative path to the file to process</param>
        /// <param name="inPackage">True if the file is in a package, and false otherwise</param>
        /// <param name="variables">The variables that contain the deployment locations</param>
        /// <returns>The path to the supplied file</returns>
        ResolvedTemplatePath Resolve(string relativeFilePath, bool inPackage, IVariables variables);

        /// <summary>
        /// Gets the path to the supplied template file in a safe way.
        /// </summary>
        /// <param name="relativeFilePath">The relative path to the file to process</param>
        /// <param name="inPackage">True if the file is in a package, and false otherwise</param>
        /// <param name="variables">The variables that contain the deployment locations</param>
        /// <returns>Maybe file path or Nothing if it doesn't exist</returns>
        Maybe<ResolvedTemplatePath> MaybeResolve(string relativeFilePath, bool inPackage, IVariables variables);
    }
}