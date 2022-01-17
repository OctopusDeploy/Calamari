using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Aws.Deployment.Conventions;
using Calamari.Aws.Integration.S3;
using Calamari.CloudAccounts;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;

namespace Calamari.Aws.Commands
{
    [Command("upload-aws-s3", Description = "Uploads a package or package file(s) to an AWS s3 bucket")]
    public class UploadAwsS3Command : Command
    {
        readonly ILog log;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly ISubstituteInFiles substituteInFiles;
        readonly IExtractPackage extractPackage;
        readonly IStructuredConfigVariablesService structuredConfigVariablesService;
        PathToPackage pathToPackage;
        string bucket;
        string targetMode;

        public UploadAwsS3Command(
            ILog log,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            ISubstituteInFiles substituteInFiles,
            IExtractPackage extractPackage,
            IStructuredConfigVariablesService structuredConfigVariablesService
            )
        {
            this.log = log;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.substituteInFiles = substituteInFiles;
            this.extractPackage = extractPackage;
            this.structuredConfigVariablesService = structuredConfigVariablesService;
            Options.Add("package=", "Path to the package to extract that contains the package.", v => pathToPackage = new PathToPackage(Path.GetFullPath(v)));
            Options.Add("bucket=", "The bucket to use", v => bucket = v);
            Options.Add("targetMode=", "Whether the entire package or files within the package should be uploaded to the s3 bucket", v => targetMode = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            if (string.IsNullOrEmpty(pathToPackage))
            {
                throw new CommandException($"No package file was specified. Please provide `{TentacleVariables.CurrentDeployment.PackageFilePath}` variable");
            }

            if (!fileSystem.FileExists(pathToPackage))
                throw new CommandException("Could not find package file: " + pathToPackage);

            var environment = AwsEnvironmentGeneration.Create(log, variables).GetAwaiter().GetResult();
            var bucketKeyProvider = new BucketKeyProvider();
            var targetType = GetTargetMode(targetMode);

            var conventions = new List<IConvention>
            {
                new DelegateInstallConvention(d => extractPackage.ExtractToStagingDirectory(pathToPackage)).When(_ => targetType == S3TargetMode.FileSelections),
                new LogAwsUserInfoConvention(environment),
                new CreateS3BucketConvention(environment, _ => bucket),
                new UploadAwsS3Convention(
                    log,
                    fileSystem,
                    environment,
                    bucket,
                    targetType,
                    new VariableS3TargetOptionsProvider(variables),
                    bucketKeyProvider,
                    substituteInFiles,
                    structuredConfigVariablesService
                )
            };

            var deployment = new RunningDeployment(pathToPackage, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions, log);

            conventionRunner.RunConventions();
            return 0;
        }

        private static S3TargetMode GetTargetMode(string value)
        {
            return Enum.TryParse<S3TargetMode>(value, out var result) ? result : S3TargetMode.EntirePackage;
        }
    }
}
