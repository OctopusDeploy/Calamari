using System;
using System.Collections.Generic;
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
using Newtonsoft.Json;
using Octopus.Core.Extensions;

namespace Calamari.Aws.Commands
{
    [Command("upload-aws-s3", Description = "Uploads a package or package file(s) to an AWS s3 bucket")]
    public class UploadAwsS3Command : Command
    {
        private string variablesFile;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;
        private string bucket;
        private string targetMode;

        public UploadAwsS3Command()
        {
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => sensitiveVariablesPassword = v);
            Options.Add("bucket=", "The bucket to use", v => bucket = v);
            Options.Add("targetMode=", "Whether the entire package or files within the package should be uploaded to the s3 bucket", v => targetMode = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword);
            var packageFile = variables.GetEnvironmentExpandedPath(SpecialVariables.Tentacle.CurrentDeployment.PackageFilePath);
            if (string.IsNullOrEmpty(packageFile))
            {
                throw new CommandException($"No package file was specified. Please provide `{SpecialVariables.Tentacle.CurrentDeployment.PackageFilePath}` variable");
            }

            if (!fileSystem.FileExists(packageFile))
                throw new CommandException("Could not find package file: " + packageFile);

            fileSystem.FreeDiskSpaceOverrideInMegaBytes = variables.GetInt32(SpecialVariables.FreeDiskSpaceOverrideInMegaBytes);
            fileSystem.SkipFreeDiskSpaceCheck = variables.GetFlag(SpecialVariables.SkipFreeDiskSpaceCheck);
            var environment = new AwsEnvironmentGeneration(variables);
            var substituter = new FileSubstituter(fileSystem);

            var conventions = new List<IConvention>
            {
                new ContributeEnvironmentVariablesConvention(),
                new LogVariablesConvention(),
                new ExtractPackageToStagingDirectoryConvention(new GenericPackageExtractorFactory().createStandardGenericPackageExtractor(), fileSystem),
                new LogAwsUserInfoConvention(environment),
                new UploadAwsS3Convention(
                    fileSystem,
                    environment,
                    bucket,
                    GetTargetMode(targetMode),
                    new VariableS3TargetOptionsProvider(variables),
                    substituter
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