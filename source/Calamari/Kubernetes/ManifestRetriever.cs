using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Kubernetes
{
    public interface IManifestRetriever
    {
        IEnumerable<string> GetManifests(string workingDirectory);
    }
    
    public class ManifestRetriever : IManifestRetriever
    {
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;

        public ManifestRetriever(IVariables variables, ICalamariFileSystem fileSystem)
        {
            this.variables = variables;
            this.fileSystem = fileSystem;
        }
        
        public IEnumerable<string> GetManifests(string workingDirectory)
        {
            var groupedFiles = GetGroupedYamlDirectories(workingDirectory).ToList();
            if (groupedFiles.Any())
            {
                return from file in groupedFiles
                       where fileSystem.FileExists(file)
                       select fileSystem.ReadFile(file);
            }

            return from file in GetManifestFileNames(workingDirectory)
                   where fileSystem.FileExists(file)
                   select fileSystem.ReadFile(file);
        }

        IEnumerable<string> GetManifestFileNames(string workingDirectory)
        {
            var customResourceFileName =
                variables.Get(SpecialVariables.CustomResourceYamlFileName) ?? "customresource.yml";

            return new[]
            {
                "secret.yml", customResourceFileName, "deployment.yml", "service.yml", "ingress.yml",
            }.Select(p => Path.Combine(workingDirectory, p));
        }

        IEnumerable<string> GetGroupedYamlDirectories(string workingDirectory)
        {
            var groupedDirectories = variables.Get(SpecialVariables.GroupedYamlDirectories);
            return groupedDirectories != null
                ? groupedDirectories.Split(';').SelectMany(d => fileSystem.EnumerateFilesRecursively(Path.Combine(workingDirectory, d)))
                : Enumerable.Empty<string>();
        }

        
    }
}