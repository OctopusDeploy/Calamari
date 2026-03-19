#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Features.Processes.ScriptIsolation;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ScriptIsolation
{
    [TestFixture]
    [Category(TestCategory.RunOnceOnWindowsAndLinux)]
    public class LockDirectoryFixture
    {
        // -------------------------------------------------------------------------
        // Platform-appropriate path constants
        //
        // CachedDriveInfo is just a record — its RootDirectory is matched by longest
        // string prefix, not by querying the real OS mount table. This means we can
        // construct a MountedDrives with any set of roots (e.g. "/home", "/tmp",
        // "/var") and the prefix-matching logic in GetAssociatedDrive will route
        // paths to the correct fake drive, on any platform.
        //
        // Windows uses drive letters (C:\, D:\) as naturally distinct roots.
        // Non-Windows uses POSIX-style mount points (/home, /tmp) to simulate a
        // realistic multi-filesystem hierarchy.
        //
        // With ITemporaryDirectoryFallback injected, the Group D tests no longer
        // need TempRoots to match what the real TemporaryDirectoryFallback would
        // return — we supply both the fake drives and the fake temp candidates
        // together, so they are always consistent.
        // -------------------------------------------------------------------------

        // Root and path for the primary candidate.
        static readonly string CandidateRoot = OperatingSystem.IsWindows() ? @"C:\" : "/home";
        static readonly string CandidatePath = OperatingSystem.IsWindows()
            ? @"C:\Octopus\Tentacle"
            : "/home/octopus/tentacle";

        // Root used for the fake temp drive in Group D tests.
        static readonly string TempRoot = OperatingSystem.IsWindows() ? @"D:\" : "/tmp";

        // A fake temp path that lives under TempRoot (returned by FakeTemporaryDirectoryFallback).
        static readonly string TempPath = OperatingSystem.IsWindows()
            ? @"D:\Calamari\tentacle"
            : "/tmp/tentacle";

        // A single stable fake root used by Group A/B/C tests.
        static readonly string FakeRoot = CandidateRoot;

        // -------------------------------------------------------------------------
        // FakeLockService — simulates filesystem lock semantics without relying on
        // call order. Tracks currently-held locks and enforces four compatibility
        // rules that together describe any filesystem's locking behaviour.
        // -------------------------------------------------------------------------

        /// <summary>
        /// Simulates the lock-acquisition behaviour of a filesystem.  Each call to
        /// <see cref="AcquireLock"/> checks compatibility rules against currently-held
        /// locks and either returns a handle (which releases the lock on Dispose) or
        /// throws <see cref="LockRejectedException"/>.
        ///
        /// The four boolean flags map directly to real filesystem properties:
        /// <list type="bullet">
        ///   <item><description>
        ///     <c>exclusiveBlocksExclusive</c> — a second exclusive lock is rejected
        ///     while one is already held (any sane filesystem).
        ///   </description></item>
        ///   <item><description>
        ///     <c>sharedAllowed</c> — shared locks can be acquired at all; when
        ///     <c>false</c> every shared-lock attempt throws immediately (e.g. some
        ///     NFS configurations, SMB with oplocks disabled).
        ///   </description></item>
        ///   <item><description>
        ///     <c>exclusiveBlocksShared</c> — a shared lock is rejected while an
        ///     exclusive is held (correct POSIX / NTFS behaviour).
        ///   </description></item>
        ///   <item><description>
        ///     <c>sharedBlocksExclusive</c> — an exclusive lock is rejected while a
        ///     shared is held (correct POSIX / NTFS behaviour).
        ///   </description></item>
        /// </list>
        ///
        /// Preset factory methods cover the most common filesystem profiles:
        /// <see cref="FullySupported"/>,
        /// <see cref="ExclusiveOnlyBecauseSharedUnsupported"/>,
        /// <see cref="ExclusiveOnlyBecauseExclusiveDoesNotBlockShared"/>,
        /// <see cref="ExclusiveOnlyBecauseSharedDoesNotBlockExclusive"/>,
        /// <see cref="Unsupported"/>.
        /// </summary>
        sealed class FakeLockService : IFileLockService
        {
            readonly bool exclusiveBlocksExclusive;
            readonly bool sharedAllowed;
            readonly bool exclusiveBlocksShared;
            readonly bool sharedBlocksExclusive;

            // Tracks counts of currently-held locks (released on handle Dispose).
            int heldExclusive;
            int heldShared;

            FakeLockService(
                bool exclusiveBlocksExclusive,
                bool sharedAllowed,
                bool exclusiveBlocksShared,
                bool sharedBlocksExclusive)
            {
                this.exclusiveBlocksExclusive = exclusiveBlocksExclusive;
                this.sharedAllowed            = sharedAllowed;
                this.exclusiveBlocksShared    = exclusiveBlocksShared;
                this.sharedBlocksExclusive    = sharedBlocksExclusive;
            }

            // ---- Preset factory methods ----------------------------------------

            /// <summary>
            /// A filesystem that supports both exclusive and shared locks with full
            /// mutual-exclusion semantics (NTFS, ext4, apfs, …).
            /// </summary>
            public static FakeLockService FullySupported() =>
                new(exclusiveBlocksExclusive: true,
                    sharedAllowed: true,
                    exclusiveBlocksShared: true,
                    sharedBlocksExclusive: true);

            /// <summary>
            /// A filesystem where shared locks are completely unsupported — every
            /// shared-lock attempt fails immediately.
            /// </summary>
            public static FakeLockService ExclusiveOnlyBecauseSharedUnsupported() =>
                new(exclusiveBlocksExclusive: true,
                    sharedAllowed: false,
                    exclusiveBlocksShared: true,
                    sharedBlocksExclusive: true);

            /// <summary>
            /// A filesystem where shared locks can be acquired, but an exclusive lock
            /// does <em>not</em> block a concurrent shared lock (broken mutual-exclusion).
            /// </summary>
            public static FakeLockService ExclusiveOnlyBecauseExclusiveDoesNotBlockShared() =>
                new(exclusiveBlocksExclusive: true,
                    sharedAllowed: true,
                    exclusiveBlocksShared: false,
                    sharedBlocksExclusive: true);

            /// <summary>
            /// A filesystem where shared locks can be acquired, but a shared lock does
            /// <em>not</em> block a concurrent exclusive lock (broken mutual-exclusion).
            /// </summary>
            public static FakeLockService ExclusiveOnlyBecauseSharedDoesNotBlockExclusive() =>
                new(exclusiveBlocksExclusive: true,
                    sharedAllowed: true,
                    exclusiveBlocksShared: true,
                    sharedBlocksExclusive: false);

            /// <summary>
            /// A filesystem where even exclusive locking is unsupported (e.g. some
            /// network file systems or read-only mounts).
            /// </summary>
            public static FakeLockService Unsupported() =>
                new(exclusiveBlocksExclusive: false,
                    sharedAllowed: false,
                    exclusiveBlocksShared: false,
                    sharedBlocksExclusive: false);

            // ---- IFileLockService implementation --------------------------------

            // Directory creation is a no-op in the fake: the FakeLockService does not
            // touch the real filesystem, so there is nothing to create.
            public void CreateDirectory(string path) { }

            public ILockHandle AcquireLock(LockOptions opts)
            {
                switch (opts.Type)
                {
                    case LockType.Exclusive:
                        if (exclusiveBlocksExclusive && heldExclusive > 0)
                            throw new LockRejectedException("exclusive lock is already held");
                        if (sharedBlocksExclusive && heldShared > 0)
                            throw new LockRejectedException("shared lock blocks exclusive acquisition");
                        if (!exclusiveBlocksExclusive && heldExclusive == 0)
                            // The very first exclusive open failing means the fs doesn't support it.
                            throw new IOException("exclusive locking not supported on this filesystem");
                        heldExclusive++;
                        return new Handle(() => heldExclusive--);

                    case LockType.Shared:
                        if (!sharedAllowed)
                            throw new LockRejectedException("shared locking is not supported on this filesystem");
                        if (exclusiveBlocksShared && heldExclusive > 0)
                            throw new LockRejectedException("exclusive lock blocks shared acquisition");
                        heldShared++;
                        return new Handle(() => heldShared--);

                    default:
                        throw new ArgumentOutOfRangeException(nameof(opts));
                }
            }

            sealed class Handle(Action release) : ILockHandle
            {
                bool disposed;

                public void Dispose()
                {
                    if (disposed) return;
                    disposed = true;
                    release();
                }

                public System.Threading.Tasks.ValueTask DisposeAsync()
                {
                    Dispose();
                    return System.Threading.Tasks.ValueTask.CompletedTask;
                }
            }
        }

        // -------------------------------------------------------------------------
        // FakeTemporaryDirectoryFallback — returns a fixed list of temp candidates
        // without consulting environment variables or the real filesystem.
        // -------------------------------------------------------------------------

        /// <summary>
        /// Returns a caller-supplied list of candidate paths, ignoring the real
        /// environment.  This makes Group D tests independent of $TMPDIR, /tmp
        /// existence, and /dev/shm existence.
        /// </summary>
        sealed class FakeTemporaryDirectoryFallback(params string[] candidates)
            : ITemporaryDirectoryFallback
        {
            public IEnumerable<string> GetCandidates(string candidatePath) => candidates;
        }

        // -------------------------------------------------------------------------
        // Group A: LockDirectory.Supports(LockType)
        // -------------------------------------------------------------------------

        [TestCase(LockCapability.Supported,    LockType.Exclusive, true)]
        [TestCase(LockCapability.Supported,    LockType.Shared,    true)]
        [TestCase(LockCapability.ExclusiveOnly, LockType.Exclusive, true)]
        [TestCase(LockCapability.ExclusiveOnly, LockType.Shared,    false)]
        [TestCase(LockCapability.Unsupported,  LockType.Exclusive, false)]
        [TestCase(LockCapability.Unknown,      LockType.Exclusive, false)]
        public void Supports_ReturnsExpectedResult(
            LockCapability capability, LockType lockType, bool expected)
        {
            var dir = new LockDirectory(new DirectoryInfo(CandidatePath), capability);
            dir.Supports(lockType).Should().Be(expected);
        }

        // -------------------------------------------------------------------------
        // Group B: CachedDriveInfo.LockSupport property
        // -------------------------------------------------------------------------

        [TestCase("ntfs",    DriveType.Fixed,   null,                         LockCapability.Supported)]
        [TestCase("ext4",    DriveType.Fixed,   null,                         LockCapability.Supported)]
        [TestCase("apfs",    DriveType.Fixed,   null,                         LockCapability.Supported)]
        [TestCase("btrfs",   DriveType.Fixed,   null,                         LockCapability.Supported)]
        [TestCase("tmpfs",   DriveType.Fixed,   null,                         LockCapability.Supported)]
        [TestCase("xfs",     DriveType.Fixed,   null,                         LockCapability.Supported)]
        [TestCase("zfs",     DriveType.Fixed,   null,                         LockCapability.Supported)]
        [TestCase("hfs+",    DriveType.Fixed,   null,                         LockCapability.Supported)]
        [TestCase("nfs",     DriveType.Fixed,   null,                         LockCapability.Unknown)]
        [TestCase("nfs",     DriveType.Network, null,                         LockCapability.Unknown)]
        [TestCase("ntfs",    DriveType.Network, null,                         LockCapability.Unknown)]
        [TestCase("unknown", DriveType.Fixed,   LockCapability.Unsupported,   LockCapability.Unsupported)]
        [TestCase("ntfs",    DriveType.Fixed,   LockCapability.ExclusiveOnly, LockCapability.ExclusiveOnly)]
        public void CachedDriveInfo_LockSupport_ReturnsExpectedCapability(
            string format,
            DriveType driveType,
            LockCapability? detectedOverride,
            LockCapability expected)
        {
            var info = new CachedDriveInfo(
                           RootDirectory: new DirectoryInfo(FakeRoot),
                           Format: format,
                           DriveType: driveType,
                           DetectedLockSupport: detectedOverride
                          );
            info.LockSupport.Should().Be(expected);
        }

        // -------------------------------------------------------------------------
        // Group C: CachedDriveInfo.DetectLockSupport with injected FakeLockService
        // -------------------------------------------------------------------------

        // Builds a CachedDriveInfo with LockSupport == Unknown so detection is triggered.
        static CachedDriveInfo UnknownDrive()
            => new(
                   RootDirectory: new DirectoryInfo(FakeRoot),
                   Format: "unknown-fs",
                   DriveType: DriveType.Fixed,
                   DetectedLockSupport: null   // Format is unrecognised → LockSupport == Unknown
                  );

        [Test]
        public void DetectLockSupport_ReturnsUnsupported_WhenExclusiveLockingIsNotSupported()
        {
            var drive = UnknownDrive();
            var fs = FakeLockService.Unsupported();

            var result = drive.DetectLockSupport(Path.GetTempPath(), fs);

            result.LockSupport.Should().Be(LockCapability.Unsupported);
        }

        [Test]
        public void DetectLockSupport_ReturnsExclusiveOnly_WhenSharedLockingIsNotSupported()
        {
            var drive = UnknownDrive();
            var fs = FakeLockService.ExclusiveOnlyBecauseSharedUnsupported();

            var result = drive.DetectLockSupport(Path.GetTempPath(), fs);

            result.LockSupport.Should().Be(LockCapability.ExclusiveOnly);
        }

        [Test]
        public void DetectLockSupport_ReturnsExclusiveOnly_WhenExclusiveLockDoesNotBlockSharedLock()
        {
            // A shared lock can be acquired even while an exclusive lock is held —
            // the filesystem does not enforce mutual exclusion between the two types.
            var drive = UnknownDrive();
            var fs = FakeLockService.ExclusiveOnlyBecauseExclusiveDoesNotBlockShared();

            var result = drive.DetectLockSupport(Path.GetTempPath(), fs);

            result.LockSupport.Should().Be(LockCapability.ExclusiveOnly);
        }

        [Test]
        public void DetectLockSupport_ReturnsExclusiveOnly_WhenSharedLockDoesNotBlockExclusiveLock()
        {
            // An exclusive lock can be acquired even while a shared lock is held —
            // the filesystem does not enforce mutual exclusion between the two types.
            var drive = UnknownDrive();
            var fs = FakeLockService.ExclusiveOnlyBecauseSharedDoesNotBlockExclusive();

            var result = drive.DetectLockSupport(Path.GetTempPath(), fs);

            result.LockSupport.Should().Be(LockCapability.ExclusiveOnly);
        }

        [Test]
        public void DetectLockSupport_ReturnsSupported_WhenFullMutualExclusionIsEnforced()
        {
            // The filesystem correctly blocks all conflicting lock combinations.
            var drive = UnknownDrive();
            var fs = FakeLockService.FullySupported();

            var result = drive.DetectLockSupport(Path.GetTempPath(), fs);

            result.LockSupport.Should().Be(LockCapability.Supported);
        }

        // -------------------------------------------------------------------------
        // Group D: GetLockDirectory path-selection with injected MountedDrives
        //          and ITemporaryDirectoryFallback
        //
        // FakeTemporaryDirectoryFallback supplies a fixed list of temp candidates,
        // removing any dependency on $TMPDIR, /tmp existence, or /dev/shm existence.
        // The corresponding MountedDrives is built to match the injected candidates.
        // -------------------------------------------------------------------------

        // Builds a CachedDriveInfo with a known LockSupport (via DetectedLockSupport) so
        // DetectLockSupport is a no-op (it returns early when LockSupport != Unknown).
        static CachedDriveInfo DriveWithCapability(string rootPath, LockCapability capability)
            => new(
                   RootDirectory: new DirectoryInfo(rootPath),
                   Format: "ntfs",
                   DriveType: DriveType.Fixed,
                   DetectedLockSupport: capability
                  );

        [Test]
        public void GetLockDirectory_ReturnsCandidatePath_WhenCandidateDriveIsSupported()
        {
            var drives = new MountedDrives([
                DriveWithCapability(FakeRoot, LockCapability.Supported)
            ]);
            var fallback = new FakeTemporaryDirectoryFallback(TempPath);

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives,
                temporaryDirectoryFallback: fallback);

            result.DirectoryInfo.FullName.Should().Be(CandidatePath);
            result.LockSupport.Should().Be(LockCapability.Supported);
        }

        [Test]
        public void GetLockDirectory_ReturnsTempPath_WhenCandidateIsUnknownAndTempDriveIsSupported()
        {
            // Candidate root is Unknown; the injected temp path lives on a Supported drive.
            // GetLockDirectory should return the first temp path it finds, not the candidate.
            var drives = new MountedDrives([
                DriveWithCapability(CandidateRoot, LockCapability.Unknown),
                DriveWithCapability(TempRoot, LockCapability.Supported)
            ]);
            var fallback = new FakeTemporaryDirectoryFallback(TempPath);

            // Temp drive is already-detected as Supported so DetectLockSupport is a no-op;
            // no lock service is needed.
            var result = LockDirectory.GetLockDirectory(CandidatePath, drives,
                temporaryDirectoryFallback: fallback);

            result.LockSupport.Should().Be(LockCapability.Supported);
            result.DirectoryInfo.FullName.Should().NotStartWith(CandidateRoot,
                because: "a temp path on the supported drive should be preferred");
        }

        [Test]
        public void GetLockDirectory_ReturnsCandidatePath_WhenAllTempsAreExclusiveOnlyAndCandidateDetectsSupported()
        {
            // Temp drive is pre-detected as ExclusiveOnly; candidate root is Unknown.
            // Detection on the candidate drive returns Supported, so the candidate path
            // should be returned rather than the ExclusiveOnly temp path.
            var drives = new MountedDrives([
                DriveWithCapability(CandidateRoot, LockCapability.Unknown),
                DriveWithCapability(TempRoot, LockCapability.ExclusiveOnly)
            ]);
            var fallback = new FakeTemporaryDirectoryFallback(TempPath);
            var fs = FakeLockService.FullySupported();

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives, fs, fallback);

            result.LockSupport.Should().Be(LockCapability.Supported);
            result.DirectoryInfo.FullName.Should().Be(CandidatePath,
                because: "the candidate detects as Supported which is better than any temp ExclusiveOnly path");
        }

        [Test]
        public void GetLockDirectory_ReturnsCandidatePath_WhenBothCandidateAndTempAreExclusiveOnly()
        {
            // All roots (candidate and temp) are pre-detected as ExclusiveOnly.
            // The temp path offers no better support than the candidate, so the candidate
            // should be returned.
            var drives = new MountedDrives([
                DriveWithCapability(CandidateRoot, LockCapability.ExclusiveOnly),
                DriveWithCapability(TempRoot, LockCapability.ExclusiveOnly)
            ]);
            var fallback = new FakeTemporaryDirectoryFallback(TempPath);

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives,
                temporaryDirectoryFallback: fallback);

            result.LockSupport.Should().Be(LockCapability.ExclusiveOnly);
            result.DirectoryInfo.FullName.Should().Be(CandidatePath,
                because: "the candidate path should be used when temp offers no better support");
        }

        [Test]
        public void GetLockDirectory_ReturnsTempPath_WhenTempIsExclusiveOnlyAndCandidateIsUnsupported()
        {
            // Candidate root is pre-detected as Unsupported; temp drive is pre-detected as
            // ExclusiveOnly. The temp path genuinely offers better support, so it should be
            // preferred over the candidate.
            var drives = new MountedDrives([
                DriveWithCapability(CandidateRoot, LockCapability.Unsupported),
                DriveWithCapability(TempRoot, LockCapability.ExclusiveOnly)
            ]);
            var fallback = new FakeTemporaryDirectoryFallback(TempPath);

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives,
                temporaryDirectoryFallback: fallback);

            result.LockSupport.Should().Be(LockCapability.ExclusiveOnly);
            result.DirectoryInfo.FullName.Should().NotStartWith(CandidateRoot,
                because: "the temp path should be used when it offers better support than the candidate");
        }

        [Test]
        public void GetLockDirectory_ReturnsUnsupported_WhenNothingWorks()
        {
            var drives = new MountedDrives([
                DriveWithCapability(FakeRoot, LockCapability.Unknown)
            ]);
            var fallback = new FakeTemporaryDirectoryFallback(TempPath);
            var fs = FakeLockService.Unsupported();

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives, fs, fallback);

            result.LockSupport.Should().Be(LockCapability.Unsupported);
            result.DirectoryInfo.FullName.Should().Be(CandidatePath);
        }

        [Test]
        public void GetLockDirectory_ReturnsUnsupported_WhenMountedDrivesIsEmpty()
        {
            // No drives at all — GetAssociatedDrive throws DirectoryNotFoundException.
            // GetLockDirectory should catch it and return Unsupported rather than propagating.
            var drives = new MountedDrives([]);
            var fallback = new FakeTemporaryDirectoryFallback(TempPath);

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives,
                temporaryDirectoryFallback: fallback);

            result.LockSupport.Should().Be(LockCapability.Unsupported);
            result.DirectoryInfo.FullName.Should().Be(CandidatePath);
        }

        [Test]
        public void GetLockDirectory_SkipsTempsWithNoMatchingDrive_AndFallsBackToCandidate()
        {
            // The fallback returns a path whose drive is not in MountedDrives at all.
            // TryGetDrive returns null for that temp path, so it is skipped entirely.
            // The candidate root is Unknown → Unsupported after detection.
            var drives = new MountedDrives([
                DriveWithCapability(CandidateRoot, LockCapability.Unknown)
            ]);
            // TempPath is under TempRoot, which has no entry in drives.
            var fallback = new FakeTemporaryDirectoryFallback(TempPath);
            var fs = FakeLockService.Unsupported();

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives, fs, fallback);

            result.LockSupport.Should().Be(LockCapability.Unsupported);
            result.DirectoryInfo.FullName.Should().Be(CandidatePath,
                because: "temp paths with no associated drive should be ignored");
        }

        [Test]
        public void GetLockDirectory_UsesFirstSupportedTempPath_WhenMultipleTempCandidatesExist()
        {
            // Two temp candidates: the first maps to an ExclusiveOnly drive, the second to
            // a Supported drive.  The method should return the first Supported path it finds.
            var secondTempRoot = OperatingSystem.IsWindows() ? @"E:\" : "/dev/shm";
            var secondTempPath = OperatingSystem.IsWindows()
                ? @"E:\tentacle"
                : "/dev/shm/tentacle";

            var drives = new MountedDrives([
                DriveWithCapability(CandidateRoot, LockCapability.Unknown),
                DriveWithCapability(TempRoot, LockCapability.ExclusiveOnly),
                DriveWithCapability(secondTempRoot, LockCapability.Supported)
            ]);
            var fallback = new FakeTemporaryDirectoryFallback(TempPath, secondTempPath);

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives,
                temporaryDirectoryFallback: fallback);

            result.LockSupport.Should().Be(LockCapability.Supported);
            result.DirectoryInfo.FullName.Should().StartWith(secondTempRoot,
                because: "the second temp candidate is on a Supported drive and should be chosen");
        }
    }
}
