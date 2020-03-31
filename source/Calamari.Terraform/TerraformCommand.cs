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
        const string DefaultTerraformFileSubstitution = "**/*.tf\n**/*.tf.json\n**/*.tfvars\n**/*.tfvars.json";

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
            var additionalFileSubstitution = variables.Get(TerraformSpecialVariables.Action.Terraform.FileSubstitution);
            var runAutomaticFileSubstitution = variables.GetFlag(TerraformSpecialVariables.Action.Terraform.RunAutomaticFileSubstitution, true);
            var enableNoMatchWarning = variables.Get(SpecialVariables.Package.EnableNoMatchWarning);

            variables.Add(SpecialVariables.Package.EnableNoMatchWarning,
                !String.IsNullOrEmpty(enableNoMatchWarning) ? enableNoMatchWarning : (!String.IsNullOrEmpty(additionalFileSubstitution)).ToString());

            yield return new ExtractPackageToStagingDirectoryConvention(new CombinedPackageExtractor(log), fileSystem).When(_ => PrimaryPackagePath != null);
            yield return new SubstituteInFilesConvention(fileSystem, substituter,
                _ => true,
                _ => FileTargetFactory(runAutomaticFileSubstitution ? DefaultTerraformFileSubstitution : string.Empty, additionalFileSubstitution));
            yield return step;
        }


        static string[] FileTargetFactory(string defaultFileSubstitution, string additionalFileSubstitution)
        {
            return (defaultFileSubstitution +
                    (string.IsNullOrWhiteSpace(additionalFileSubstitution)
                        ? string.Empty
                        : "\n" + additionalFileSubstitution))
                .Split(new[] {"\r", "\n"}, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}