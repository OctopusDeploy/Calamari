using System;
using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Octopus.CoreUtilities;

namespace Calamari.Common.Util
{
    public class TemplateResolver : ITemplateResolver
    {
        readonly ICalamariFileSystem filesystem;

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
            var packageMessage = inPackage
                ? $" in the package {packageId} {packageVersion}"
                : string.Empty;

            throw new CommandException($"Could not find '{relativeFilePath}'{packageMessage}");
        }

        public Maybe<ResolvedTemplatePath> MaybeResolve(string relativeFilePath, bool inPackage, IVariables variables)
        {
            var absolutePath = relativeFilePath.ToMaybe()
                .Select(path => inPackage
                    ? Path.Combine(variables.Get(KnownVariables.OriginalPackageDirectoryPath), variables.Evaluate(path))
                    : Path.Combine(Environment.CurrentDirectory, path));

            return absolutePath.SelectValueOr(x =>
                    !filesystem.FileExists(x) ? Maybe<ResolvedTemplatePath>.None : new ResolvedTemplatePath(x).AsSome(),
                Maybe<ResolvedTemplatePath>.None
            );
        }
    }
}