using System;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Common.Util;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.AzureResourceGroup;

class TemplateService(ICalamariFileSystem fileSystem, ITemplateResolver resolver) : ITemplateService
{
    public string GetSubstitutedTemplateContent(string relativePath, bool inPackage, IVariables variables)
    {
        return ResolveAndSubstituteFile(() => resolver.Resolve(relativePath, inPackage, variables).Value,
                                        fileSystem.ReadFile,
                                        variables);
    }

    static string ResolveAndSubstituteFile(Func<string> resolvePath, Func<string, string> readContent, IVariables variables)
    {
        return resolvePath()
               .Map(readContent)
               .Map(variables.Evaluate)
               // Evaluate can return null - while unlikely we should handle gracefully
               ?? string.Empty;
    }
}