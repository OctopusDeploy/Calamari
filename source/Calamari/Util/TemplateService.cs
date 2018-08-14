using Calamari.Shared.FileSystem;
using Calamari.Shared.Util;
using Octopus.CoreUtilities.Extensions;
using Octostache;

namespace Calamari.Util
{    
    public class TemplateService: ITemplateService
    {
        private readonly ICalamariFileSystem fileSystem;
        private readonly ITemplateResolver resolver;
        private readonly ITemplateReplacement replacement;

        public TemplateService(ICalamariFileSystem fileSystem, ITemplateResolver resolver, ITemplateReplacement replacement)
        {
            this.fileSystem = fileSystem;
            this.resolver = resolver;
            this.replacement = replacement;
        }

        public string GetTemplateContent(string relativePath, bool inPackage, VariableDictionary variables)
        {
            return resolver.Resolve(relativePath, inPackage, variables)
                .Map(x => x.Value)
                .Map(fileSystem.ReadFile);
        }

        public string GetSubstitutedTemplateContent(string relativePath, bool inPackage, VariableDictionary variables)
        {
            return replacement.ResolveAndSubstituteFile(
                () => resolver.Resolve(relativePath, inPackage, variables).Value,
                fileSystem.ReadFile,
                variables);
        }
    }
}