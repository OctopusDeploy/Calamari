using System;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Octopus.CoreUtilities.Extensions;
using Octostache;

namespace Calamari.Util
{
    public class TemplateReplacement : ITemplateReplacement
    {
        public string ResolveAndSubstituteFile(ICalamariFileSystem fileSystem, string relativeFilePath, bool inPackage, VariableDictionary variables)
        {
            return GetAbsolutePath(fileSystem, relativeFilePath, inPackage, variables)
                .Map(path => variables.Evaluate(fileSystem.ReadFile(path)));
        }

        public string GetAbsolutePath(ICalamariFileSystem fileSystem, string relativeFilePath, bool inPackage, VariableDictionary variables)
        {
            var absolutePath = inPackage
                ? Path.Combine(variables.Get(SpecialVariables.OriginalPackageDirectoryPath), variables.Evaluate(relativeFilePath))
                : Path.Combine(Environment.CurrentDirectory, relativeFilePath);

            if (!File.Exists(absolutePath))
                throw new CommandException($"Could not resolve '{relativeFilePath}' to physical file");

            return absolutePath;
        }
    }
}