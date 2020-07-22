using System;
using System.Collections.Generic;
using Calamari.Aws;
using Calamari.Aws.Integration.S3;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using Sashimi.Aws.Accounts;
using Sashimi.Aws.ActionHandler;
using Sashimi.Aws.Validation;
using Sashimi.Server.Contracts;
using Sashimi.Tests.Shared;
using Sashimi.Tests.Shared.Server;
using BucketKeyBehaviourType = Sashimi.Aws.Validation.BucketKeyBehaviourType;
using S3FileSelectionProperties = Sashimi.Aws.Validation.S3FileSelectionProperties;

namespace Sashimi.Aws.Tests.RunScript
{
    public class AwsS3UploadActionHandlerFixture
    {
        [Test]
        public void AwsS3UploadPackage()
        {
            var bucketName = "octopus-e2e-tests";
            var region = "ap-southeast-1";
            var folderPrefix = $"test/{Guid.NewGuid()}/";
            var path = TestEnvironment.GetTestPath(@"AwsS3Sample/AwsS3Sample.1.0.0.nupkg");

            ActionHandlerTestBuilder.Create<AwsUploadS3ActionHandler, Program>()
                .WithArrange(context =>
                {
                    context.Variables.Add("Octopus.Action.AwsAccount.Variable", "AWSAccount");
                    context.Variables.Add("Octopus.Action.Aws.AssumeRole", "False");
                    context.Variables.Add("Octopus.Action.Aws.AssumedRoleArn", "");
                    context.Variables.Add("Octopus.Action.Aws.AssumedRoleSession", "");
                    context.Variables.Add("Octopus.Action.Aws.Region", region);
                    context.Variables.Add(AwsSpecialVariables.Action.Aws.S3.BucketName, bucketName);
                    context.Variables.Add(AwsSpecialVariables.Action.Aws.S3.TargetMode, "EntirePackage");
                    context.WithAwsAccount();
                    context.WithPackage(path);
                    context.Variables.Add(
                        AwsSpecialVariables.Action.Aws.S3.PackageOptions,
                        JsonConvert.SerializeObject(new S3PackageProperties
                        {
                            BucketKey = $"{folderPrefix}AwsS3Sample.nupkg",
                            CannedAcl = "private",
                            Metadata = {new KeyValuePair<string, string>("TestKey", "TestValue")},
                            Tags = {new KeyValuePair<string, string>("TestTag", "TestTagValue")},
                            StorageClass = "STANDARD",
                            BucketKeyBehaviour = BucketKeyBehaviourType.Custom
                        }, new JsonSerializerSettings {ContractResolver = new CamelCasePropertyNamesContractResolver()}));
                })
                .Execute();

            CleanUpS3Bucket(bucketName, folderPrefix, region);
        }

        [Test]
        public void AwsS3UploadFileSelections()
        {
            var bucketName = "octopus-e2e-tests";
            var region = "ap-southeast-1";
            var folderPrefix = $"test/{Guid.NewGuid().ToString()}/";
            var path = TestEnvironment.GetTestPath(@"AwsS3Sample/AwsS3Sample.1.0.0.nupkg");

            ActionHandlerTestBuilder.Create<AwsUploadS3ActionHandler, Program>()
                .WithArrange(context =>
                {
                    context.Variables.Add("Octopus.Action.Aws.AssumeRole", bool.FalseString);
                    context.Variables.Add("Octopus.Action.Aws.AssumedRoleArn", string.Empty);
                    context.Variables.Add("Octopus.Action.Aws.AssumedRoleSession", string.Empty);
                    context.Variables.Add("Octopus.Action.Aws.Region", region);
                    context.Variables.Add(SpecialVariables.Action.Aws.UseInstanceRole, bool.FalseString);
                    context.Variables.Add(AwsSpecialVariables.Action.Aws.S3.BucketName, bucketName);
                    context.Variables.Add(AwsSpecialVariables.Action.Aws.S3.TargetMode, S3TargetMode.FileSelections.ToString());
                    context.WithPackage(path);
                    context.WithAwsAccount();
                    context.Variables.Add(
                        AwsSpecialVariables.Action.Aws.S3.FileSelections,
                                JsonConvert.SerializeObject(new List<S3FileSelectionProperties>
                                {
                                    new S3FileSelectionProperties
                                    {
                                        Type = "SingleFile",
                                        BucketKey = $"{folderPrefix}single-file.ini",
                                        Path = "desktop.ini",
                                        PerformVariableSubstitution = bool.TrueString,
                                        CannedAcl = "private",
                                        Tags = {new KeyValuePair<string, string>("TestTag", "TestTagValue")},
                                        BucketKeyBehaviour = BucketKeyBehaviourType.Custom,
                                        StorageClass = "STANDARD"
                                    },
                                    new S3FileSelectionProperties
                                    {
                                        Type = "SingleFile",
                                        BucketKeyPrefix = folderPrefix,
                                        Path = "desktop.ini",
                                        PerformVariableSubstitution = bool.TrueString,
                                        CannedAcl = "private",
                                        Tags = {new KeyValuePair<string, string>("TestTag", "TestTagValue")},
                                        BucketKeyBehaviour = BucketKeyBehaviourType.Filename,
                                        StorageClass = "STANDARD"
                                    },
                                    new S3FileSelectionProperties
                                    {
                                        Type = "MultipleFiles",
                                        BucketKeyPrefix = folderPrefix,
                                        Pattern = "*.ini",
                                        VariableSubstitutionPatterns = "*.ini",
                                        CannedAcl = "private",
                                        Tags = {new KeyValuePair<string, string>("TestTag", "TestTagValue")},
                                        StorageClass = "STANDARD"
                                    }
                                }, new JsonSerializerSettings {ContractResolver = new CamelCasePropertyNamesContractResolver()}));
                })
                .Execute();

            CleanUpS3Bucket(bucketName, folderPrefix, region);
        }

        private void CleanUpS3Bucket(string bucketName, string folderPrefix, string region)
        {
            ActionHandlerTestBuilder.Create<AwsRunScriptActionHandler, Program>()
                .WithArrange(context =>
                {
                    context.WithAwsAccount();
                    context.Variables.Add(SpecialVariables.Action.Aws.UseInstanceRole, bool.FalseString);
                    context.Variables.Add(KnownVariables.Action.Script.ScriptSource, "Inline");
                    context.Variables.Add(KnownVariables.Action.Script.ScriptBody, $"aws s3 rm s3://{bucketName}/{folderPrefix} --recursive");
                    context.Variables.Add(SpecialVariables.Action.Aws.AssumeRole, bool.FalseString);
                    context.Variables.Add(SpecialVariables.Action.Aws.AssumedRoleArn, string.Empty);
                    context.Variables.Add(SpecialVariables.Action.Aws.AssumedRoleSession, string.Empty);
                    context.Variables.Add(SpecialVariables.Action.Aws.AwsRegion, region);
                })
                .Execute(assertWasSuccess: false); // don't fail the test if the cleanup fails
        }
    }
}