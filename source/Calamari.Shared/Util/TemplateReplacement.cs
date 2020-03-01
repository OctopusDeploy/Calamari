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

        public string ResolveAndSubstituteFile(Func<string, string> readContent, string relativeFilePath, bool inPackage, IVariables variables)
        {
            return ResolveAndSubstituteFile(
                () => resolver.Resolve(relativeFilePath, inPackage, variables).Value,
                readContent,
                variables);
        }

        public string ResolveAndSubstituteFile(Func<string> resolvePath, Func<string, string> readContent, IVariables variables)
        {
            return resolvePath()
                .Map(readContent)
                .Map(variables.Evaluate);
        }
    }
}