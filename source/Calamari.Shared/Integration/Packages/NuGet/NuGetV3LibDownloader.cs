#if USE_NUGET_V3_LIBS

using System;
using System.Net;
using System.Threading;
using NuGet.Configuration;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using System.IO;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Extensions;
using NuGet.DependencyResolver;
using NuGet.LibraryModel;
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
                sourceRepository.PackageSource.Credentials = new PackageSourceCredential("octopus", cred.UserName, cred.Password, true);
            }

            using (var sourceCacheContext = new SourceCacheContext() { NoCache = true })
            {
                var providers = new SourceRepositoryDependencyProvider(sourceRepository, logger, sourceCacheContext);
                var libraryIdentity = new LibraryIdentity(packageId, version.ToNuGetVersion(), LibraryType.Package);

                var targetPath = Directory.GetParent(targetFilePath).FullName;
                if (!Directory.Exists(targetPath))
                {
                    Directory.CreateDirectory(targetPath);
                }

                string targetTempNupkg = Path.Combine(targetPath, Path.GetRandomFileName());
                using (var nupkgStream = new FileStream(targetTempNupkg,
                                                        FileMode.Create,
                                                        FileAccess.ReadWrite,
                                                        FileShare.ReadWrite | FileShare.Delete,
                                                        4096,
                                                        true))
                {
                    providers.CopyToAsync(libraryIdentity, nupkgStream, CancellationToken.None)
                             .GetAwaiter()
                             .GetResult();
                }

                File.Move(targetTempNupkg, targetFilePath);
            }
        }

        public class NugetLogger : ILogger
        {
            public void LogDebug(string data) => Log.Verbose(data);
            public void LogVerbose(string data) => Log.Verbose(data);
            public void LogInformation(string data) => Log.Info(data);
            public void LogMinimal(string data) => Log.Verbose(data);
            public void LogWarning(string data) => Log.Warn(data);
            public void LogError(string data) => Log.Error(data);
            public void LogSummary(string data) => Log.Info(data);
            public void LogInformationSummary(string data) => Log.Info(data);
            public void LogErrorSummary(string data) => Log.Error(data);
        }
    }
}

#endif