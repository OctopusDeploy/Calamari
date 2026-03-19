#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
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
            readonly string[] temporaryDirectories;

            // Tracks counts of currently-held locks (released on handle Dispose).
            int heldExclusive;
            int heldShared;

            FakeLockService(
                bool exclusiveBlocksExclusive,
                bool sharedAllowed,
                bool exclusiveBlocksShared,
                bool sharedBlocksExclusive,
                string[] temporaryDirectories)
            {
                this.exclusiveBlocksExclusive = exclusiveBlocksExclusive;
                this.sharedAllowed = sharedAllowed;
                this.exclusiveBlocksShared = exclusiveBlocksShared;
                this.sharedBlocksExclusive = sharedBlocksExclusive;
                this.temporaryDirectories = temporaryDirectories;
            }

            // ---- Preset factory methods ----------------------------------------

            /// <summary>
            /// A filesystem that supports both exclusive and shared locks with full
            /// mutual-exclusion semantics (NTFS, ext4, apfs, …).
            /// </summary>
            public static FakeLockService FullySupported(params string[] temporaryDirectories) =>
                new(exclusiveBlocksExclusive: true,
                    sharedAllowed: true,
                    exclusiveBlocksShared: true,
                    sharedBlocksExclusive: true,
                    temporaryDirectories: temporaryDirectories);

            /// <summary>
            /// A filesystem where shared locks are completely unsupported — every
            /// shared-lock attempt fails immediately.
            /// </summary>
            public static FakeLockService ExclusiveOnlyBecauseSharedUnsupported(params string[] temporaryDirectories) =>
                new(exclusiveBlocksExclusive: true,
                    sharedAllowed: false,
                    exclusiveBlocksShared: true,
                    sharedBlocksExclusive: true,
                    temporaryDirectories: temporaryDirectories);

            /// <summary>
            /// A filesystem where shared locks can be acquired, but an exclusive lock
            /// does <em>not</em> block a concurrent shared lock (broken mutual-exclusion).
            /// </summary>
            public static FakeLockService ExclusiveOnlyBecauseExclusiveDoesNotBlockShared(params string[] temporaryDirectories) =>
                new(exclusiveBlocksExclusive: true,
                    sharedAllowed: true,
                    exclusiveBlocksShared: false,
                    sharedBlocksExclusive: true,
                    temporaryDirectories: temporaryDirectories);

            /// <summary>
            /// A filesystem where shared locks can be acquired, but a shared lock does
            /// <em>not</em> block a concurrent exclusive lock (broken mutual-exclusion).
            /// </summary>
            public static FakeLockService ExclusiveOnlyBecauseSharedDoesNotBlockExclusive(params string[] temporaryDirectories) =>
                new(exclusiveBlocksExclusive: true,
                    sharedAllowed: true,
                    exclusiveBlocksShared: true,
                    sharedBlocksExclusive: false,
                    temporaryDirectories: temporaryDirectories);

            /// <summary>
            /// A filesystem where even exclusive locking is unsupported (e.g. some
            /// network file systems or read-only mounts).
            /// </summary>
            public static FakeLockService Unsupported(params string[] temporaryDirectories) =>
                new(exclusiveBlocksExclusive: false,
                    sharedAllowed: false,
                    exclusiveBlocksShared: false,
                    sharedBlocksExclusive: false,
                    temporaryDirectories: temporaryDirectories);

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

            public IEnumerable<string> GetFallbackTemporaryDirectories(string candidatePath)
            {
                return temporaryDirectories;
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
        //          and FakeLockService carrying temp directory candidates
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
            var fs = FakeLockService.FullySupported(TempPath);

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives,
                                                        lockService: fs,
                                                        pathResolver: FakePathResolutionService.PassThrough);

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
            var fs = FakeLockService.FullySupported(TempPath);

            // Temp drive is already-detected as Supported so DetectLockSupport is a no-op;
            // no lock service is needed.
            var result = LockDirectory.GetLockDirectory(CandidatePath, drives,
                                                        lockService: fs,
                                                        pathResolver: FakePathResolutionService.PassThrough);

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
            var fs = FakeLockService.FullySupported(TempPath);

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives, fs,
                                                        pathResolver: FakePathResolutionService.PassThrough);

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
            var fs = FakeLockService.FullySupported(TempPath);

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives,
                                                         lockService: fs,
                                                         pathResolver: FakePathResolutionService.PassThrough);

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
            var fs = FakeLockService.FullySupported(TempPath);

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives,
                                                         lockService: fs,
                                                         pathResolver: FakePathResolutionService.PassThrough);

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
            var fs = FakeLockService.Unsupported(TempPath);

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives, fs,
                                                        pathResolver: FakePathResolutionService.PassThrough);

            result.LockSupport.Should().Be(LockCapability.Unsupported);
            result.DirectoryInfo.FullName.Should().Be(CandidatePath);
        }

        [Test]
        public void GetLockDirectory_ReturnsUnsupported_WhenMountedDrivesIsEmpty()
        {
            // No drives at all — GetAssociatedDrive throws DirectoryNotFoundException.
            // GetLockDirectory should catch it and return Unsupported rather than propagating.
            var drives = new MountedDrives([]);
            var fs = FakeLockService.FullySupported(TempPath);

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives,
                                                         lockService: fs,
                                                         pathResolver: FakePathResolutionService.PassThrough);

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
            var fs = FakeLockService.Unsupported(TempPath);

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives, fs,
                                                        pathResolver: FakePathResolutionService.PassThrough);

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
            var fs = FakeLockService.FullySupported(TempPath, secondTempPath);

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives,
                                                         lockService: fs,
                                                         pathResolver: FakePathResolutionService.PassThrough);

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
        /// uses an explicit dictionary for symlink resolution and accepts a caller-
        /// supplied <see cref="StringComparison"/> for path matching.
        /// </summary>
        sealed class FakePathResolutionService(
            StringComparison pathComparison,
            Dictionary<string, string>? symlinkMap = null) : IPathResolutionService
        {
            readonly Dictionary<string, string> symlinkMap =
                symlinkMap ?? new Dictionary<string, string>();

            /// <summary>
            /// A pass-through resolver that performs no symlink resolution and uses
            /// OrdinalIgnoreCase comparison.  Used by Group D tests so that fake
            /// paths (e.g. /home/octopus/tentacle) are matched against fake drive
            /// roots without the real DefaultPathResolutionService touching the
            /// actual filesystem.
            /// </summary>
            public static readonly FakePathResolutionService PassThrough =
                new(StringComparison.OrdinalIgnoreCase);

            public string ResolvePath(string path)
                => symlinkMap.TryGetValue(path, out var resolved) ? resolved : path;

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

        [Test]
        public void GetAssociatedDrive_ResolvesSymlink_BeforeMatching()
        {
            // Simulate macOS: /tmp is a symlink to /private/tmp.
            // DriveInfo returns /private/tmp as the mount root.
            // The caller passes /tmp/foo — without resolution this would not match.
            var privateRoot = OperatingSystem.IsWindows() ? @"C:\real\" : "/private/tmp";
            var symlinkInput = OperatingSystem.IsWindows() ? @"C:\link\foo" : "/tmp/foo";
            var resolvedInput = OperatingSystem.IsWindows() ? @"C:\real\foo" : "/private/tmp/foo";

            var drives = new MountedDrives([DriveAt(privateRoot)]);
            var resolver = new FakePathResolutionService(
                StringComparison.OrdinalIgnoreCase,
                new Dictionary<string, string> { [symlinkInput] = resolvedInput }
            );

            var result = drives.GetAssociatedDrive(symlinkInput, resolver);

            result.RootDirectory.FullName.Should().Be(privateRoot);
        }

        [Test]
        public void GetAssociatedDrive_CaseSensitive_RejectsWrongCase()
        {
            // On a case-sensitive filesystem (Linux), /Home/foo should NOT match /home.
            var root = OperatingSystem.IsWindows() ? @"C:\home\" : "/home";
            var wrongCasePath = OperatingSystem.IsWindows() ? @"C:\Home\foo" : "/Home/foo";

            var drives = new MountedDrives([DriveAt(root)]);
            var resolver = new FakePathResolutionService(StringComparison.Ordinal);

            var act = () => drives.GetAssociatedDrive(wrongCasePath, resolver);

            act.Should().Throw<DirectoryNotFoundException>();
        }

        [Test]
        public void GetAssociatedDrive_CaseInsensitive_MatchesWrongCase()
        {
            // On a case-insensitive filesystem (Windows/macOS), C:\foo should match C:\.
            var root = OperatingSystem.IsWindows() ? @"C:\" : "/home";
            var wrongCasePath = OperatingSystem.IsWindows() ? @"c:\foo" : "/Home/foo";

            var drives = new MountedDrives([DriveAt(root)]);
            var resolver = new FakePathResolutionService(StringComparison.OrdinalIgnoreCase);

            var result = drives.GetAssociatedDrive(wrongCasePath, resolver);

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

            var drives = new MountedDrives([DriveAt(root)]);
            var resolver = new FakePathResolutionService(
                StringComparison.OrdinalIgnoreCase,
                new Dictionary<string, string> { [rawInput] = normalisedInput }
            );

            var result = drives.GetAssociatedDrive(rawInput, resolver);

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

            var rootMount = DriveAt("/");
            var homeMount = DriveAt("/home");
            var inputPath = "/home/octopus/foo";

            var drives = new MountedDrives([rootMount, homeMount]);
            var resolver = new FakePathResolutionService(StringComparison.Ordinal);

            var result = drives.GetAssociatedDrive(inputPath, resolver);

            result.RootDirectory.FullName.Should().Be("/home",
                                                      because: "longest matching mount point should win");
        }

        [Test]
        public void GetAssociatedDrive_ThrowsDirectoryNotFoundException_WhenNoMatchAfterResolution()
        {
            // Even after symlink resolution, no drive covers the path.
            var root = OperatingSystem.IsWindows() ? @"C:\" : "/home";
            var unrelatedPath = OperatingSystem.IsWindows() ? @"D:\foo" : "/mnt/data/foo";

            var drives = new MountedDrives([DriveAt(root)]);
            var resolver = new FakePathResolutionService(StringComparison.OrdinalIgnoreCase);

            var act = () => drives.GetAssociatedDrive(unrelatedPath, resolver);

            act.Should().Throw<DirectoryNotFoundException>()
               .WithMessage($"*{unrelatedPath}*");
        }

        [Test]
        public void GetAssociatedDrive_ResolvesSymlinkInAncestor_WhenChildDoesNotExist()
        {
            // Simulates the critical macOS scenario: the lock directory path does not yet
            // exist, but its ancestor (/tmp) is a symlink.  The resolver must walk up to
            // the existing ancestor, resolve it, and re-attach the non-existent tail so
            // that the path matches the /private/tmp drive root.
            var privateRoot = OperatingSystem.IsWindows() ? @"C:\real\" : "/private/tmp";
            var symlinkInput = OperatingSystem.IsWindows()
                ? @"C:\link\subdir\lockfile"
                : "/tmp/subdir/lockfile";
            var resolvedInput = OperatingSystem.IsWindows()
                ? @"C:\real\subdir\lockfile"
                : "/private/tmp/subdir/lockfile";

            var drives = new MountedDrives([DriveAt(privateRoot)]);
            var resolver = new FakePathResolutionService(
                StringComparison.OrdinalIgnoreCase,
                new Dictionary<string, string> { [symlinkInput] = resolvedInput }
            );

            var result = drives.GetAssociatedDrive(symlinkInput, resolver);

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
        [Platform("Unix")]
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
        [Platform("Unix")]
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
    }
}
