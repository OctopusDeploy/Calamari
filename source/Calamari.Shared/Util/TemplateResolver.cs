using System;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
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

        public string ResolveAbsolutePath(string relativeFilePath, bool inPackage, VariableDictionary variables)
        {
            var absolutePath = inPackage
                ? Path.Combine(variables.Get(SpecialVariables.OriginalPackageDirectoryPath), variables.Evaluate(relativeFilePath))
                : Path.Combine(Environment.CurrentDirectory, relativeFilePath);

            if (!filesystem.FileExists(absolutePath))
                throw new CommandException($"Could not resolve '{relativeFilePath}' to physical file");

            return absolutePath;
        }
    }
}