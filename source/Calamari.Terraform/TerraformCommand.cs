using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Extensions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Integration.Substitutions;

namespace Calamari.Terraform
{
    public abstract class TerraformCommand : ICommand
    {
        readonly ILog log;
        private readonly IConvention step;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;


        protected TerraformCommand(ILog log, IVariables variables, ICalamariFileSystem fileSystem, IConvention step)
        {
            this.log = log;
            this.step = step;
            this.variables = variables;
            this.fileSystem = fileSystem;
        }

        public string PrimaryPackagePath => variables.GetPathToPrimaryPackage(fileSystem, false);

        public IEnumerable<IConvention> GetConventions()
        {
            var substituter = new FileSubstituter(log, fileSystem);
            var isEnableNoMatchWarningSet = variables.IsSet(SpecialVariables.Package.EnableNoMatchWarning);
            if (!isEnableNoMatchWarningSet && !string.IsNullOrEmpty(GetAdditionalFileSubstitutions()))
                variables.Add(SpecialVariables.Package.EnableNoMatchWarning, "true");

            yield return new ExtractPackageToStagingDirectoryConvention(new CombinedPackageExtractor(log), fileSystem).When(_ => PrimaryPackagePath != null);
            yield return new SubstituteInFilesConvention(fileSystem, substituter,
                _ => true,
                _ => GetFilesToSubstitute()
            );
            yield return step;
        }


        string[] GetFilesToSubstitute()
        {
            var result = new List<string>();

            var runAutomaticFileSubstitution = variables.GetFlag(TerraformSpecialVariables.Action.Terraform.RunAutomaticFileSubstitution, true);
            if (runAutomaticFileSubstitution)
                result.AddRange(new[] {"**/*.tf", "**/*.tf.json", "**/*.tfvars", "**/*.tfvars.json"});

            var additionalFileSubstitution = GetAdditionalFileSubstitutions();
            if (!string.IsNullOrWhiteSpace(additionalFileSubstitution))
                result.AddRange(additionalFileSubstitution.Split(new[] {"\r", "\n"}, StringSplitOptions.RemoveEmptyEntries));

            return result.ToArray();
        }

        string GetAdditionalFileSubstitutions()
            => variables.Get(TerraformSpecialVariables.Action.Terraform.FileSubstitution);
    }
}