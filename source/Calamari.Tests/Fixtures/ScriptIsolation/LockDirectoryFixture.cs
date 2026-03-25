#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using Calamari.Common.Features.Processes.ScriptIsolation;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;
using StringComparison = System.StringComparison;

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
        // With temp directories now carried by FakeLockService, the Group D tests no longer
        // need TempRoots to match what the real TemporaryDirectoryFallback would
        // return — we supply both the fake drives and the fake temp candidates
        // together via the injected FakeLockService, so they are always consistent.
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
                this.sharedAllowed = sharedAllowed;
                this.exclusiveBlocksShared = exclusiveBlocksShared;
                this.sharedBlocksExclusive = sharedBlocksExclusive;
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

        /// <summary>
        /// Supplies a fixed list of fallback temporary directory candidates, removing
        /// any dependency on environment variables or the real filesystem layout.
        /// </summary>
        sealed class FakeTemporaryDirectoryFallbackProvider(params string[] temporaryDirectories)
            : ITemporaryDirectoryFallbackProvider
        {
            public IEnumerable<DirectoryInfo> GetFallbackCandidates(DirectoryInfo preferredDirectory)
                => Array.ConvertAll(temporaryDirectories, p => new DirectoryInfo(p));
        }

        sealed class FakeMountedDrivesProvider(
            CachedDriveInfo[] cachedDrives,
            IPathResolutionService pathResolutionService
        ) : IMountedDrivesProvider
        {
            public MountedDrives GetMountedDrives() => new(cachedDrives, pathResolutionService);
        }

        static FakeMountedDrivesProvider PassThroughDrives(params CachedDriveInfo[] drives)
            => new(drives, FakePathResolutionService.PassThrough);

        /// <summary>
        /// Builds a <see cref="LockDirectoryFactory"/> wired with the given fake drives and lock
        /// service.  An optional set of <paramref name="tempPaths"/> is supplied to the injected
        /// <see cref="FakeTemporaryDirectoryFallbackProvider"/>.
        /// </summary>
        static LockDirectoryFactory CreateFactory(
            FakeMountedDrivesProvider drives,
            FakeLockService lockService,
            params string[] tempPaths)
            => new(drives, lockService, new FakeTemporaryDirectoryFallbackProvider(tempPaths));

        // -------------------------------------------------------------------------
        // Group A: LockDirectory.Supports(LockType)
        // -------------------------------------------------------------------------

        [TestCase(LockCapability.Supported,    LockType.Exclusive, true)]
        [TestCase(LockCapability.Supported,    LockType.Shared,    true)]
        [TestCase(LockCapability.ExclusiveOnly, LockType.Exclusive, true)]
        [TestCase(LockCapability.ExclusiveOnly, LockType.Shared,    false)]
        [TestCase(LockCapability.Unsupported,  LockType.Exclusive, false)]
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
        [TestCase("nfs",     DriveType.Fixed,   null,                         null)]
        [TestCase("nfs",     DriveType.Network, null,                         null)]
        [TestCase("ntfs",    DriveType.Network, null,                         null)]
        [TestCase("unknown", DriveType.Fixed,   LockCapability.Unsupported,   LockCapability.Unsupported)]
        [TestCase("ntfs",    DriveType.Fixed,   LockCapability.ExclusiveOnly, LockCapability.ExclusiveOnly)]
        public void CachedDriveInfo_LockSupport_ReturnsExpectedCapability(
            string format,
            DriveType driveType,
            LockCapability? detectedOverride,
            LockCapability? expected)
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
        // Group C: LockDirectoryFactory.DetectLockSupport with injected FakeLockService
        // -------------------------------------------------------------------------

        [Test]
        public void DetectLockSupport_ReturnsUnsupported_WhenExclusiveLockingIsNotSupported()
        {
            var fs = FakeLockService.Unsupported();

            var result = CreateFactory(PassThroughDrives(), fs)
                .DetectLockSupport(new DirectoryInfo(Path.GetTempPath()));

            result.Should().Be(LockCapability.Unsupported);
        }

        [Test]
        public void DetectLockSupport_ReturnsExclusiveOnly_WhenSharedLockingIsNotSupported()
        {
            var fs = FakeLockService.ExclusiveOnlyBecauseSharedUnsupported();

            var result = CreateFactory(PassThroughDrives(), fs)
                .DetectLockSupport(new DirectoryInfo(Path.GetTempPath()));

            result.Should().Be(LockCapability.ExclusiveOnly);
        }

        [Test]
        public void DetectLockSupport_ReturnsExclusiveOnly_WhenExclusiveLockDoesNotBlockSharedLock()
        {
            // A shared lock can be acquired even while an exclusive lock is held —
            // the filesystem does not enforce mutual exclusion between the two types.
            var fs = FakeLockService.ExclusiveOnlyBecauseExclusiveDoesNotBlockShared();

            var result = CreateFactory(PassThroughDrives(), fs)
                .DetectLockSupport(new DirectoryInfo(Path.GetTempPath()));

            result.Should().Be(LockCapability.ExclusiveOnly);
        }

        [Test]
        public void DetectLockSupport_ReturnsExclusiveOnly_WhenSharedLockDoesNotBlockExclusiveLock()
        {
            // An exclusive lock can be acquired even while a shared lock is held —
            // the filesystem does not enforce mutual exclusion between the two types.
            var fs = FakeLockService.ExclusiveOnlyBecauseSharedDoesNotBlockExclusive();

            var result = CreateFactory(PassThroughDrives(), fs)
                .DetectLockSupport(new DirectoryInfo(Path.GetTempPath()));

            result.Should().Be(LockCapability.ExclusiveOnly);
        }

        [Test]
        public void DetectLockSupport_ReturnsSupported_WhenFullMutualExclusionIsEnforced()
        {
            // The filesystem correctly blocks all conflicting lock combinations.
            var fs = FakeLockService.FullySupported();

            var result = CreateFactory(PassThroughDrives(), fs)
                .DetectLockSupport(new DirectoryInfo(Path.GetTempPath()));

            result.Should().Be(LockCapability.Supported);
        }

        // -------------------------------------------------------------------------
        // Group D: GetLockDirectory path-selection with injected MountedDrives
        //          and FakeLockService carrying temp directory candidates
        //
        // FakeTemporaryDirectoryFallback supplies a fixed list of temp candidates,
        // removing any dependency on $TMPDIR, /tmp existence, or /dev/shm existence.
        // The corresponding MountedDrives is built to match the injected candidates.
        // -------------------------------------------------------------------------

        // Builds a CachedDriveInfo with a known LockSupport (via DetectedLockSupport) so
        // GetLockDirectory short-circuits to that capability without running detection.
        // Pass null to leave DetectedLockSupport unset and use an unrecognised format so
        // that LockSupport returns null, which triggers live detection.
        static CachedDriveInfo DriveWithCapability(string rootPath, LockCapability? capability)
            => new(
                   RootDirectory: new DirectoryInfo(rootPath),
                   Format: capability is null ? "unknown-fs" : "ntfs",
                   DriveType: DriveType.Fixed,
                   DetectedLockSupport: capability
                  );

        [Test]
        public void GetLockDirectory_ReturnsCandidatePath_WhenCandidateDriveIsSupported()
        {
            var drives = PassThroughDrives(DriveWithCapability(FakeRoot, LockCapability.Supported));
            var fs = FakeLockService.FullySupported();

            var result = CreateFactory(drives, fs, TempPath)
                .Create(new DirectoryInfo(CandidatePath));

            result.DirectoryInfo.FullName.Should().Be(CandidatePath);
            result.LockSupport.Should().Be(LockCapability.Supported);
        }

        [Test]
        public void GetLockDirectory_ReturnsCandidatePath_WhenCandidateDetectsSupportedAndTempDriveIsAlsoSupported()
        {
            // Candidate root has no pre-detected support (detection required); the injected temp path lives on
            // a pre-detected Supported drive. Detection on the candidate drive is performed
            // first: because the lock service is FullySupported, the candidate detects as
            // Supported before the temp directories are even inspected. The candidate path
            // should therefore be returned, not the temp path.
            var drives = PassThroughDrives(
                                           DriveWithCapability(CandidateRoot, null),
                                           DriveWithCapability(TempRoot, LockCapability.Supported));
            var fs = FakeLockService.FullySupported();

            var result = CreateFactory(drives, fs, TempPath)
                .Create(new DirectoryInfo(CandidatePath));

            result.LockSupport.Should().Be(LockCapability.Supported);
            result.DirectoryInfo.FullName.Should().Be(CandidatePath,
                                                      because: "the candidate is detected as Supported first and should be preferred over a temp path");
        }

        [Test]
        public void GetLockDirectory_ReturnsTempPath_WhenCandidateIsUnsupportedAndTempDriveIsSupported()
        {
            // Candidate root is pre-detected as Unsupported (no detection call fires).
            // The injected temp path lives on a pre-detected Supported drive.
            // Because the candidate cannot support locking, the temp path should be chosen.
            var drives = PassThroughDrives(
                                           DriveWithCapability(CandidateRoot, LockCapability.Unsupported),
                                           DriveWithCapability(TempRoot, LockCapability.Supported));
            var fs = FakeLockService.FullySupported();

            var result = CreateFactory(drives, fs, TempPath)
                .Create(new DirectoryInfo(CandidatePath));

            result.LockSupport.Should().Be(LockCapability.Supported);
            result.DirectoryInfo.FullName.Should().NotStartWith(CandidateRoot,
                                                                because: "a temp path on the Supported drive should be preferred when the candidate is Unsupported");
        }

        [Test]
        public void GetLockDirectory_ReturnsCandidatePath_WhenCandidateDetectsSupportedAndTempsAreExclusiveOnly()
        {
            // Temp drive is pre-detected as ExclusiveOnly; candidate root has no pre-detected support.
            // Detection on the candidate drive is performed first: with a FullySupported
            // lock service the candidate detects as Supported, so it is returned
            // immediately without inspecting any temp directories.
            var drives = PassThroughDrives(
                                           DriveWithCapability(CandidateRoot, null),
                                           DriveWithCapability(TempRoot, LockCapability.ExclusiveOnly));
            var fs = FakeLockService.FullySupported();

            var result = CreateFactory(drives, fs, TempPath)
                .Create(new DirectoryInfo(CandidatePath));

            result.LockSupport.Should().Be(LockCapability.Supported);
            result.DirectoryInfo.FullName.Should().Be(CandidatePath,
                                                      because: "the candidate detects as Supported and should be returned before temp directories are considered");
        }

        [Test]
        public void GetLockDirectory_ReturnsCandidatePath_WhenBothCandidateAndTempAreExclusiveOnly()
        {
            // All roots (candidate and temp) are pre-detected as ExclusiveOnly.
            // The temp path offers no better support than the candidate, so the candidate
            // should be returned.
            var drives = PassThroughDrives(
                                           DriveWithCapability(CandidateRoot, LockCapability.ExclusiveOnly),
                                           DriveWithCapability(TempRoot, LockCapability.ExclusiveOnly));
            var fs = FakeLockService.FullySupported();

            var result = CreateFactory(drives, fs, TempPath)
                .Create(new DirectoryInfo(CandidatePath));

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
            var drives = PassThroughDrives(
                                           DriveWithCapability(CandidateRoot, LockCapability.Unsupported),
                                           DriveWithCapability(TempRoot, LockCapability.ExclusiveOnly));
            var fs = FakeLockService.FullySupported();

            var result = CreateFactory(drives, fs, TempPath)
                .Create(new DirectoryInfo(CandidatePath));

            result.LockSupport.Should().Be(LockCapability.ExclusiveOnly);
            result.DirectoryInfo.FullName.Should().NotStartWith(CandidateRoot,
                                                                because: "the temp path should be used when it offers better support than the candidate");
        }

        [Test]
        public void GetLockDirectory_ReturnsUnsupported_WhenNothingWorks()
        {
            var drives = PassThroughDrives(DriveWithCapability(FakeRoot, null));
            var fs = FakeLockService.Unsupported();

            var result = CreateFactory(drives, fs, TempPath)
                .Create(new DirectoryInfo(CandidatePath));

            result.LockSupport.Should().Be(LockCapability.Unsupported);
            result.DirectoryInfo.FullName.Should().Be(CandidatePath);
        }

        [Test]
        public void GetLockDirectory_RunsDetection_WhenMountedDrivesIsEmpty()
        {
            // No drives at all — GetAssociatedDrive throws DirectoryNotFoundException.
            // With no drive heuristic available, GetLockDirectory falls back to live
            // detection. A FullySupported lock service means detection succeeds.
            var drives = PassThroughDrives();
            var fs = FakeLockService.FullySupported();

            var result = CreateFactory(drives, fs, TempPath)
                .Create(new DirectoryInfo(CandidatePath));

            result.LockSupport.Should().Be(LockCapability.Supported);
            result.DirectoryInfo.FullName.Should().Be(CandidatePath);
        }

        [Test]
        public void GetLockDirectory_SkipsTempsWithNoMatchingDrive_AndFallsBackToCandidate()
        {
            // The fallback returns a path whose drive is not in MountedDrives at all.
            // TryGetDrive returns null for that temp path, so it is skipped entirely.
            // The candidate root has no pre-detected support → Unsupported after detection.
            var drives = PassThroughDrives(DriveWithCapability(CandidateRoot, null));
            // TempPath is under TempRoot, which has no entry in drives.
            var fs = FakeLockService.Unsupported();

            var result = CreateFactory(drives, fs, TempPath)
                .Create(new DirectoryInfo(CandidatePath));

            result.LockSupport.Should().Be(LockCapability.Unsupported);
            result.DirectoryInfo.FullName.Should().Be(CandidatePath,
                                                      because: "temp paths with no associated drive should be ignored");
        }

        [Test]
        public void GetLockDirectory_UsesFirstSupportedTempPath_WhenMultipleTempCandidatesExist()
        {
            // Two temp candidates: the first maps to an ExclusiveOnly drive, the second to
            // a Supported drive. The candidate is pre-detected as Unsupported so detection
            // short-circuits there. The method should then return the first Supported temp
            // path it finds.
            var secondTempRoot = OperatingSystem.IsWindows() ? @"E:\" : "/dev/shm";
            var secondTempPath = OperatingSystem.IsWindows()
                ? @"E:\tentacle"
                : "/dev/shm/tentacle";

            var drives = PassThroughDrives(
                                           DriveWithCapability(CandidateRoot, LockCapability.Unsupported),
                                           DriveWithCapability(TempRoot, LockCapability.ExclusiveOnly),
                                           DriveWithCapability(secondTempRoot, LockCapability.Supported));
            var fs = FakeLockService.FullySupported();

            var result = CreateFactory(drives, fs, TempPath, secondTempPath)
                .Create(new DirectoryInfo(CandidatePath));

            result.LockSupport.Should().Be(LockCapability.Supported);
            result.DirectoryInfo.FullName.Should().StartWith(secondTempRoot,
                                                             because: "the second temp candidate is on a Supported drive and should be chosen");
        }

        // -------------------------------------------------------------------------
        // Group E: MountedDrives.GetAssociatedDrive with injected IPathResolutionService
        //
        // These tests exercise the three robustness improvements in isolation:
        //   1. Symlink resolution — input path is resolved before prefix-matching.
        //   2. Platform-aware case comparison — Ordinal vs OrdinalIgnoreCase.
        //   3. Path normalisation — relative paths / ".." components are resolved.
        // All tests are fully hermetic; no real symlinks or filesystem state needed.
        // -------------------------------------------------------------------------

        /// <summary>
        /// A test-double implementation of <see cref="IPathResolutionService"/> that
        /// maps individual BCL primitives to controllable fakes so that the
        /// ancestor-walk logic in <see cref="PathResolutionServiceExtensions.ResolvePath"/>
        /// can be exercised without touching the real filesystem.
        /// </summary>
        /// <param name="pathComparison">Controls <see cref="PathComparison"/>.</param>
        /// <param name="fullPathMap">
        ///   Overrides for <see cref="GetFullPath"/>: maps raw input paths to their
        ///   normalised forms (simulates <c>..</c> / relative-path expansion).
        ///   Paths absent from the map are returned unchanged.
        /// </param>
        /// <param name="symlinkMap">
        ///   Simulated symlinks for <see cref="ResolveLinkTarget"/>: maps a path to
        ///   its symlink target.  A path present in this map causes
        ///   <see cref="ResolveLinkTarget"/> to return a <see cref="FileSystemInfo"/>
        ///   whose <c>FullName</c> is the mapped target.  A path absent from this map
        ///   causes <see cref="ResolveLinkTarget"/> to throw
        ///   <see cref="FileNotFoundException"/>, simulating a non-existent path and
        ///   driving the ancestor-walk in
        ///   <see cref="PathResolutionServiceExtensions.ResolvePath"/>.
        /// </param>
        /// <param name="getFullPathException">
        ///   When non-<c>null</c>, <see cref="GetFullPath"/> throws this exception
        ///   instead of performing a map lookup.  Use this to simulate the documented
        ///   failure modes of <see cref="Path.GetFullPath(string)"/> such as
        ///   <see cref="ArgumentException"/>, <see cref="SecurityException"/>,
        ///   <see cref="NotSupportedException"/>, or
        ///   <see cref="PathTooLongException"/>.
        /// </param>
        sealed class FakePathResolutionService(
            StringComparison pathComparison,
            Dictionary<string, string>? fullPathMap = null,
            Dictionary<string, string>? symlinkMap = null,
            Exception? getFullPathException = null) : IPathResolutionService
        {
            readonly Dictionary<string, string> fullPathMap =
                fullPathMap ?? new Dictionary<string, string>();

            readonly Dictionary<string, string> symlinkMap =
                symlinkMap ?? new Dictionary<string, string>();

            /// <summary>
            /// A pass-through resolver: <see cref="GetFullPath"/> returns the path
            /// unchanged, and <see cref="ResolveLinkTarget"/> always throws
            /// <see cref="FileNotFoundException"/> so that
            /// <see cref="PathResolutionServiceExtensions.ResolvePath"/> falls back to
            /// returning the normalised path as-is.  Used by Group D tests so that
            /// fake paths (e.g. /home/octopus/tentacle) are matched against fake drive
            /// roots without the real <see cref="DefaultPathResolutionService"/>
            /// touching the actual filesystem.
            /// </summary>
            public static readonly FakePathResolutionService PassThrough =
                new(StringComparison.OrdinalIgnoreCase);

            /// <inheritdoc/>
            /// <remarks>
            /// Throws <see cref="getFullPathException"/> when one was supplied to the
            /// constructor, simulating the documented failure modes of
            /// <see cref="Path.GetFullPath(string)"/>.  Otherwise performs a map
            /// lookup, returning the mapped value or the original path unchanged.
            /// </remarks>
            public string GetFullPath(string path)
            {
                if (getFullPathException is not null)
                    throw getFullPathException;

                return fullPathMap.TryGetValue(path, out var normalised) ? normalised : path;
            }

            /// <inheritdoc/>
            /// <remarks>
            /// Returns a <see cref="FileSystemInfo"/> pointing at the mapped target
            /// when <paramref name="path"/> is present in the symlink map.  Throws
            /// <see cref="FileNotFoundException"/> otherwise, driving the ancestor-walk
            /// in <see cref="PathResolutionServiceExtensions.ResolvePath"/>.
            /// </remarks>
            public FileSystemInfo? ResolveLinkTarget(string path)
            {
                if (symlinkMap.TryGetValue(path, out var target))
                    return new FileInfo(target);

                throw new FileNotFoundException($"Simulated non-existent path: {path}", path);
            }

            /// <inheritdoc/>
            public StringComparison PathComparison => pathComparison;
        }

        // Builds a CachedDriveInfo whose root is rootPath with a known LockCapability.
        static CachedDriveInfo DriveAt(string rootPath)
            => new(
                   RootDirectory: new DirectoryInfo(rootPath),
                   Format: "apfs",
                   DriveType: DriveType.Fixed,
                   DetectedLockSupport: LockCapability.Supported
                  );

        // Constructs a MountedDrives with the given resolver injected, for use in Group E tests
        // where the resolver's symlink and path-normalisation behaviour is under test.
        static MountedDrives DrivesWithResolver(FakePathResolutionService resolver, params CachedDriveInfo[] driveInfos)
            => new(driveInfos, resolver);

        [Test]
        public void GetAssociatedDrive_ResolvesSymlink_BeforeMatching()
        {
            // Simulate macOS: /tmp is a symlink to /private/tmp.
            // DriveInfo returns /private/tmp as the mount root.
            // The caller passes /tmp/foo — without resolution this would not match.
            var privateRoot = OperatingSystem.IsWindows() ? @"C:\real\" : "/private/tmp";
            var symlinkInput = OperatingSystem.IsWindows() ? @"C:\link\foo" : "/tmp/foo";
            var resolvedInput = OperatingSystem.IsWindows() ? @"C:\real\foo" : "/private/tmp/foo";

            var resolver = new FakePathResolutionService(
                                                         StringComparison.OrdinalIgnoreCase,
                                                         symlinkMap: new Dictionary<string, string> { [symlinkInput] = resolvedInput }
                                                        );
            var drives = DrivesWithResolver(resolver, DriveAt(privateRoot));

            var result = drives.GetAssociatedDrive(symlinkInput);

            result.RootDirectory.FullName.Should().Be(privateRoot);
        }

        [Test]
        public void GetAssociatedDrive_CaseSensitive_RejectsWrongCase()
        {
            // On a case-sensitive filesystem (Linux), /Home/foo should NOT match /home.
            var root = OperatingSystem.IsWindows() ? @"C:\home\" : "/home";
            var wrongCasePath = OperatingSystem.IsWindows() ? @"C:\Home\foo" : "/Home/foo";

            var resolver = new FakePathResolutionService(StringComparison.Ordinal);
            var drives = DrivesWithResolver(resolver, DriveAt(root));

            var act = () => drives.GetAssociatedDrive(wrongCasePath);

            act.Should().Throw<DirectoryNotFoundException>();
        }

        [Test]
        public void GetAssociatedDrive_CaseInsensitive_MatchesWrongCase()
        {
            // On a case-insensitive filesystem (Windows/macOS), C:\foo should match C:\.
            var root = OperatingSystem.IsWindows() ? @"C:\" : "/home";
            var wrongCasePath = OperatingSystem.IsWindows() ? @"c:\foo" : "/Home/foo";

            var resolver = new FakePathResolutionService(StringComparison.OrdinalIgnoreCase);
            var drives = DrivesWithResolver(resolver, DriveAt(root));

            var result = drives.GetAssociatedDrive(wrongCasePath);

            result.RootDirectory.FullName.Should().Be(root);
        }

        [Test]
        public void GetAssociatedDrive_NormalisesPath_ViaResolver()
        {
            // The resolver is responsible for expanding ".." / relative paths.
            // Here we simulate a resolver that converts "../work/foo" to an absolute path.
            var root = OperatingSystem.IsWindows() ? @"C:\work\" : "/work";
            var rawInput = OperatingSystem.IsWindows() ? @"C:\other\..\work\foo" : "/other/../work/foo";
            var normalisedInput = OperatingSystem.IsWindows() ? @"C:\work\foo" : "/work/foo";

            var resolver = new FakePathResolutionService(
                                                         StringComparison.OrdinalIgnoreCase,
                                                         fullPathMap: new Dictionary<string, string> { [rawInput] = normalisedInput }
                                                        );
            var drives = DrivesWithResolver(resolver, DriveAt(root));

            var result = drives.GetAssociatedDrive(rawInput);

            result.RootDirectory.FullName.Should().Be(root);
        }

        [Test]
        public void GetAssociatedDrive_SelectsLongestMatchingMount()
        {
            // Both "/" and "/home" are mounts.  "/home/octopus/foo" should match "/home",
            // not "/", because longest prefix wins.
            if (OperatingSystem.IsWindows())
            {
                Assert.Ignore("POSIX-only test: Windows uses drive letters, not nested mounts.");
                return;
            }

            var inputPath = "/home/octopus/foo";
            var resolver = new FakePathResolutionService(StringComparison.Ordinal);
            var drives = DrivesWithResolver(resolver, DriveAt("/"), DriveAt("/home"));

            var result = drives.GetAssociatedDrive(inputPath);

            result.RootDirectory.FullName.Should().Be("/home",
                                                      because: "longest matching mount point should win");
        }

        [Test]
        public void GetAssociatedDrive_ThrowsDirectoryNotFoundException_WhenNoMatchAfterResolution()
        {
            // Even after symlink resolution, no drive covers the path.
            var root = OperatingSystem.IsWindows() ? @"C:\" : "/home";
            var unrelatedPath = OperatingSystem.IsWindows() ? @"D:\foo" : "/mnt/data/foo";

            var resolver = new FakePathResolutionService(StringComparison.OrdinalIgnoreCase);
            var drives = DrivesWithResolver(resolver, DriveAt(root));

            var act = () => drives.GetAssociatedDrive(unrelatedPath);

            act.Should().Throw<DirectoryNotFoundException>()
               .WithMessage($"*{unrelatedPath}*");
        }

        [Test]
        public void GetAssociatedDrive_ResolvesSymlinkInAncestor_WhenChildDoesNotExist()
        {
            // Simulates the critical macOS scenario: the lock directory path does not yet
            // exist, but its ancestor (/tmp) is a symlink to /private/tmp.
            // The extension method must walk up from the non-existent child path,
            // resolve the symlink on the ancestor, and re-attach the tail so that the
            // path matches the /private/tmp drive root.
            //
            // symlinkMap contains only the ancestor symlink entry (/tmp → /private/tmp).
            // The full input path and intermediate paths are absent from the map, which
            // causes ResolveLinkTarget to throw FileNotFoundException for them — driving
            // the ancestor-walk in PathResolutionServiceExtensions.ResolvePath.
            var privateRoot = OperatingSystem.IsWindows() ? @"C:\real\" : "/private/tmp";
            var symlinkAncestor = OperatingSystem.IsWindows() ? @"C:\link" : "/tmp";
            var symlinkTarget = OperatingSystem.IsWindows() ? @"C:\real" : "/private/tmp";
            var inputPath = OperatingSystem.IsWindows()
                ? @"C:\link\subdir\lockfile"
                : "/tmp/subdir/lockfile";

            var resolver = new FakePathResolutionService(
                                                         StringComparison.OrdinalIgnoreCase,
                                                         symlinkMap: new Dictionary<string, string> { [symlinkAncestor] = symlinkTarget }
                                                        );
            var drives = DrivesWithResolver(resolver, DriveAt(privateRoot));

            var result = drives.GetAssociatedDrive(inputPath);

            result.RootDirectory.FullName.Should().Be(privateRoot,
                                                      because: "symlink in ancestor should be resolved even when the full path does not yet exist");
        }

        // -------------------------------------------------------------------------
        // Group F: DefaultPathResolutionService — real filesystem integration tests
        //
        // These tests use the actual DefaultPathResolutionService against the real
        // filesystem.  They are necessarily platform-specific and touch real paths,
        // but require no filesystem writes.
        // -------------------------------------------------------------------------

        [Test]
        [Platform("Unix,Linux,MacOsX")]
        public void DefaultPathResolutionService_ResolvesExistingSymlink()
        {
            // /tmp is a symlink to /private/tmp on macOS; on other Unix systems it may
            // not be a symlink, in which case ResolvePath should return the path unchanged.
            var result = DefaultPathResolutionService.Instance.ResolvePath("/tmp");

            // The result must be an absolute path and must not contain /tmp as a prefix
            // if /tmp is a symlink (i.e. it should point at the real location).
            result.Should().StartWith("/",
                                      because: "result must always be an absolute path");

            // If /tmp really is a symlink, the result should differ from /tmp.
            var tmpInfo = new FileInfo("/tmp");
            if (tmpInfo.LinkTarget is not null)
            {
                result.Should().NotBe("/tmp",
                                      because: "/tmp is a symlink and should resolve to its real target");
            }
        }

        [Test]
        [Platform("Unix,Linux,MacOsX")]
        public void DefaultPathResolutionService_ResolvesSymlinkInAncestor_WhenChildDoesNotExist()
        {
            // The child path does not exist, but /tmp (if a symlink) should still be
            // resolved so that the returned path starts with the real mount root.
            const string nonExistentUnderTmp = "/tmp/calamari-test-nonexistent-path-xyz";

            var result = DefaultPathResolutionService.Instance.ResolvePath(nonExistentUnderTmp);

            result.Should().StartWith("/",
                                      because: "result must always be an absolute path");
            result.Should().EndWith("/calamari-test-nonexistent-path-xyz",
                                    because: "the non-existent tail segment must be preserved");

            var tmpInfo = new FileInfo("/tmp");
            if (tmpInfo.LinkTarget is not null)
            {
                // The prefix should have been resolved away from the symlink
                result.Should().NotStartWith("/tmp/",
                                             because: "/tmp is a symlink; the resolved path should start with the real target");
            }
        }

        [Test]
        public void DefaultPathResolutionService_ReturnsNormalisedPath_WhenPathDoesNotExistAtAll()
        {
            // A path with no existing ancestor (other than the filesystem root) should
            // still return a normalised, absolute path without throwing.
            var nonExistent = OperatingSystem.IsWindows()
                ? @"C:\calamari-nonexistent-root-xyz\foo\bar"
                : "/calamari-nonexistent-root-xyz/foo/bar";

            var act = () => DefaultPathResolutionService.Instance.ResolvePath(nonExistent);

            act.Should().NotThrow();
            var result = act();
            result.Should().Contain("calamari-nonexistent-root-xyz");
        }

        // -------------------------------------------------------------------------
        // Group G: PathResolutionServiceExtensions.ResolvePath — unit tests
        //
        // These tests exercise the ancestor-walk logic in the extension method
        // directly, using FakePathResolutionService so no real filesystem access
        // is needed.  They cover:
        //   1. Non-existent path — fallback returns GetFullPath result.
        //   2. Existing path that is not a symlink — returned unchanged.
        //   3. Existing symlink — resolved to target.
        //   4. Non-existent path whose existing ancestor is a symlink — ancestor
        //      is resolved and tail is re-attached.
        //   5. Path normalisation — GetFullPath expansion is applied first.
        // -------------------------------------------------------------------------

        [Test]
        public void ResolvePath_ReturnsFullPath_WhenPathDoesNotExist()
        {
            // Arrange: no symlink entries — every ResolveLinkTarget call throws.
            // The root itself ("/nonexistent") also throws, so we walk all the way
            // up to a path whose GetDirectoryName is null or itself, then fall back
            // to returning the GetFullPath result.
            var nonExistent = OperatingSystem.IsWindows()
                ? @"C:\nonexistent\foo\bar"
                : "/nonexistent/foo/bar";
            var resolver = new FakePathResolutionService(StringComparison.Ordinal);

            var result = resolver.ResolvePath(nonExistent);

            result.Should().Be(nonExistent,
                               because: "GetFullPath returns the path unchanged when it is already absolute; " +
                                        "the fallback must return that value");
        }

        [Test]
        public void ResolvePath_ReturnsPath_WhenExistingPathIsNotASymlink()
        {
            // Arrange: the path is "present but not a symlink" — ResolveLinkTarget
            // returns null.  ResolvePath should return the path as-is (no tail to
            // re-attach).
            var existing = OperatingSystem.IsWindows() ? @"C:\home\foo" : "/home/foo";

            // symlinkMap entry with null-equivalent: map the path to itself so that
            // ResolveLinkTarget returns a FileInfo with the same FullName.
            var resolver = new FakePathResolutionService(
                                                         StringComparison.Ordinal,
                                                         symlinkMap: new Dictionary<string, string> { [existing] = existing }
                                                        );

            var result = resolver.ResolvePath(existing);

            result.Should().Be(existing,
                               because: "a non-symlink existing path should be returned unchanged");
        }

        [Test]
        public void ResolvePath_FollowsSymlink_WhenPathIsASymlink()
        {
            // Arrange: the full path is a symlink pointing at a different location.
            var symlink = OperatingSystem.IsWindows() ? @"C:\link\foo" : "/link/foo";
            var target = OperatingSystem.IsWindows() ? @"C:\real\foo" : "/real/foo";

            var resolver = new FakePathResolutionService(
                                                         StringComparison.Ordinal,
                                                         symlinkMap: new Dictionary<string, string> { [symlink] = target }
                                                        );

            var result = resolver.ResolvePath(symlink);

            result.Should().Be(target,
                               because: "a symlink should be resolved to its final target");
        }

        [Test]
        public void ResolvePath_ResolvesAncestorSymlink_AndReattachesTail()
        {
            // Arrange: the full input path does not exist, but its parent is a symlink.
            // Only the parent entry is in the symlinkMap; the child path is absent
            // (causing ResolveLinkTarget to throw for it), so the ancestor-walk kicks in.
            var symlinkParent = OperatingSystem.IsWindows() ? @"C:\link" : "/link";
            var realParent = OperatingSystem.IsWindows() ? @"C:\real" : "/real";
            var inputPath = OperatingSystem.IsWindows()
                ? @"C:\link\child\file"
                : "/link/child/file";
            var expectedResult = OperatingSystem.IsWindows()
                ? @"C:\real\child\file"
                : "/real/child/file";

            var resolver = new FakePathResolutionService(
                                                         StringComparison.Ordinal,
                                                         symlinkMap: new Dictionary<string, string> { [symlinkParent] = realParent }
                                                        );

            var result = resolver.ResolvePath(inputPath);

            result.Should().Be(expectedResult,
                               because: "the ancestor symlink should be resolved and the non-existent " +
                                        "tail segments re-attached in order");
        }

        [Test]
        public void ResolvePath_AppliesGetFullPath_BeforeWalking()
        {
            // Arrange: the raw input contains ".." components.  GetFullPath must be
            // applied first so the walk operates on a normalised path.
            var rawInput = OperatingSystem.IsWindows()
                ? @"C:\other\..\work\foo"
                : "/other/../work/foo";
            var normalised = OperatingSystem.IsWindows() ? @"C:\work\foo" : "/work/foo";

            // fullPathMap handles the normalisation; the normalised path maps to itself
            // via symlinkMap (exists, not a symlink).
            var resolver = new FakePathResolutionService(
                                                         StringComparison.Ordinal,
                                                         fullPathMap: new Dictionary<string, string> { [rawInput] = normalised },
                                                         symlinkMap: new Dictionary<string, string> { [normalised] = normalised }
                                                        );

            var result = resolver.ResolvePath(rawInput);

            result.Should().Be(normalised,
                               because: "GetFullPath normalisation must be applied before symlink resolution");
        }

        [Test]
        public void ResolvePath_ReattachesMultipleTailSegments_WhenDeepAncestorIsSymlink()
        {
            // Arrange: input has two non-existent tail segments; only a grandparent
            // is in the symlinkMap.
            var symlinkGrandparent = OperatingSystem.IsWindows() ? @"C:\link" : "/link";
            var realGrandparent = OperatingSystem.IsWindows() ? @"C:\real" : "/real";
            var inputPath = OperatingSystem.IsWindows()
                ? @"C:\link\a\b"
                : "/link/a/b";
            var expectedResult = OperatingSystem.IsWindows()
                ? @"C:\real\a\b"
                : "/real/a/b";

            var resolver = new FakePathResolutionService(
                                                         StringComparison.Ordinal,
                                                         symlinkMap: new Dictionary<string, string> { [symlinkGrandparent] = realGrandparent }
                                                        );

            var result = resolver.ResolvePath(inputPath);

            result.Should().Be(expectedResult,
                               because: "all non-existent tail segments must be re-attached in the correct order");
        }

        // -------------------------------------------------------------------------
        // These tests cover the GetFullPath exception-handling path: all five
        // documented exception types must cause ResolvePath to return the original
        // raw path unchanged so that a single malformed path does not abort the
        // drive-selection loop.
        // -------------------------------------------------------------------------

        static IEnumerable<TestCaseData> GetFullPathExceptionCases()
        {
            yield return new TestCaseData(new ArgumentException("invalid path"))
                .SetName("ResolvePath_ReturnsRawPath_WhenGetFullPathThrows_ArgumentException");
            yield return new TestCaseData(new ArgumentNullException("path"))
                .SetName("ResolvePath_ReturnsRawPath_WhenGetFullPathThrows_ArgumentNullException");
            yield return new TestCaseData(new SecurityException("access denied"))
                .SetName("ResolvePath_ReturnsRawPath_WhenGetFullPathThrows_SecurityException");
            yield return new TestCaseData(new NotSupportedException("not supported"))
                .SetName("ResolvePath_ReturnsRawPath_WhenGetFullPathThrows_NotSupportedException");
            yield return new TestCaseData(new PathTooLongException("path too long"))
                .SetName("ResolvePath_ReturnsRawPath_WhenGetFullPathThrows_PathTooLongException");
        }

        [TestCaseSource(nameof(GetFullPathExceptionCases))]
        public void ResolvePath_ReturnsRawPath_WhenGetFullPathThrows(Exception exception)
        {
            // Arrange: GetFullPath throws the given exception.
            // ResolvePath must return the original raw path unchanged rather than
            // propagating the exception, so that one bad path cannot prevent other
            // drive candidates from being evaluated.
            var rawPath = OperatingSystem.IsWindows() ? @"C:\bad\path" : "/bad/path";
            var resolver = new FakePathResolutionService(
                                                         StringComparison.Ordinal,
                                                         getFullPathException: exception
                                                        );

            var result = resolver.ResolvePath(rawPath);

            result.Should().Be(rawPath,
                               because: $"a {exception.GetType().Name} from GetFullPath must not propagate; " +
                                        "the raw path must be returned as a safe fallback");
        }
    }
}
