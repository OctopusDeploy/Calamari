using System;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Common.Variables;
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

        public ResolvedTemplatePath Resolve(string relativeFilePath, bool inPackage, IVariables variables)
        {
            var result = MaybeResolve(relativeFilePath, inPackage, variables);
            
            if (result.Some())
                return result.Value;

            var packageId = variables.Get("Octopus.Action.Package.PackageId");
            var packageVersion = variables.Get("Octopus.Action.Package.PackageVersion");
            var packageMessage = inPackage ? 
                $" in the package {packageId} {packageVersion}" 
                : string.Empty;
            
            throw new CommandException($"Could not find '{relativeFilePath}'{packageMessage}");
        }

        public Maybe<ResolvedTemplatePath> MaybeResolve(string relativeFilePath, bool inPackage, IVariables variables)
        {
            var absolutePath = relativeFilePath.ToMaybe().Select(path => inPackage
                ? Path.Combine(variables.Get(KnownVariables.OriginalPackageDirectoryPath), variables.Evaluate(path))
                : Path.Combine(Environment.CurrentDirectory, path));

            return absolutePath.SelectValueOr(x =>
                    !filesystem.FileExists(x) ? Maybe<ResolvedTemplatePath>.None : new ResolvedTemplatePath(x).AsSome(),
                Maybe<ResolvedTemplatePath>.None
            );
        }
    }
}