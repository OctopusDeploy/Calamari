using System;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Octopus.CoreUtilities;
using Octostache;

namespace Calamari.Util
{
    public class TemplateResolver : ITemplateResolver
    {
        private readonly ICalamariFileSystem filesystem;

        public TemplateResolver(ICalamariFileSystem filesystem)
        {
            this.filesystem = filesystem;
        }

        public ResolvedTemplatePath Resolve(string relativeFilePath, bool inPackage, VariableDictionary variables)
        {
            var result = MaybeResolve(relativeFilePath, inPackage, variables);
            
            if (result.Some())
                return result.Value;

            throw new CommandException($"Could not resolve '{relativeFilePath}' to physical file");
        }

        public Maybe<ResolvedTemplatePath> MaybeResolve(string relativeFilePath, bool inPackage, VariableDictionary variables)
        {
            var absolutePath = relativeFilePath.ToMaybe().Select(path => inPackage
                ? Path.Combine(variables.Get(SpecialVariables.OriginalPackageDirectoryPath), variables.Evaluate(path))
                : Path.Combine(Environment.CurrentDirectory, path));

            return absolutePath.SelectValueOr(x =>
                    !filesystem.FileExists(x) ? Maybe<ResolvedTemplatePath>.None : new ResolvedTemplatePath(x).AsSome(),
                Maybe<ResolvedTemplatePath>.None
            );
        }
    }
}