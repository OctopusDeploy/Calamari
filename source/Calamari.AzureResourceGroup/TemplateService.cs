using System;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Common.Util;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.AzureResourceGroup
{
    class TemplateService
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ITemplateResolver resolver;

        public TemplateService(ICalamariFileSystem fileSystem, ITemplateResolver resolver)
        {
            this.fileSystem = fileSystem;
            this.resolver = resolver;
        }

        public string GetSubstitutedTemplateContent(string relativePath, bool inPackage, IVariables variables)
        {
            return ResolveAndSubstituteFile(() => resolver.Resolve(relativePath, inPackage, variables).Value,
                                            fileSystem.ReadFile,
                                            variables);
        }

        string ResolveAndSubstituteFile(Func<string> resolvePath, Func<string, string> readContent, IVariables variables)
        {
            return resolvePath()
                   .Map(readContent)
                   .Map(variables.Evaluate);
        }
    }
}