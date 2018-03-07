using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Amazon.S3;
using Amazon.S3.Model;
using Calamari.Aws.Integration;
using Calamari.Aws.Integration.S3;
using Calamari.Aws.Util;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Substitutions;
using Octopus.Core.Extensions;

namespace Calamari.Aws.Deployment.Conventions
{
    public class UploadAwsS3Convention : IInstallConvention
    {
        private readonly ICalamariFileSystem fileSystem;
        private readonly IAwsEnvironmentGeneration awsEnvironmentGeneration;
        private readonly string bucket;
        private readonly S3TargetMode targetMode;
        private readonly IProvideS3TargetOptions optionsProvider;
        private readonly IFileSubstituter fileSubstituter;

        public UploadAwsS3Convention(ICalamariFileSystem fileSystem,
            IAwsEnvironmentGeneration awsEnvironmentGeneration,
            string bucket,
            S3TargetMode targetMode,
            IProvideS3TargetOptions optionsProvider,
            IFileSubstituter fileSubstituter
        )
        {
            this.fileSystem = fileSystem;
            this.awsEnvironmentGeneration = awsEnvironmentGeneration;
            this.bucket = bucket;
            this.targetMode = targetMode;
            this.optionsProvider = optionsProvider;
            this.fileSubstituter = fileSubstituter;
        }

        public void Install(RunningDeployment deployment)
        {
            Guard.NotNull(deployment, "deployment can not be null");

            var options = optionsProvider.GetOptions(targetMode);

            AmazonS3Client Factory() => ClientHelpers.CreateS3Client(awsEnvironmentGeneration);
            EnsureBucketExists(Factory);
        
            foreach (var option in options)
            {
                switch (option)
                {
                    case S3PackageOptions package:
                        UploadUsingPackage(Factory, deployment, package);
                        break;
                    case S3SingleFileSlectionProperties selection:
                        UploadSingleFileSelection(Factory, deployment, selection);
                        break;
                    case S3MultiFileSelectionProperties selection:
                        UploadMultiFileSelection(Factory, deployment, selection);
                        break;
                    default:
                        return;
                }
            }
        }

        private void UploadMultiFileSelection(Func<AmazonS3Client> clientFactory, RunningDeployment deployment, S3MultiFileSelectionProperties selection)
        {
            Guard.NotNull(deployment, "Deployment may not be null");
            Guard.NotNull(selection, "Mutli file selection properties may not be null");
            Guard.NotNull(clientFactory, "Client factory must not be null");

            var files = fileSystem.EnumerateFilesWithGlob(deployment.StagingDirectory, selection.Pattern).ToList();
            if (!files.Any())
            {
                Log.Info($"The glob pattern '{selection.Pattern}' didn't match any files. Nothing was uploaded to S3.");
                return;
            }

            var substitutionPatterns = selection.VariableSubstitutionPatterns?.Split(new[] { "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries) ?? new string[0];
            
            new SubstituteInFilesConvention(fileSystem, fileSubstituter,
                    _ => substitutionPatterns.Any(),
                    _ => substitutionPatterns)
                .Install(deployment);

            using (var client = clientFactory())
            {
                foreach (var matchedFile in files)
                {
                    var request = CreateRequest(matchedFile, $"{selection.BucketKeyPrefix}{fileSystem.GetFileName(matchedFile)}", selection);
                    client.PutObject(request);
                }   
            }
        }

        public void UploadSingleFileSelection(Func<AmazonS3Client> clientFactory, RunningDeployment deployment, S3SingleFileSlectionProperties selection)
        {
            Guard.NotNull(deployment, "Deployment may not be null");
            Guard.NotNull(selection, "Single file selection properties may not be null");
            Guard.NotNull(clientFactory, "Client factory must not be null");

            var filePath = Path.Combine(deployment.CurrentDirectory, selection.Path);

            if (!fileSystem.FileExists(filePath))
            {
                throw new FileNotFoundException($"The file {selection.Path} could not be found in the package.");
            }

            new SubstituteInFilesConvention(fileSystem, fileSubstituter, 
                _ => selection.PerformVariableSubstitution, 
                _ => new List<string>{ filePath })
                .Install(deployment);
    
                CreateRequest(filePath, selection.BucketKey, selection)
                    .Tee(x => LogPutObjectRequest(filePath, x))
                    .Tee(x => clientFactory().PutObject(x));
        }

        public void UploadUsingPackage(Func<AmazonS3Client> clientFactory, RunningDeployment deployment,S3PackageOptions options)
        {
            Guard.NotNull(deployment, "Deployment may not be null");
            Guard.NotNull(options, "Package options may not be null");
            Guard.NotNull(clientFactory, "Client factory must not be null");

            using (var client = clientFactory())
            {
                var request = CreateRequest(deployment.PackageFilePath, options.BucketKey, options);
                LogPutObjectRequest("entire package", request);
                client.PutObject(request);
            }
        }

        private PutObjectRequest CreateRequest(string path, string bucketKey, S3TargetPropertiesBase properties)
        {
            Guard.NotNullOrWhiteSpace(path, "The given path may not be null");
            Guard.NotNullOrWhiteSpace(bucket, "The provided bucket key may not be null");
            Guard.NotNull(properties, "Target properties may not be null");

            return new PutObjectRequest
                {
                    BucketName = bucket,
                    FilePath = path,
                    Key = bucketKey,
                    StorageClass = properties.StorageClass,
                    CannedACL = properties.CannedAcl
                }
                .WithMetadata(properties)
                .WithTags(properties);
        }


        public void EnsureBucketExists(Func<AmazonS3Client> clientFactory)
        {
            Guard.NotNull(clientFactory, "Client factory should not be null");

            using (var client = clientFactory())
            {
                if (Amazon.S3.Util.AmazonS3Util.DoesS3BucketExist(client, bucket)) return;
                var request = new PutBucketRequest
                {
                    BucketName = bucket,
                    UseClientRegion = true
                };

                client.PutBucket(request);
            }
        }

        private static void LogPutObjectRequest(string fileOrPackageDescription, PutObjectRequest request)
        {
            Log.Info($"Uploading {fileOrPackageDescription} to bucket {request.BucketName} with key {request.Key}.");
        }
    }
}
