using System;
using System.Collections.Generic;
using System.IO;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

sealed class LockDirectoryFactory(
    IMountedDrivesProvider mountedDrivesProvider,
    IFileLockService fileLockService,
    ITemporaryDirectoryFallbackProvider temporaryDirectoryFallbackProvider
) : ILockDirectoryFactory
{
    const string UnknownFileSystem = "Unknown";

    public LockDirectory Create(DirectoryInfo preferredLockDirectory)
    {
        var mountedDrives =  mountedDrivesProvider.GetMountedDrives();
        var preferredDrive = GetDriveOrNull(preferredLockDirectory);

        // Detect lock support on the preferred drive first. If it is fully supported,
        // return immediately - no need to fall back to temporary directories
        var preferredCapability = preferredDrive?.LockSupport ?? DetectLockSupport(preferredLockDirectory);
        if (preferredCapability is LockCapability.Supported)
        {
            return new LockDirectory(preferredLockDirectory, LockCapability.Supported);
        }

        var fallbackResults = new List<LockSupportDetectionResult>();
        fallbackResults.Add(
                            new LockSupportDetectionResult(
                                                           FallbackType: null,
                                                           FileSystem: preferredDrive?.Format ?? UnknownFileSystem,
                                                           LockCapability: preferredCapability
                                                          )
                           );
        DirectoryInfo? tempPathExclusiveOnly = null;

        // Preferred directory is not fully supported; check temp directories for something better.
        foreach (var tempFallback in temporaryDirectoryFallbackProvider.GetFallbackCandidates(preferredLockDirectory))
        {
            var tempDir = tempFallback.Directory;
            var tempDrive = GetDriveOrNull(tempDir);
            var tempSupport = tempDrive?.LockSupport ?? DetectLockSupport(tempDir);
            fallbackResults.Add(
                                new LockSupportDetectionResult(
                                                               FallbackType: tempFallback.Type,
                                                               FileSystem: tempDrive?.Format ?? UnknownFileSystem,
                                                               LockCapability: tempSupport
                                                              )
                               );
            if (tempSupport is LockCapability.Supported)
            {
                return new LockDirectory(
                                         DirectoryInfo: tempDir,
                                         LockSupport: LockCapability.Supported,
                                         IsFallback: true,
                                         DetectionResults: fallbackResults
                                        );
            }

            if (tempSupport is LockCapability.ExclusiveOnly)
            {
                // Catch the first temp path that supports exclusive locking
                tempPathExclusiveOnly ??= tempDir;
            }
        }

        // We haven't found a supported directory. Return preferred if it at least supported exclusive
        if (preferredCapability is LockCapability.ExclusiveOnly)
        {
            return new LockDirectory(
                                     DirectoryInfo: preferredLockDirectory,
                                     LockSupport: LockCapability.ExclusiveOnly,
                                     IsFallback: false,
                                     DetectionResults: fallbackResults
                                    );
        }

        // The preferred location couldn't be determined to support exclusive locking. If we found
        // a temp path that gives better support (i.e. exclusive), return it instead
        if (tempPathExclusiveOnly is not null)
        {
            return new LockDirectory(
                                     DirectoryInfo: tempPathExclusiveOnly,
                                     LockSupport: LockCapability.ExclusiveOnly,
                                     IsFallback: true,
                                     DetectionResults: fallbackResults
                                    );
        }

        // We don't appear to support locking
        return new LockDirectory(
                                 DirectoryInfo: preferredLockDirectory,
                                 LockSupport: LockCapability.Unsupported,
                                 IsFallback: false,
                                 DetectionResults: fallbackResults
                                );

        CachedDriveInfo? GetDriveOrNull(DirectoryInfo path)
        {
            try
            {
                return mountedDrives.GetAssociatedDrive(path.FullName);
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }
    }

    internal LockCapability DetectLockSupport(DirectoryInfo path)
    {
        var testFile =
            new LockDirectory(
                              directoryInfo: path,
                              lockSupport: LockCapability.Supported  // Ensures that lock will be attempted
                             ).GetLockFile(
                                           $"locktest-{Guid.NewGuid():N}.tmp"
                                          );
        try
        {
            fileLockService.CreateDirectory(path);
            var supportsExclusiveLock = TestExclusiveLock(testFile);
            if (!supportsExclusiveLock)
            {
                return LockCapability.Unsupported;
            }

            // From here on we know we at least support exclusive locks
            var supportsSharedLock = TestSharedLock(testFile);
            if (!supportsSharedLock)
            {
                return LockCapability.ExclusiveOnly;
            }

            var supportsExclusiveBlocksShared = TestExclusiveBlocksShared(testFile);
            if (!supportsExclusiveBlocksShared)
            {
                return LockCapability.ExclusiveOnly;
            }

            var supportsSharedBlocksExclusive = TestSharedBlocksExclusive(testFile);
            if (!supportsSharedBlocksExclusive)
            {
                return LockCapability.ExclusiveOnly;
            }

            return LockCapability.Supported;
        }
        catch
        {
            return LockCapability.Unsupported;
        }
        finally
        {
            try
            {
                testFile.Delete();
            }
            catch
            {
                // ignored
            }
        }
    }

    bool TestExclusiveLock(LockFile testFile)
    {
        var lockOptions = new LockOptions(
                                          Type: LockType.Exclusive,
                                          Name: "ExclusiveLockTest",
                                          LockFile: testFile,
                                          Timeout: TimeSpan.Zero
                                         );
        try
        {
            using var initialAcquire = fileLockService.AcquireLock(lockOptions);
            try
            {
                using var secondAcquire = fileLockService.AcquireLock(lockOptions);
                return false;
            }
            catch (LockRejectedException)
            {
                return true;
            }
        }
        catch
        {
            // Some error occurred acquiring the first lock
            return false;
        }
    }

    bool TestSharedLock(LockFile testFile)
    {
        var lockOptions = new LockOptions(
                                          Type: LockType.Shared,
                                          Name: "SharedLockTest",
                                          LockFile: testFile,
                                          Timeout: TimeSpan.Zero
                                         );
        try
        {
            using var initialAcquire = fileLockService.AcquireLock(lockOptions);
            using var secondAcquire = fileLockService.AcquireLock(lockOptions);
            return true;
        }
        catch
        {
            // No errors should occur acquiring either lock
            return false;
        }
    }

    bool TestExclusiveBlocksShared(LockFile testFile)
    {
        var exclusiveLockOptions = new LockOptions(
                                                   Type: LockType.Exclusive,
                                                   Name: "ExclusiveBlocksSharedTest",
                                                   LockFile: testFile,
                                                   Timeout: TimeSpan.Zero
                                                  );
        var sharedLockOptions = exclusiveLockOptions with { Type = LockType.Shared };

        try
        {
            using var exclusiveAcquire = fileLockService.AcquireLock(exclusiveLockOptions);
            try
            {
                using var sharedAcquire = fileLockService.AcquireLock(sharedLockOptions);
                return false;  // Should not have been able to acquire the lock
            }
            catch (LockRejectedException)
            {
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    bool TestSharedBlocksExclusive(LockFile testFile)
    {
        var exclusiveLockOptions = new LockOptions(
                                                   Type: LockType.Exclusive,
                                                   Name: "SharedBlocksExclusiveTest",
                                                   LockFile: testFile,
                                                   Timeout: TimeSpan.Zero
                                                  );
        var sharedLockOptions = exclusiveLockOptions with { Type = LockType.Shared };

        try
        {
            using var sharedAcquire = fileLockService.AcquireLock(sharedLockOptions);
            try
            {
                using var exclusiveAcquire = fileLockService.AcquireLock(exclusiveLockOptions);
                return false;  // Should not have been able to acquire the lock
            }
            catch (LockRejectedException)
            {
                return true;
            }
        }
        catch
        {
            return false;
        }
    }
}
