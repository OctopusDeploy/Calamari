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

    public static LockDirectory GetLockDirectory(string candidatePath)
        => GetLockDirectory(candidatePath, MountedDrives.Get());

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

        if (candidateDrive?.LockSupport == LockCapability.Supported)
        {
            return Supported(candidatePath);
        }

        string? tempPathExclusiveOnly = null;

        // Fall back immediately to somewhere under the temp directory
        foreach (var tempPath in service.GetFallbackTemporaryDirectories(candidatePath))
        {
            var tempDrive = TryGetDrive(tempPath)
                                ?.DetectLockSupport(tempPath, service);
            if (tempDrive?.LockSupport == LockCapability.Supported)
            {
                return Supported(tempPath);
            }

            if (tempDrive?.LockSupport == LockCapability.ExclusiveOnly)
            {
                // Catch the first temp path that supports exclusive locking
                tempPathExclusiveOnly ??= tempPath;
            }
        }

        // Go back to the original drive and check its support
        var detectedCandidateDrive = candidateDrive
            ?.DetectLockSupport(candidatePath, service);
        if (detectedCandidateDrive?.LockSupport == LockCapability.Supported)
        {
            return Supported(candidatePath);
        }

        if (detectedCandidateDrive?.LockSupport == LockCapability.ExclusiveOnly)
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

    static LockDirectory Supported(string path)
    {
        return new(
                   DirectoryInfo: new DirectoryInfo(path),
                   LockSupport: LockCapability.Supported
                  );
    }
}
