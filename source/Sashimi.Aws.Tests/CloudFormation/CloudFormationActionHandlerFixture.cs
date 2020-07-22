using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using NuGet.Packaging;
using NuGet.Versioning;
using NUnit.Framework;
using Sashimi.Aws.ActionHandler;
using Sashimi.Tests.Shared;
using Sashimi.Tests.Shared.Server;

namespace Sashimi.Aws.Tests.CloudFormation
{
    public class CloudFormationActionHandlerFixture
    {
        const string AwsStackRole = "arn:aws:iam::968802670493:role/e2e_buckets";
        // BucketName must use the following prefix, otherwise the above stack role will not have permission to access
        const string ValidBucketNamePrefix = "cfe2e-";
        const string StackNamePrefix = "E2ETestStack-";
        const string TransformIncludeLocation = "s3://octopus-e2e-tests/permanent/tags.json";
        const string AwsRegion = "us-east-1";

        string stackName;

        [SetUp]
        public void Setup()
        {
            stackName = $"{StackNamePrefix}{UniqueName.Generate()}";
        }

        [Test]
        public void RunCloudFormation_InlineSourceWithoutParameters()
        {
            var bucketName = $"{ValidBucketNamePrefix}{UniqueName.Short()}";

            var template = File.ReadAllText(Path.Combine(TestEnvironment.GetTestPath(), "CloudFormation", "package-withoutparameters", "template.json"))
                .Replace("@BucketName", bucketName);
            var result = ActionHandlerTestBuilder.Create<AwsRunCloudFormationActionHandler, Calamari.Aws.Program>()
                    .WithArrange(context =>
                    {
                        context.WithStack(stackName)
                            .WithAwsAccount()
                            .WithAwsRegion(AwsRegion)
                            .WithStackRole(AwsStackRole)
                            .WithAwsTemplateInlineSource(template, null);

                        context.Variables.Add(AwsSpecialVariables.Action.Aws.WaitForCompletion, bool.TrueString);
                    })
                    .Execute(false);

            result.WasSuccessful.Should().BeTrue();
            result.OutputVariables["AwsOutputs[OutputName]"].Value.Should().Be(bucketName);
        }

        [Test]
        public void RunCloudFormation_InlineSourceWithYamlTemplateAndParameters()
        {
            var nameVarParamValue = $"{ValidBucketNamePrefix}{UniqueName.Short()}";
            var namePlainParamValue = $"{ValidBucketNamePrefix}{UniqueName.Short()}";

            var templateFolderPath = Path.Combine(TestEnvironment.GetTestPath(), "CloudFormation", "package-withparameters");
            var template = File.ReadAllText(Path.Combine(templateFolderPath, "template.yaml"));
            var parameters = File.ReadAllText(Path.Combine(templateFolderPath, "parameters.json"))
                .Replace("@NamePlainParamValue", namePlainParamValue);

            var result = ActionHandlerTestBuilder.Create<AwsRunCloudFormationActionHandler, Calamari.Aws.Program>()
                .WithArrange(context =>
                {
                    context.WithStack(stackName)
                        .WithAwsAccount()
                        .WithAwsRegion(AwsRegion)
                        .WithStackRole(AwsStackRole)
                        .WithAwsTemplateInlineSource(template, parameters);

                    context.Variables.Add(AwsSpecialVariables.Action.Aws.WaitForCompletion, bool.TrueString);
                    context.Variables.Add("NameVarParamValue", nameVarParamValue);
                })
                .Execute(false);

            result.WasSuccessful.Should().BeTrue();

            result.OutputVariables["AwsOutputs[OutputWithVariableParam]"].Value.Should().Be(nameVarParamValue);
            result.OutputVariables["AwsOutputs[OutputWithPlainParam]"].Value.Should().Be(namePlainParamValue);
        }

        [Test]
        public void RunCloudFormation_PackageWithoutParameters()
        {
            const string templateFileName = "template.json";

            var bucketName = $"{ValidBucketNamePrefix}{UniqueName.Short()}";

            var templateFolderPath = Path.Combine(TestEnvironment.GetTestPath(), "CloudFormation", "package-withoutparameters");
            var tempFolderPath = Path.Combine(templateFolderPath, UniqueName.Short());

            var templateContent = File.ReadAllText(Path.Combine(templateFolderPath, templateFileName))
                .Replace("@BucketName", bucketName);
            CreateFile(tempFolderPath, templateFileName, templateContent);

            var packageFileName = CreateNugetPackage($"{nameof(RunCloudFormation_PackageWithoutParameters)}", tempFolderPath);
            var pathToPackage = Path.Combine(tempFolderPath, packageFileName);

            var result = ActionHandlerTestBuilder.Create<AwsRunCloudFormationActionHandler, Calamari.Aws.Program>()
                .WithArrange(context =>
                {
                    context.WithStack(stackName)
                        .WithAwsAccount()
                        .WithAwsRegion(AwsRegion)
                        .WithStackRole(AwsStackRole)
                        .WithAwsTemplatePackageSource("template.json", null)
                        .WithPackage(pathToPackage);

                    context.Variables.Add(AwsSpecialVariables.Action.Aws.WaitForCompletion, bool.TrueString);
                })
                .Execute(false);

            result.WasSuccessful.Should().BeTrue();
            result.OutputVariables["AwsOutputs[OutputName]"].Value.Should().Be(bucketName);

            Directory.Delete(tempFolderPath, true);
        }

        [Test]
        public void RunCloudFormation_PackageWithParameters()
        {
            const string parametersFileName = "parameters.json";
            const string templateFileName = "template.yaml";

            var templateFolderPath = Path.Combine(TestEnvironment.GetTestPath(), "CloudFormation", "package-withparameters");
            var tempFolderPath = Path.Combine(templateFolderPath, UniqueName.Short());

            var templateContent = File.ReadAllText(Path.Combine(templateFolderPath, templateFileName));
            CreateFile(tempFolderPath, templateFileName, templateContent);

            var nameVarParamValue = $"{ValidBucketNamePrefix}{UniqueName.Short()}";
            var namePlainParamValue = $"{ValidBucketNamePrefix}{UniqueName.Short()}";
            var parametersContent = File.ReadAllText(Path.Combine(templateFolderPath, parametersFileName))
                    .Replace("@NamePlainParamValue", namePlainParamValue);
            CreateFile(tempFolderPath, parametersFileName, parametersContent);

            var packageFileName = CreateNugetPackage($"{nameof(RunCloudFormation_PackageWithoutParameters)}", tempFolderPath);
            var pathToPackage = Path.Combine(tempFolderPath, packageFileName);

            var result = ActionHandlerTestBuilder.Create<AwsRunCloudFormationActionHandler, Calamari.Aws.Program>()
                .WithArrange(context =>
                {
                    context.WithStack(stackName)
                        .WithAwsAccount()
                        .WithAwsRegion(AwsRegion)
                        .WithStackRole(AwsStackRole)
                        .WithAwsTemplatePackageSource(templateFileName, parametersFileName)
                        .WithPackage(pathToPackage);

                    context.Variables.Add("NameVarParamValue", nameVarParamValue);
                    context.Variables.Add(AwsSpecialVariables.Action.Aws.WaitForCompletion, bool.TrueString);
                })
                .Execute(false);

            result.OutputVariables["AwsOutputs[OutputWithVariableParam]"].Value.Should().Be(nameVarParamValue);
            result.OutputVariables["AwsOutputs[OutputWithPlainParam]"].Value.Should().Be(namePlainParamValue);

            Directory.Delete(tempFolderPath, true);
        }

        [Test]
        public void RunCloudFormation_ChangeSet()
        {
            var bucketName = $"{ValidBucketNamePrefix}{UniqueName.Generate()}";
            var pathToPackage = TestEnvironment.GetTestPath(@"Packages/CloudFormationS3.1.0.0.nupkg");

            // create bucket
            CreateBucket(stackName, bucketName, pathToPackage);

            // remove bucket tags
            var (changeSetId, stackId) = RemoveBucketTag(stackName, bucketName, pathToPackage);

            // apply remove bucket tags
            ApplyChangeSet(changeSetId, stackId, bucketName);

            // create bucket again
            CreateBucketAgain(stackName, bucketName, pathToPackage);
        }

        [TearDown]
        public void TearDown()
        {
            DeleteStack(stackName);
        }

        static void DeleteStack(string stackName)
        {
            ActionHandlerTestBuilder.Create<AwsDeleteCloudFormationActionHandler, Calamari.Aws.Program>()
                .WithArrange(context =>
                {
                    context.WithStack(stackName)
                        .WithAwsAccount()
                        .WithAwsRegion(AwsRegion)
                        .WithStackRole(AwsStackRole);

                    context.Variables.Add(AwsSpecialVariables.Action.Aws.WaitForCompletion, bool.TrueString);
                })
                .Execute();
        }

        static void CreateBucketAgain(string stackName, string bucketName, string pathToPackage)
        {
            ActionHandlerTestBuilder.Create<AwsRunCloudFormationActionHandler, Calamari.Aws.Program>()
                .WithArrange(context =>
                {
                    context.WithStack(stackName)
                        .WithAwsAccount()
                        .WithAwsRegion(AwsRegion)
                        .WithStackRole(AwsStackRole)
                        .WithCloudFormationChangeSets()
                        .WithAwsTemplatePackageSource("bucket.json", "bucket-parameters.json")
                        .WithPackage(pathToPackage);

                    context.Variables.Add("BucketName", bucketName);
                    context.Variables.Add("TransformIncludeLocation", TransformIncludeLocation);
                    context.Variables.Add(AwsSpecialVariables.Action.Aws.WaitForCompletion, bool.TrueString);
                })
                .Execute();
        }

        static void ApplyChangeSet(string changeSetId, string stackId, string bucketName)
        {
            ActionHandlerTestBuilder.Create<AwsApplyCloudFormationChangeSetActionHandler, Calamari.Aws.Program>()
                .WithArrange(context =>
                {
                    context.WithStack(stackId)
                        .WithAwsAccount()
                        .WithAwsRegion(AwsRegion)
                        .WithStackRole(AwsStackRole);

                    context.Variables.Add("BucketName", bucketName);
                    context.Variables.Add("TransformIncludeLocation", TransformIncludeLocation);
                    context.Variables.Add(AwsSpecialVariables.Action.Aws.WaitForCompletion, bool.TrueString);
                    context.Variables.Add(AwsSpecialVariables.Action.Aws.CloudFormation.Changesets.Arn, changeSetId);
                })
                .Execute();
        }

        static (string changeSetId, string stackName) RemoveBucketTag(string stackName, string bucketName, string pathToPackage)
        {
            var result = ActionHandlerTestBuilder.Create<AwsRunCloudFormationActionHandler, Calamari.Aws.Program>()
                .WithArrange(context =>
                {
                    context.WithStack(stackName)
                        .WithAwsAccount()
                        .WithAwsRegion(AwsRegion)
                        .WithStackRole(AwsStackRole)
                        .WithPackage(pathToPackage)
                        .WithAwsTemplatePackageSource("bucket.json", "bucket-parameters.json")
                        .WithCloudFormationChangeSets(deferExecution: true)
                        .WithIamCapabilities(new List<string> { "CAPABILITY_IAM"});

                    context.Variables.Add("BucketName", bucketName);
                    context.Variables.Add("TransformIncludeLocation", TransformIncludeLocation);
                    context.Variables.Add(AwsSpecialVariables.Action.Aws.WaitForCompletion, bool.TrueString);
                })
                .Execute();

            return (result.OutputVariables["AwsOutputs[ChangesetId]"].Value, result.OutputVariables["AwsOutputs[StackId]"].Value);
        }

        static void CreateBucket(string stackName, string bucketName, string pathToPackage)
        {
            ActionHandlerTestBuilder.Create<AwsRunCloudFormationActionHandler, Calamari.Aws.Program>()
                .WithArrange(context =>
                {
                    context.WithStack(stackName)
                        .WithAwsAccount()
                        .WithAwsRegion(AwsRegion)
                        .WithStackRole(AwsStackRole)
                        .WithCloudFormationChangeSets()
                        .WithIamCapabilities(new List<string> { "CAPABILITY_IAM" })
                        .WithAwsTemplatePackageSource("bucket-transform.json", "bucket-parameters.json")
                        .WithPackage(pathToPackage);

                    context.Variables.Add("BucketName", bucketName);
                    context.Variables.Add("TransformIncludeLocation", TransformIncludeLocation);
                    context.Variables.Add(AwsSpecialVariables.Action.Aws.WaitForCompletion, bool.TrueString);
                })
                .Execute();
        }

        static void CreateFile(string folderPath, string fileName, string content)
        {
            Directory.CreateDirectory(folderPath);

            using (var file = File.CreateText(Path.Combine(folderPath, fileName)))
            {
                file.Write(content);
            }
        }

        static string CreateNugetPackage(string packageId, string filePath)
        {
            var metadata = new ManifestMetadata
            {
                Authors = new[] { "octopus@e2eTests" },
                Version = new NuGetVersion(1, 0, 0),
                Id = packageId,
                Description = "For CloudFormation E2E Test(s)"
            };

            var packageFileName = $"{packageId}{metadata.Version}.nupkg";

            var builder = new PackageBuilder();
            builder.PopulateFiles(filePath, new[] { new ManifestFile { Source = "**" } });
            builder.Populate(metadata);

            using (var stream = File.Open(Path.Combine(filePath, packageFileName), FileMode.OpenOrCreate))
            {
                builder.Save(stream);
            }

            return packageFileName;
        }
    }
}