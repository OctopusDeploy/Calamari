using System;
using System.IO;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public sealed record LockDirectory(
    DirectoryInfo DirectoryInfo,
    LockCapability LockSupport
)
{
    public LockFile GetLockFile(string name)
    {
        return LockFile.FromDirectory(this, name);
    }

    public bool Supports(LockType lockType)
    {
        return LockSupport switch
               {
                   LockCapability.Supported => true,
                   LockCapability.ExclusiveOnly => lockType == LockType.Exclusive,
                   _ => false
               };
    }

    static readonly Lazy<MountedDrives> MountedDrivesCache = new(MountedDrives.Get);

    public static LockDirectory GetLockDirectory(string candidatePath)
        => GetLockDirectory(candidatePath, MountedDrivesCache.Value);

    internal static LockDirectory GetLockDirectory(
        string candidatePath,
        MountedDrives mountedDrives,
        IFileLockService? lockService = null,
        IPathResolutionService? pathResolver = null)
    {
        var service = lockService ?? FileLockService.Instance;
        var resolver = pathResolver ?? DefaultPathResolutionService.Instance;

        CachedDriveInfo? TryGetDrive(string path)
        {
            try { return mountedDrives.GetAssociatedDrive(path, resolver); }
            catch (DirectoryNotFoundException) { return null; }
        }

        var candidateDrive = TryGetDrive(candidatePath);

        // Detect lock support on the candidate drive first. If it is fully supported,
        // return immediately — no need to inspect temp directories at all.
        var candidateSupport = candidateDrive?.LockSupport ?? DetectLockSupport(candidatePath, service);
        if (candidateSupport is LockCapability.Supported)
        {
            return Supported(candidatePath);
        }

        string? tempPathExclusiveOnly = null;

        // Candidate is not fully supported; check temp directories for something better.
        foreach (var tempPath in service.GetFallbackTemporaryDirectories(candidatePath))
        {
            var tempDrive = TryGetDrive(tempPath);
            var tempSupport = tempDrive?.LockSupport ?? DetectLockSupport(tempPath, service);
            if (tempSupport is LockCapability.Supported)
            {
                return Supported(tempPath);
            }

            if (tempSupport is LockCapability.ExclusiveOnly)
            {
                // Catch the first temp path that supports exclusive locking
                tempPathExclusiveOnly ??= tempPath;
            }
        }

        if (candidateSupport is LockCapability.ExclusiveOnly)
        {
            // The candidate itself supports exclusive locking — the temp path offers no
            // advantage, so stay on the candidate.
            return new(
                       DirectoryInfo: new DirectoryInfo(candidatePath),
                       LockSupport: LockCapability.ExclusiveOnly
                      );
        }

        // The candidate is Unsupported (or unknown). Only fall back to the temp path if
        // it genuinely gives better support (ExclusiveOnly > Unsupported).
        if (tempPathExclusiveOnly is not null)
        {
            return new(
                       DirectoryInfo: new DirectoryInfo(tempPathExclusiveOnly),
                       LockSupport: LockCapability.ExclusiveOnly
                      );
        }

        return new(
                   DirectoryInfo: new DirectoryInfo(candidatePath),
                   LockSupport: LockCapability.Unsupported
                  );
    }

    public static LockCapability DetectLockSupport(string lockDirectory)
        => DetectLockSupport(lockDirectory, FileLockService.Instance);

    internal static LockCapability DetectLockSupport(
        string lockDirectory,
        IFileLockService lockService)
    {
        var testFile =
            new LockDirectory(
                               DirectoryInfo: new DirectoryInfo(lockDirectory),
                               LockSupport: LockCapability.Supported  // Ensures that lock will be attempted
                              ).GetLockFile(
                                            $"locktest-{Guid.NewGuid():N}.tmp"
                                           );
        try
        {
            lockService.CreateDirectory(lockDirectory);
            var supportsExclusiveLock = TestExclusiveLock(testFile, lockService);
            if (!supportsExclusiveLock)
            {
                return LockCapability.Unsupported;
            }

            // From here on we know we at least support exclusive locks
            var supportsSharedLock = TestSharedLock(testFile, lockService);
            if (!supportsSharedLock)
            {
                return LockCapability.ExclusiveOnly;
            }

            var supportsExclusiveBlocksShared = TestExclusiveBlocksShared(testFile, lockService);
            if (!supportsExclusiveBlocksShared)
            {
                return LockCapability.ExclusiveOnly;
            }

            var supportsSharedBlocksExclusive = TestSharedBlocksExclusive(testFile, lockService);
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

    static bool TestExclusiveLock(LockFile testFile, IFileLockService lockService)
    {
        var lockOptions = new LockOptions(
                                          Type: LockType.Exclusive,
                                          Name: "ExclusiveLockTest",
                                          LockFile: testFile,
                                          Timeout: TimeSpan.Zero
                                         );
        try
        {
            using var initialAcquire = lockService.AcquireLock(lockOptions);
            try
            {
                using var secondAcquire = lockService.AcquireLock(lockOptions);
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

    static bool TestSharedLock(LockFile testFile, IFileLockService lockService)
    {
        var lockOptions = new LockOptions(
                                          Type: LockType.Shared,
                                          Name: "SharedLockTest",
                                          LockFile: testFile,
                                          Timeout: TimeSpan.Zero
                                         );
        try
        {
            using var initialAcquire = lockService.AcquireLock(lockOptions);
            using var secondAcquire = lockService.AcquireLock(lockOptions);
            return true;
        }
        catch
        {
            // No errors should occur acquiring either lock
            return false;
        }
    }

    static bool TestExclusiveBlocksShared(LockFile testFile, IFileLockService lockService)
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
            using var exclusiveAcquire = lockService.AcquireLock(exclusiveLockOptions);
            try
            {
                using var sharedAcquire = lockService.AcquireLock(sharedLockOptions);
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

    static bool TestSharedBlocksExclusive(LockFile testFile, IFileLockService lockService)
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
            using var sharedAcquire = lockService.AcquireLock(sharedLockOptions);
            try
            {
                using var exclusiveAcquire = lockService.AcquireLock(exclusiveLockOptions);
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

    static LockDirectory Supported(string path)
    {
        return new(
                   DirectoryInfo: new DirectoryInfo(path),
                   LockSupport: LockCapability.Supported
                  );
    }
}
