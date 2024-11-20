#if USE_NUGET_V3_LIBS

using System;
using System.Net;
using System.Threading;
using NuGet.Configuration;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using System.IO;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Extensions;
using NuGet.Commands;
using NuGet.Packaging.Core;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.NuGet
{
    public class NuGetV3LibDownloader
    {
        public static void DownloadPackage(string packageId, IVersion version, Uri feedUri, ICredentials feedCredentials, string targetFilePath)
        {
            ILogger logger = new NugetLogger();
            var sourceRepository = Repository.Factory.GetCoreV3(feedUri.AbsoluteUri);
            if (feedCredentials != null)
            {
                var cred = feedCredentials.GetCredential(feedUri, "basic");
                sourceRepository.PackageSource.Credentials = new PackageSourceCredential("octopus", cred.UserName, cred.Password, true, null);
            }

            using (var sourceCacheContext = new SourceCacheContext() { NoCache = true })
            {
                var providers = new SourceRepositoryDependencyProvider(sourceRepository, logger, sourceCacheContext, sourceCacheContext.IgnoreFailedSources, false);
                var targetPath = Directory.GetParent(targetFilePath).FullName;
                if (!Directory.Exists(targetPath))
                {
                    Directory.CreateDirectory(targetPath);
                }

                string targetTempNupkg = Path.Combine(targetPath, Path.GetRandomFileName());
                var packageDownloader =  providers.GetPackageDownloaderAsync(new PackageIdentity(packageId, version.ToNuGetVersion()), sourceCacheContext, logger, CancellationToken.None)
                                                      .GetAwaiter()
                                                      .GetResult();
                var fileCopied = packageDownloader.CopyNupkgFileToAsync(targetTempNupkg, CancellationToken.None).GetAwaiter().GetResult();
                
                if (!fileCopied) //I would expect any actual standard exception to be thrown above and not returned as a bool
                {
                    throw new Exception("Unable to download Nupkg file");
                }

                File.Move(targetTempNupkg, targetFilePath);
            }
        }

        public class NugetLogger : ILogger
        {
            public void LogDebug(string data) => Common.Plumbing.Logging.Log.Verbose(data);
            public void LogVerbose(string data) => Common.Plumbing.Logging.Log.Verbose(data);
            public void LogInformation(string data) => Common.Plumbing.Logging.Log.Info(data);
            public void LogMinimal(string data) => Common.Plumbing.Logging.Log.Verbose(data);
            public void LogWarning(string data) => Common.Plumbing.Logging.Log.Warn(data);
            public void LogError(string data) => Common.Plumbing.Logging.Log.Error(data);
            public void LogInformationSummary(string data) => Common.Plumbing.Logging.Log.Info(data);
            public void Log(LogLevel level, string data)
            {
                switch (level)
                {
                    case LogLevel.Debug:
                        LogDebug(data);
                        break;
                    case LogLevel.Verbose:
                        LogVerbose(data);
                        break;
                    case LogLevel.Information:
                        LogInformation(data);
                        break;
                    case LogLevel.Minimal:
                        LogMinimal(data);
                        break;
                    case LogLevel.Warning:
                        LogWarning(data);
                        break;
                    case LogLevel.Error:
                        LogError(data);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(level), level, null);
                }
            }

            public Task LogAsync(LogLevel level, string data)
            {
                this.Log(level, data);
                return Task.CompletedTask;
            }

            public void Log(ILogMessage message)
            {
                this.Log(message.Level, message.Message);
            }

            public Task LogAsync(ILogMessage message)
            {
                this.Log(message);
                return Task.CompletedTask;
            }

        }
    }
}

#endif