using System;
using Octopus.CoreUtilities.Extensions;
using Octostache;

namespace Calamari.Util
{
    public class TemplateReplacement : ITemplateReplacement
    {
        private readonly ITemplateResolver resolver;

        public TemplateReplacement(ITemplateResolver resolver)
        {
            this.resolver = resolver;
        }

        public string ResolveAndSubstituteFile(Func<string, string> readContent, string relativeFilePath, bool inPackage, VariableDictionary variables)
        {
            return ResolveAndSubstituteFile(
                () => resolver.ResolveAbsolutePath(relativeFilePath, inPackage, variables),
                readContent,
                variables);
        }

        public string ResolveAndSubstituteFile(Func<string> resolvePath, Func<string, string> readContent, VariableDictionary variables)
        {
            return resolvePath()
                .Map(readContent)
                .Map(variables.Evaluate);
        }
    }
}