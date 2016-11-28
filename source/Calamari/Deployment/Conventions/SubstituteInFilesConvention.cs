using System.IO;
using System.Linq;
using Calamari.Extensibility;
using Calamari.Extensibility.FileSystem;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Substitutions;

namespace Calamari.Deployment.Conventions
{

    public class SubstituteFileConvention : IInstallConvention
    {
        private readonly string file;
        private readonly ICalamariFileSystem fileSystem;
        readonly IFileSubstituter substituter;

        public SubstituteFileConvention(string file, ICalamariFileSystem fileSystem, IFileSubstituter substituter)
        {
            this.file = file;
            this.fileSystem = fileSystem;
            this.substituter = substituter;
        }

        public void Install(RunningDeployment deployment)
        {
            if (!deployment.Variables.GetFlag(SpecialVariables.Package.SubstituteInFilesEnabled))
                return;

            if (!fileSystem.FileExists(file))
            {
                Log.WarnFormat("The file '{0}' could not be found for variable substitution.", file);
                return;
            }
            Log.Info("Performing variable substitution on '{0}'", file);
            substituter.PerformSubstitution(file, deployment.Variables);
        }
    }

    public class SubstituteInFilesConvention : IInstallConvention
    {
        private readonly ICalamariFileSystem fileSystem;
        readonly IFileSubstituter substituter;

        public SubstituteInFilesConvention(ICalamariFileSystem fileSystem, IFileSubstituter substituter)
        {
            this.fileSystem = fileSystem;
            this.substituter = substituter;
        }

        public void Install(RunningDeployment deployment)
        {
            if (!deployment.Variables.GetFlag(SpecialVariables.Package.SubstituteInFilesEnabled))
                return;

            foreach (var target in deployment.Variables.GetPaths(SpecialVariables.Package.SubstituteInFilesTargets))
            {
                var matchingFiles = fileSystem.EnumerateFiles(deployment.CurrentDirectory, target).Select(Path.GetFullPath).ToList();

                if (!matchingFiles.Any())
                {
                    Log.WarnFormat("No files were found that match the substitution target pattern '{0}'", target);
                    continue;
                }

                foreach (var file in matchingFiles)
                {
                    Log.Info("Performing variable substitution on '{0}'", file);
                    substituter.PerformSubstitution(file, deployment.Variables);
                }
            }
        }
    }
}