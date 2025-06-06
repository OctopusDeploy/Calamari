﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Calamari.Aws.Exceptions;
using Calamari.Aws.Integration.S3;
using Calamari.Aws.Util;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Util;
using Octopus.CoreUtilities;
using Octopus.CoreUtilities.Extensions;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Archives.Zip;
using CompressionType = SharpCompress.Common.CompressionType;

namespace Calamari.Aws.Deployment.Conventions
{
     public class UploadAwsS3Convention : IInstallConvention
    {
        readonly ILog log;
        private readonly ICalamariFileSystem fileSystem;
        private readonly AwsEnvironmentGeneration awsEnvironmentGeneration;
        private readonly string bucket;
        private readonly S3TargetMode targetMode;
        private readonly IProvideS3TargetOptions optionsProvider;
        readonly IBucketKeyProvider bucketKeyProvider;
        readonly ISubstituteInFiles substituteInFiles;
        readonly IStructuredConfigVariablesService structuredConfigVariablesService;
        private readonly bool md5HashSupported;

        private static readonly HashSet<S3CannedACL> CannedAcls = new HashSet<S3CannedACL>(ConstantHelpers.GetConstantValues<S3CannedACL>());

        public UploadAwsS3Convention(
            ILog log,
            ICalamariFileSystem fileSystem,
            AwsEnvironmentGeneration awsEnvironmentGeneration,
            string bucket,
            S3TargetMode targetMode,
            IProvideS3TargetOptions optionsProvider,
            IBucketKeyProvider bucketKeyProvider,
            ISubstituteInFiles substituteInFiles,
            IStructuredConfigVariablesService structuredConfigVariablesService
        )
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.awsEnvironmentGeneration = awsEnvironmentGeneration;
            this.bucket = bucket;
            this.targetMode = targetMode;
            this.optionsProvider = optionsProvider;
            this.bucketKeyProvider = bucketKeyProvider;
            this.substituteInFiles = substituteInFiles;
            this.structuredConfigVariablesService = structuredConfigVariablesService;
            this.md5HashSupported = HashCalculator.IsAvailableHashingAlgorithm(MD5.Create);
        }

        private static string ExceptionMessageWithFilePath(PutObjectRequest request, Exception exception)
        {
            return $"Failed to upload file {request.FilePath}. {exception.Message}";
        }

        private static string InvalidArgumentExceptionMessage(PutObjectRequest request, Exception exception)
        {
            //There isn't an associated error we can check for the Canned ACL so just check it against what we can determine
            //from the values in the SDK.
            string error = $"Failed to upload {request.FilePath}. An invalid argument was provided.";
            return !CannedAcls.Contains(request.CannedACL) ? $"{error} This is possibly due to the value specified for the canned ACL." : error;
        }

        //Errors we care about for each upload.
        private readonly Dictionary<string, Func<PutObjectRequest, Exception, string>> perFileUploadErrors = new Dictionary<string, Func<PutObjectRequest, Exception, string>>
        {
            { "RequestIsNotMultiPartContent", ExceptionMessageWithFilePath },
            { "UnexpectedContent", ExceptionMessageWithFilePath },
            { "MetadataTooLarge", ExceptionMessageWithFilePath },
            { "MaxMessageLengthExceeded", ExceptionMessageWithFilePath },
            { "KeyTooLongError", ExceptionMessageWithFilePath },
            { "SignatureDoesNotMatch", ExceptionMessageWithFilePath },
            { "InvalidStorageClass", ExceptionMessageWithFilePath },
            { "InvalidArgument",  InvalidArgumentExceptionMessage },
            { "InvalidTag", ExceptionMessageWithFilePath }
        };

        public void Install(RunningDeployment deployment)
        {
            InstallAsync(deployment).GetAwaiter().GetResult();
        }

        private async Task InstallAsync(RunningDeployment deployment)
        {
            //The bucket should exist at this point
            Guard.NotNull(deployment, "deployment can not be null");

            if (!md5HashSupported)
            {
                log.Info("MD5 hashes are not supported in executing environment. Files will always be uploaded.");
            }

            var options = optionsProvider.GetOptions(targetMode);
            AmazonS3Client Factory() => ClientHelpers.CreateS3Client(awsEnvironmentGeneration);

            try
            {
                (await UploadAll(options, Factory, deployment)).Tee(responses =>
                {
                    var results = responses.Where(z => z.IsSuccess()).ToArray();
                    if (targetMode == S3TargetMode.EntirePackage && results.FirstOrDefault() != null)
                    {
                        SetOutputVariables(deployment, results.FirstOrDefault());
                    }
                    else if (targetMode == S3TargetMode.FileSelections)
                    {
                        foreach (var result in results)
                        {
                            var fileName = Path.GetFileName(result.BucketKey);
                            SetOutputVariables(deployment, result, fileName);
                        }
                    }
                });
            }
            catch (AmazonS3Exception exception)
            {
                if (exception.ErrorCode == "AccessDenied")
                {
                    throw new PermissionException("The AWS account used to perform the operation does not have the " +
                                                  $"the required permissions to upload to bucket {bucket}");
                }

                throw new UnknownException(
                    $"An unrecognized {exception.ErrorCode} error was thrown while uploading to bucket {bucket}");
            }
            catch (AmazonServiceException exception)
            {
                HandleAmazonServiceException(exception);
                throw;
            }
        }

        void SetOutputVariables(RunningDeployment deployment, S3UploadResult result, string index = "")
        {
            log.SetOutputVariableButDoNotAddToVariables(PackageVariables.Output.FileName, Path.GetFileName(deployment.PackageFilePath));
            log.SetOutputVariableButDoNotAddToVariables(PackageVariables.Output.FilePath, deployment.PackageFilePath);
            var actionName = deployment.Variables["Octopus.Action.Name"];
            log.Info($"Saving object version id to variable \"Octopus.Action[{actionName}].Output.Files[{result.BucketKey}]\"");
            log.SetOutputVariableButDoNotAddToVariables($"Files[{result.BucketKey}]", result.Version);

            var packageKeyVariableName = index.IsNullOrEmpty() ? "Package.Key" : $"Package.Key[{index}]";
            var packageKeyVariableValue = result.BucketKey;
            log.Info($"Saving bucket key to variable \"Octopus.Action[{actionName}].Output.{packageKeyVariableName}\"");
            log.SetOutputVariableButDoNotAddToVariables(packageKeyVariableName, packageKeyVariableValue);

            var packageS3UriVariableName = index.IsNullOrEmpty() ? "Package.S3Uri" : $"Package.S3Uri[{index}]";
            var packageS3UriVariableValue = $"s3://{result.BucketName}/{result.BucketKey}";
            log.Info($"Saving object S3 URI to variable \"Octopus.Action[{actionName}].Output.{packageS3UriVariableName}\"");
            log.SetOutputVariableButDoNotAddToVariables(packageS3UriVariableName, packageS3UriVariableValue);

            var packageUriVariableName = index.IsNullOrEmpty() ? "Package.Uri" : $"Package.Uri[{index}]";
            var packageUriVariableValue = $"https://{result.BucketName}.s3.{awsEnvironmentGeneration.AwsRegion.SystemName}.amazonaws.com/{bucketKeyProvider.EncodeBucketKeyForUrl(result.BucketKey)}";
            log.Info($"Saving object URI to variable \"Octopus.Action[{actionName}].Output.{packageUriVariableName}\"");
            log.SetOutputVariableButDoNotAddToVariables(packageUriVariableName, packageUriVariableValue);

            //  ARN format: `arn:aws:s3:::bucket-name/key` (Note: China (Beijing region (cn-north-1) uses `aws-cn` instead of `aws`)
            var packageArnVariableName = index.IsNullOrEmpty() ? "Package.Arn" : $"Package.Arn[{index}]";
            var packageArnVariableValue = $"arn:{(awsEnvironmentGeneration.AwsRegion.SystemName.Equals("cn-north-1") ? "aws-cn" : "aws")}:s3:::{result.BucketName}/{result.BucketKey}";
            log.Info($"Saving object ARN to variable \"Octopus.Action[{actionName}].Output.{packageArnVariableName}\"");
            log.SetOutputVariableButDoNotAddToVariables(packageArnVariableName, packageArnVariableValue);

            var packageObjectVersionVariableName = index.IsNullOrEmpty() ? "Package.ObjectVersion" : $"Package.ObjectVersion[{index}]";
            var packageObjectVersionVariableValue = result.Version;
            log.Info($"Saving object version id to variable \"Octopus.Action[{actionName}].Output.{packageObjectVersionVariableName}\"");
            log.SetOutputVariableButDoNotAddToVariables(packageObjectVersionVariableName, packageObjectVersionVariableValue);
        }

        private static void ThrowInvalidFileUpload(Exception exception, string message)
        {
            throw new AmazonFileUploadException(message, exception);
        }

        void WarnAndIgnoreException(Exception exception, string message)
        {
            log.Warn(message);
        }

        async Task<IEnumerable<S3UploadResult>> UploadAll(IEnumerable<S3TargetPropertiesBase> options, Func<AmazonS3Client> clientFactory, RunningDeployment deployment)
        {
            var result = new List<S3UploadResult>();
            foreach (var option in options)
            {
                switch (option)
                {
                    case S3PackageOptions package:
                        result.Add(await UploadUsingPackage(clientFactory, deployment, package));
                        break;
                    case S3SingleFileSelectionProperties selection:
                        result.Add(await UploadSingleFileSelection(clientFactory, deployment, selection));
                        break;
                    case S3MultiFileSelectionProperties selection:
                        result.AddRange(await UploadMultiFileSelection(clientFactory, deployment, selection));
                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// Uploads multiple files given the globbing patterns provided by the selection properties.
        /// </summary>
        /// <param name="clientFactory"></param>
        /// <param name="deployment"></param>
        /// <param name="selection"></param>
        private async Task<IEnumerable<S3UploadResult>> UploadMultiFileSelection(Func<AmazonS3Client> clientFactory, RunningDeployment deployment, S3MultiFileSelectionProperties selection)
        {
            Guard.NotNull(deployment, "Deployment may not be null");
            Guard.NotNull(selection, "Multi file selection properties may not be null");
            Guard.NotNull(clientFactory, "Client factory must not be null");
            var results = new List<S3UploadResult>();

            var files = new RelativeGlobber(
                (@base, pattern) => fileSystem.EnumerateFilesWithGlob(@base, pattern),
                deployment.StagingDirectory).EnumerateFilesWithGlob(selection.Pattern).ToList();

            if (!files.Any())
            {
                log.Info($"The glob pattern '{selection.Pattern}' didn't match any files. Nothing was uploaded to S3.");
                return results;
            }

            log.Info($"Glob pattern '{selection.Pattern}' matched {files.Count} files");
            var substitutionPatterns = SplitFilePatternString(selection.VariableSubstitutionPatterns);

            if(substitutionPatterns.Any())
                substituteInFiles.Substitute(deployment.CurrentDirectory, substitutionPatterns);

            var structuredSubstitutionPatterns = SplitFilePatternString(selection.StructuredVariableSubstitutionPatterns);

            if(structuredSubstitutionPatterns.Any())
                structuredConfigVariablesService.ReplaceVariables(deployment.CurrentDirectory, structuredSubstitutionPatterns.ToList());

            foreach (var matchedFile in files)
            {
                var request = CreateRequest(matchedFile.FilePath,$"{selection.BucketKeyPrefix}{matchedFile.MappedRelativePath}", selection);
                LogPutObjectRequest(matchedFile.FilePath, request);

                results.Add(await HandleUploadRequest(clientFactory(), request, WarnAndIgnoreException));
            }

            return results;
        }

        string[] SplitFilePatternString(string patterns) => patterns?.Split(new[] { "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries) ?? new string[0];

        /// <summary>
        /// Uploads a single file with the given properties
        /// </summary>
        /// <param name="clientFactory"></param>
        /// <param name="deployment"></param>
        /// <param name="selection"></param>
        public Task<S3UploadResult> UploadSingleFileSelection(Func<AmazonS3Client> clientFactory, RunningDeployment deployment, S3SingleFileSelectionProperties selection)
        {
            Guard.NotNull(deployment, "Deployment may not be null");
            Guard.NotNull(selection, "Single file selection properties may not be null");
            Guard.NotNull(clientFactory, "Client factory must not be null");

            var filePath = Path.Combine(deployment.StagingDirectory, selection.Path);

            if (!fileSystem.FileExists(filePath))
            {
                throw new FileNotFoundException($"The file {selection.Path} could not be found in the package.");
            }

            if(selection.PerformVariableSubstitution)
                substituteInFiles.Substitute(deployment.CurrentDirectory, new List<string>{ filePath });

            if(selection.PerformStructuredVariableSubstitution)
                structuredConfigVariablesService.ReplaceVariables(deployment.CurrentDirectory, new List<string>{ filePath });

            return CreateRequest(filePath, GetBucketKey(filePath.AsRelativePathFrom(deployment.StagingDirectory), selection), selection)
                    .Tee(x => LogPutObjectRequest(filePath, x))
                    .Map(x => HandleUploadRequest(clientFactory(), x, ThrowInvalidFileUpload));
        }

        /// <summary>
        /// Uploads the given package file with the provided package options
        /// </summary>
        /// <param name="clientFactory"></param>
        /// <param name="deployment"></param>
        /// <param name="options"></param>
        public Task<S3UploadResult> UploadUsingPackage(Func<AmazonS3Client> clientFactory, RunningDeployment deployment, S3PackageOptions options)
        {
            Guard.NotNull(deployment, "Deployment may not be null");
            Guard.NotNull(options, "Package options may not be null");
            Guard.NotNull(clientFactory, "Client factory must not be null");

            var targetArchivePath = PerformVariableReplacement(deployment, options);

            var filename = GetNormalizedPackageFilename(deployment);

            return CreateRequest(targetArchivePath,
                    GetBucketKey(filename, options, targetArchivePath), options)
                .Tee(x => LogPutObjectRequest("entire package", x))
                .Map(x => HandleUploadRequest(clientFactory(), x, ThrowInvalidFileUpload));
        }

        string PerformVariableReplacement(RunningDeployment deployment, S3PackageOptions options)
        {
            Guard.NotNull(deployment.StagingDirectory, "deployment.StagingDirectory must not be null");

            if (options.VariableSubstitutionPatterns.IsNullOrEmpty() && options.StructuredVariableSubstitutionPatterns.IsNullOrEmpty())
                return deployment.PackageFilePath;

            var stagingDirectory = deployment.StagingDirectory;
            var substitutionPatterns = SplitFilePatternString(options.VariableSubstitutionPatterns);
            substituteInFiles.Substitute(stagingDirectory, substitutionPatterns);

            var structuredSubstitutionPatterns = SplitFilePatternString(options.StructuredVariableSubstitutionPatterns);
            structuredConfigVariablesService.ReplaceVariables(stagingDirectory, structuredSubstitutionPatterns.ToList());

            return Repackage(stagingDirectory, Path.GetFileName(deployment.PackageFilePath));
        }

        string Repackage(string stagingDirectory, string fileName)
        {
            var supportedZipExtensions = new[] {".nupkg", ".zip"};
            var supportedJavaExtensions = new[] {".jar", ".war", ".ear", ".rar"};
            var supportedTarExtensions = new[] {".tar"};
            var supportedTarGZipExtensions = new[] { ".tgz", ".tar.gz", ".tar.Z"};
            var supportedTarBZip2Extensions = new[] { "tar.bz", ".tar.bz2", ".tbz"};

            var lowercasedFileName = fileName.ToLower();
            using (var targetArchive = fileSystem.CreateTemporaryFile(string.Empty, out var targetArchivePath))
            {
                if (supportedZipExtensions.Any(lowercasedFileName.EndsWith) || supportedJavaExtensions.Any(lowercasedFileName.EndsWith))
                {
                    using (var archive = ZipArchive.Create())
                    {
                        archive.AddAllFromDirectory(stagingDirectory);
                        archive.SaveTo(targetArchive, CompressionType.Deflate);
                    }
                }
                else if (supportedTarExtensions.Any(lowercasedFileName.EndsWith))
                {
                    using (var archive = TarArchive.Create())
                    {
                        archive.AddAllFromDirectory(stagingDirectory);
                        archive.SaveTo(targetArchive, CompressionType.None);
                    }
                }
                else if (supportedTarGZipExtensions.Any(lowercasedFileName.EndsWith))
                {
                    using (var archive = TarArchive.Create())
                    {
                        archive.AddAllFromDirectory(stagingDirectory);
                        archive.SaveTo(targetArchive, CompressionType.GZip);
                    }
                }
                else if (supportedTarBZip2Extensions.Any(lowercasedFileName.EndsWith))
                {
                    using (var archive = TarArchive.Create())
                    {
                        archive.AddAllFromDirectory(stagingDirectory);
                        archive.SaveTo(targetArchive, CompressionType.BZip2);
                    }
                }
                else
                {
                    throw new ArgumentException($"Unable to compress file {fileName.ToLower()}, the extension is unsupported.");
                }

                return targetArchivePath;
            }
        }

        public string GetNormalizedPackageFilename(RunningDeployment deployment)
        {
            var id = deployment.Variables.Get(PackageVariables.PackageId);
            var version = deployment.Variables.Get(PackageVariables.PackageVersion);
            var extension = GetPackageExtension(deployment);
            return $"{id}.{version}{extension}";
        }

        static string GetPackageExtension(RunningDeployment deployment)
        {
            return PackageName.TryMatchTarExtensions(Path.GetFileName(deployment.PackageFilePath) ?? "", out _, out var extension)
                ? extension
                : Path.GetExtension(deployment.PackageFilePath);
        }

        /// <summary>
        /// Creates an upload file request based on the s3 target properties for a given file and bucket key.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="bucketKey"></param>
        /// <param name="properties"></param>
        /// <returns>PutObjectRequest with all information including metadata and tags from provided properties</returns>
        private PutObjectRequest CreateRequest(string path, string bucketKey, S3TargetPropertiesBase properties)
        {
            Guard.NotNullOrWhiteSpace(path, "The given path may not be null");
            Guard.NotNullOrWhiteSpace(bucket, "The provided bucket key may not be null");
            Guard.NotNull(properties, "Target properties may not be null");

            var request = new PutObjectRequest
                {
                    FilePath = path,
                    BucketName = bucket?.Trim(),
                    Key = bucketKey?.Trim(),
                    StorageClass = S3StorageClass.FindValue(properties.StorageClass?.Trim()),
                    CannedACL = S3CannedACL.FindValue(properties.CannedAcl?.Trim())
                }
                .WithMetadata(properties)
                .WithTags(properties);

            return md5HashSupported ? request.WithMd5Digest(fileSystem) : request;
        }

        string GetBucketKey(string defaultKey, IHaveBucketKeyBehaviour behaviour, string packageFilePath = "")
        {
            return bucketKeyProvider.GetBucketKey(defaultKey, behaviour, packageFilePath);
        }

        /// <summary>
        /// Displays the current information regarding the object that will be uploaded to the user.
        /// </summary>
        /// <param name="fileOrPackageDescription"></param>
        /// <param name="request"></param>
        void LogPutObjectRequest(string fileOrPackageDescription, PutObjectRequest request)
        {
            log.Info($"Attempting to upload {fileOrPackageDescription} to bucket {request.BucketName} with key {request.Key}.");
        }

        /// <summary>
        /// Handle the file upload request throwing exceptions only on errors from AWS which is critical enough to fail
        /// the entire deployment i.e. access denied while per file errors will result in warnings.
        /// </summary>
        /// <param name="client">The client to use</param>
        /// <param name="request">The request to send</param>
        /// <param name="errorAction">Action to take on per file error</param>
        private async Task<S3UploadResult> HandleUploadRequest(AmazonS3Client client, PutObjectRequest request, Action<AmazonS3Exception, string> errorAction)
        {
            try
            {
                if (!await ShouldUpload(client, request))
                {
                    log.Verbose(
                        $"Object key {request.Key} exists for bucket {request.BucketName} with same content hash and metadata. Skipping upload.");
                    return new S3UploadResult(request, Maybe<PutObjectResponse>.None);
                }

                return new S3UploadResult(request, Maybe<PutObjectResponse>.Some(await client.PutObjectAsync(request)));
            }
            catch (AmazonS3Exception ex)
            {
                var permissions = new List<string> {"s3:PutObject"};
                if (request.TagSet.Count > 0)
                {
                    permissions.Add("s3:PutObjectTagging");
                    permissions.Add("s3:PutObjectVersionTagging");
                }

                if (ex.ErrorCode == "AccessDenied")
                    throw new PermissionException(
                        "The AWS account used to perform the operation does not have the required permissions to upload to the bucket.\n" +
                        $"Please ensure the current account has permission to perform action(s) {string.Join(", ", permissions)}'.\n" +
                        ex.Message + "\n");

                if (!perFileUploadErrors.ContainsKey(ex.ErrorCode)) throw;
                perFileUploadErrors[ex.ErrorCode](request, ex).Tee((message) => errorAction(ex, message));
                return new S3UploadResult(request, Maybe<PutObjectResponse>.None);
            }
            catch (ArgumentException exception)
            {
                throw new AmazonFileUploadException($"An error occurred uploading file with bucket key {request.Key} possibly due to metadata. Metadata keys must be valid HTTP header values. \n" +
                                                    "Metadata:\n" + request.Metadata.Keys.Aggregate(string.Empty, (values, key) => $"{values}'{key}' = '{request.Metadata[key]}'\n") + "\n" +
                                                    $"Please see the {log.FormatLink("https://g.octopushq.com/AwsS3UsingMetadata", "AWS documentation")} for more information."
                    , exception);
            }
        }

        /// <summary>
        /// Check whether the object key exists and hash is equivalent. If these are the same we will skip the upload.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        private async Task<bool> ShouldUpload(AmazonS3Client client, PutObjectRequest request)
        {
            //This isn't ideal, however the AWS SDK doesn't really provide any means to check the existence of an object.
            try
            {
                if (!md5HashSupported)
                    return true;

                var metadataResponse = await client.GetObjectMetadataAsync(request.BucketName, request.Key);
                return !metadataResponse.GetEtag().IsSameAsRequestMd5Digest(request) || !request.HasSameMetadata(metadataResponse);
            }
            catch (AmazonServiceException exception)
            {
                if (exception.StatusCode == HttpStatusCode.NotFound)
                {
                    return true;
                }

                throw;
            }
        }

        /// <summary>
        /// The AmazonServiceException can hold additional information that is useful to include in
        /// the log.
        /// </summary>
        /// <param name="exception">The exception</param>
        private void HandleAmazonServiceException(AmazonServiceException exception)
        {
            ((exception.InnerException as WebException)?
             .Response?
             .GetResponseStream()?
             .Map(stream => new StreamReader(stream).ReadToEnd())
             .Map(message => "An exception was thrown while contacting the AWS API.\n" + message)
             ?? "An exception was thrown while contacting the AWS API.")
                .Tee(log.Warn);
        }
    }
 }