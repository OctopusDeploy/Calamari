#if USE_NUGET_V3_LIBS

using System;
using System.Net;
using System.Threading;
using NuGet.Configuration;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using System.IO;
using Calamari.Common.Plumbing.Extensions;
using Octopus.Versioning;
using System.Threading.Tasks;
using NuGet.Commands;
using PackageIdentity = NuGet.Packaging.Core.PackageIdentity;

namespace Calamari.Integration.Packages.NuGet
{
    public class NuGetV3LibDownloader
    {
        private const string AuthenticationType = "basic";

        public static void DownloadPackage(string packageId, IVersion version, Uri feedUri, ICredentials feedCredentials, string targetFilePath)
        {
            var logger = new NugetLogger();
            var sourceRepository = Repository.Factory.GetCoreV3(feedUri.AbsoluteUri);

            if (feedCredentials != null)
            {
                var cred = feedCredentials.GetCredential(feedUri, AuthenticationType);
                sourceRepository.PackageSource.Credentials = new PackageSourceCredential("octopus", cred.UserName, cred.Password, true, AuthenticationType);
            }

            var providers = new SourceRepositoryDependencyProvider(sourceRepository, logger, new SourceCacheContext { NoCache = true }, ignoreFailedSources: true, ignoreWarning: true);
            var targetPath = Directory.GetParent(targetFilePath).FullName;

            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            string targetTempNupkg = Path.Combine(targetPath, Path.GetRandomFileName());

            var downloader =
                providers.GetPackageDownloaderAsync(
                    new PackageIdentity(packageId, version.ToNuGetVersion()),
                    new SourceCacheContext { NoCache = true },
                    logger,
                    CancellationToken.None)
                        .GetAwaiter().GetResult();

            downloader.CopyNupkgFileToAsync(targetTempNupkg, CancellationToken.None).GetAwaiter().GetResult();

            File.Move(targetTempNupkg, targetFilePath);
        }

        public class NugetLogger : ILogger
        {
            public void LogDebug(string data) => Common.Plumbing.Logging.Log.Verbose(data);
            public void LogVerbose(string data) => Common.Plumbing.Logging.Log.Verbose(data);
            public void LogInformation(string data) => Common.Plumbing.Logging.Log.Info(data);
            public void LogMinimal(string data) => Common.Plumbing.Logging.Log.Verbose(data);
            public void LogWarning(string data) => Common.Plumbing.Logging.Log.Warn(data);
            public void LogError(string data) => Common.Plumbing.Logging.Log.Error(data);
            public void LogSummary(string data) => Common.Plumbing.Logging.Log.Info(data);
            public void LogInformationSummary(string data) => Common.Plumbing.Logging.Log.Info(data);
            public void LogErrorSummary(string data) => Common.Plumbing.Logging.Log.Error(data);

            public void Log(LogLevel level, string data)
            {
                GetLogMethodByLevel(level).Invoke(data);
            }

            public Task LogAsync(LogLevel level, string data)
            {
                GetLogMethodByLevel(level).Invoke(data);
                return Task.CompletedTask;
            }

            public void Log(ILogMessage message)
            {
                GetLogMethodByLevel(message.Level).Invoke(message.Message);
            }

            public Task LogAsync(ILogMessage message)
            {
                GetLogMethodByLevel(message.Level).Invoke(message.Message);
                return Task.CompletedTask;
            }

            private static Action<string> GetLogMethodByLevel(LogLevel level)
            {
                switch (level)
                {
                    case LogLevel.Debug:
                        return Common.Plumbing.Logging.Log.Verbose;
                    case LogLevel.Verbose:
                        return Common.Plumbing.Logging.Log.Verbose;
                    case LogLevel.Information:
                        return Common.Plumbing.Logging.Log.Info;
                    case LogLevel.Minimal:
                        return Common.Plumbing.Logging.Log.Verbose;
                    case LogLevel.Warning:
                        return Common.Plumbing.Logging.Log.Warn;
                    case LogLevel.Error:
                        return Common.Plumbing.Logging.Log.Error;
                    default:
                        throw new NotSupportedException($"The log level {level} is not supported.");
                }
            }
        }
    }
}

#endif