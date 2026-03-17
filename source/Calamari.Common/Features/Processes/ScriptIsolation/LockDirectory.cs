using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

    public static LockDirectory GetLockDirectory(string candidatePath)
    {
        var mountedDrives = MountedDrives.Get();

        var candidateDrive = mountedDrives.GetAssociatedDrive(candidatePath);

        if (candidateDrive.LockSupport == LockCapability.Supported)
        {
            return Supported(candidatePath);
        }

        string? tempPathExclusiveOnly = null;

        // Fall back immediately to somewhere under the temp directory
        var tempCandidates = GetTemporaryCandidates(candidatePath);
        foreach (var tempPath in tempCandidates)
        {
            var tempDrive = mountedDrives.GetAssociatedDrive(tempPath)
                                          .DetectLockSupport(tempPath);
            if (tempDrive.LockSupport == LockCapability.Supported)
            {
                return Supported(tempPath);
            }

            if (tempDrive.LockSupport == LockCapability.ExclusiveOnly)
            {
                // Catch the first temp path that supports exclusive locking
                tempPathExclusiveOnly ??= tempPath;
            }
        }

        // Go back to the original drive and check its support
        candidateDrive = candidateDrive.DetectLockSupport(candidatePath);
        if (candidateDrive.LockSupport == LockCapability.Supported)
        {
            return Supported(candidatePath);
        }

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

    static LockDirectory Supported(string path)
    {
        return new(
                   DirectoryInfo: new DirectoryInfo(path),
                   LockSupport: LockCapability.Supported
                  );
    }

    static string[] GetTemporaryCandidates(string candidatePath)
    {
        var pathNamespace = Path.GetFileName(candidatePath);
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Path.Combine(
                                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                            "Calamari",
                                            pathNamespace
                                           );
            var windowsTempPath = Path.GetTempPath();
            return [localAppData, windowsTempPath];
        }

        var tempDirs = new List<string>();
        var tmpDir = Environment.GetEnvironmentVariable("TMPDIR");
        if (!string.IsNullOrWhiteSpace(tmpDir))
        {
            tempDirs.Add(Path.Combine(tmpDir,  pathNamespace));
        }

        const string tmp = "/tmp";

        if (Directory.Exists(tmp))
        {
            tempDirs.Add(Path.Combine(tmp, pathNamespace));
        }

        return tempDirs.ToArray();
    }

    sealed record MountedDrives(CachedDriveInfo[] Drives)
    {
        public static MountedDrives Get()
        {
            try
            {
                var drives = DriveInfo.GetDrives()
                                      .Select(CachedDriveInfo.From)
                                      .OrderBy(d => d.RootDirectory.FullName)
                                      .ToArray();
                return new MountedDrives(drives);
            }
            catch
            {
                // Let's think about what we really want to do if this happens
                return new MountedDrives([]);
            }
        }

        public CachedDriveInfo GetAssociatedDrive(string path)
        {
            var result = Drives
                             .Where(d => IsAncestor(d.RootDirectory, path))
                             .OrderByDescending(d => d.RootDirectory.FullName.Length)
                             .FirstOrDefault();
            if (result is not null)
            {
                return result;
            }

            throw new DirectoryNotFoundException($"Unable to find the drive for '{path}'.");
        }

        static bool IsAncestor(DirectoryInfo ancestor, string path)
        {
            var ancestorPath = ancestor.FullName;
            if (!ancestorPath.EndsWith(Path.DirectorySeparatorChar))
            {
                ancestorPath += Path.DirectorySeparatorChar;
            }
            return path.StartsWith(ancestorPath, StringComparison.OrdinalIgnoreCase);
        }
    }

    sealed record CachedDriveInfo(
        DirectoryInfo RootDirectory,
        string Format,
        DriveType DriveType,
        LockCapability? DetectedLockSupport = null
    )
    {
        const string UnknownFormat = "unknown";

        public static CachedDriveInfo From(DriveInfo driveInfo)
        {
            // These should not throw
            var rootDirectory = driveInfo.RootDirectory;
            var driveType = driveInfo.DriveType;
            try
            {
                var format = driveInfo.DriveFormat; // May throw
                return new CachedDriveInfo(rootDirectory, format, driveType);
            }
            catch
            {
                // If it is throwing an error here, don't trust it for locking
                return new CachedDriveInfo(
                                           RootDirectory: rootDirectory,
                                           Format: UnknownFormat,
                                           DriveType: driveType,
                                           DetectedLockSupport: LockCapability.Unsupported
                                          );
            }
        }

        public LockCapability LockSupport
        {
            get
            {
                if (DetectedLockSupport is not null)
                {
                    return DetectedLockSupport.Value;
                }

                switch (DriveType)
                {
                    case DriveType.Network:
                        return LockCapability.Unknown; // Default to assuming network is unsupported
                    default:  // Explicitly falling through to format inspection
                        break;
                }

                switch (Format.ToLowerInvariant())
                {
                    case "apfs":
                    case "btrfs":
                    case "ext4":
                    case "hfs+":
                    case "ntfs":
                    case "tmpfs":
                    case "xfs":
                    case "zfs":
                        return LockCapability.Supported;
                    default:
                        return LockCapability.Unknown;
                }
            }
        }

        public CachedDriveInfo DetectLockSupport(string lockDirectory)
        {
            if (LockSupport != LockCapability.Unknown)
            {
                return this;
            }

            var testFile =
                new LockDirectory(
                                  DirectoryInfo: new DirectoryInfo(lockDirectory),
                                  LockCapability.Unknown
                                 ).GetLockFile(
                                               $"locktest-{Guid.NewGuid():N}.tmp"
                                              );
            try
            {
                Directory.CreateDirectory(lockDirectory);
                var supportsExclusiveLock = TestExclusiveLock(testFile);
                if (!supportsExclusiveLock)
                {
                    return this with
                    {
                        DetectedLockSupport = LockCapability.Unsupported
                    };
                }

                // From here on we know we at least support exclusive locks
                var unsupported = this with
                {
                    DetectedLockSupport = LockCapability.ExclusiveOnly
                };

                var supportsSharedLock = TestSharedLock(testFile);
                if (!supportsSharedLock)
                {
                    return unsupported;
                }

                var supportsExclusiveBlocksShared = TestExclusiveBlocksShared(testFile);
                if (!supportsExclusiveBlocksShared)
                {
                    return unsupported;
                }

                var supportsSharedBlocksExclusive = TestSharedBlocksExclusive(testFile);
                if (!supportsSharedBlocksExclusive)
                {
                    return unsupported;
                }

                return this with { DetectedLockSupport = LockCapability.Supported };
            }
            catch
            {
                return this with
                {
                    DetectedLockSupport = LockCapability.Unsupported
                };
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

        static bool TestExclusiveLock(LockFile testFile)
        {
            var lockOptions = new LockOptions(
                                              Type: LockType.Exclusive,
                                              Name: "ExclusiveLockTest",
                                              LockFile: testFile,
                                              Timeout: TimeSpan.Zero
                                             );
            try
            {
                using var initialAcquire = FileLock.Acquire(lockOptions);
                try
                {
                    using var secondAcquire = FileLock.Acquire(lockOptions);
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

        static bool TestSharedLock(LockFile testFile)
        {
            var lockOptions = new LockOptions(
                                              Type: LockType.Shared,
                                              Name: "SharedLockTest",
                                              LockFile: testFile,
                                              Timeout: TimeSpan.Zero
                                             );
            try
            {
                using var initialAcquire = FileLock.Acquire(lockOptions);
                using var secondAcquire = FileLock.Acquire(lockOptions);
                return true;
            }
            catch
            {
                // No errors should occur acquiring either lock
                return false;
            }
        }

        static bool TestExclusiveBlocksShared(LockFile testFile)
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
                using var exclusiveAcquire = FileLock.Acquire(exclusiveLockOptions);
                try
                {
                    using var sharedAcquire = FileLock.Acquire(sharedLockOptions);
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

        static bool TestSharedBlocksExclusive(LockFile testFile)
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
                using var sharedAcquire = FileLock.Acquire(sharedLockOptions);
                try
                {
                    using var exclusiveAcquire = FileLock.Acquire(exclusiveLockOptions);
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
}
