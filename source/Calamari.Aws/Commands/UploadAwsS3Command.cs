using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Calamari.Aws.Deployment.Conventions;
using Calamari.Aws.Integration;
using Calamari.Aws.Integration.S3;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Integration.Substitutions;

namespace Calamari.Aws.Commands
{
    [Command("upload-aws-s3", Description = "Uploads a package or package file(s) to an AWS s3 bucket")]
    public class UploadAwsS3Command : Command
    {
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        private string packageFile;
        private string bucket;
        private string targetMode;

        public UploadAwsS3Command(IVariables variables, ICalamariFileSystem fileSystem)
        {
            this.variables = variables;
            this.fileSystem = fileSystem;
            Options.Add("package=", "Path to the package to extract that contains the package.", v => packageFile = Path.GetFullPath(v));
            Options.Add("bucket=", "The bucket to use", v => bucket = v);
            Options.Add("targetMode=", "Whether the entire package or files within the package should be uploaded to the s3 bucket", v => targetMode = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            if (string.IsNullOrEmpty(packageFile))
            {
                throw new CommandException($"No package file was specified. Please provide `{SpecialVariables.Tentacle.CurrentDeployment.PackageFilePath}` variable");
            }

            if (!fileSystem.FileExists(packageFile))
                throw new CommandException("Could not find package file: " + packageFile);

            var environment = AwsEnvironmentGeneration.Create(variables).GetAwaiter().GetResult();
            var substituter = new FileSubstituter(fileSystem);
            var bucketKeyProvider = new BucketKeyProvider();
            var targetType = GetTargetMode(targetMode);
            var packageExtractor = new GenericPackageExtractorFactory().createStandardGenericPackageExtractor();

            var conventions = new List<IConvention>
            {
                new ExtractPackageToStagingDirectoryConvention(packageExtractor, fileSystem).When(_ => targetType == S3TargetMode.FileSelections),
                new LogAwsUserInfoConvention(environment),
                new CreateS3BucketConvention(environment, _ => bucket),
                new UploadAwsS3Convention(
                    fileSystem,
                    environment,
                    bucket,
                    targetType,
                    new VariableS3TargetOptionsProvider(variables),
                    substituter,
                    bucketKeyProvider
                )
            };

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);

            conventionRunner.RunConventions();
            return 0;
        }

        private static S3TargetMode GetTargetMode(string value)
        {
            return Enum.TryParse<S3TargetMode>(value, out var result) ? result : S3TargetMode.EntirePackage;
        }

    }
}