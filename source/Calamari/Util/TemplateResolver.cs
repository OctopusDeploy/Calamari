using System;
using System.IO;
using Calamari.Shared;
using Calamari.Shared.FileSystem;
using Calamari.Shared.Util;
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
            var absolutePath = inPackage
                ? Path.Combine(variables.Get(SpecialVariables.OriginalPackageDirectoryPath), variables.Evaluate(relativeFilePath))
                : Path.Combine(Environment.CurrentDirectory, relativeFilePath);

            if (filesystem.FileExists(absolutePath))
                return new ResolvedTemplatePath(absolutePath);

            throw new CommandException($"Could not resolve '{relativeFilePath}' to physical file");
        }
    }
}