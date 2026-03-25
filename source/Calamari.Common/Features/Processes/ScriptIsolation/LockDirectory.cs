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
