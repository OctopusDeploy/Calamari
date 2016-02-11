using System.IO;
using System.Linq;
using Calamari.Integration.JsonVariables;
using Calamari.Integration.FileSystem;

namespace Calamari.Deployment.Conventions
{
    public class SubstituteInJsonFilesConvention : IInstallConvention
    {
        readonly IJsonFileSubstitutor jsonFileSubstitutor;
        readonly ICalamariFileSystem fileSystem;

        public SubstituteInJsonFilesConvention(IJsonFileSubstitutor jsonFileSubstitutor, ICalamariFileSystem fileSystem)
        {
            this.jsonFileSubstitutor = jsonFileSubstitutor;
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            if (!deployment.Variables.GetFlag(SpecialVariables.Package.SubstituteInJsonFilesEnabled))
                return;

            foreach (var target in deployment.Variables.GetPaths(SpecialVariables.Package.SubstituteInJsonFilesTargets))
            {
                if (fileSystem.DirectoryExists(target))
                {
                    Log.Warn($"Skipping JSON variable substitution on '{target}' because it is a directory.");
                    continue;
                }

                var matchingFiles = fileSystem.EnumerateFiles(deployment.CurrentDirectory, target)
                    .Select(Path.GetFullPath).ToList();

                if (!matchingFiles.Any())
                {
                    Log.Warn($"No files were found that match the substitution target pattern '{target}'");
                    continue;
                }

                foreach (var file in matchingFiles)
                {
                    Log.Info($"Performing JSON variable substitution on '{file}'");
                    jsonFileSubstitutor.ModifyJsonFile(file, deployment.Variables);
                }
            }
        }
    }
}