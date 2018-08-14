using Octostache;

namespace Calamari.Shared.Util
{
    public class ResolvedTemplatePath
    {
        public static explicit operator ResolvedTemplatePath(string value)
        {
            return new ResolvedTemplatePath(value);
        }

        public static explicit operator string(ResolvedTemplatePath value)
        {
            return value.Value;
        }
        
        public string Value { get; }

        public ResolvedTemplatePath(string value)
        {
            Value = value;
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
        ResolvedTemplatePath Resolve(string relativeFilePath, bool inPackage, VariableDictionary variables);
    }
    
    public interface ITemplateService
    {
        string GetTemplateContent(string relativePath, bool inPackage, VariableDictionary variables);
        string GetSubstitutedTemplateContent(string relativePath, bool inPackage, VariableDictionary variables);
    }
}