#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        // Non-Windows uses POSIX-style mount points (/home, /tmp, /var) to simulate
        // a realistic multi-filesystem hierarchy covering the candidate path and all
        // temp candidates produced by GetTemporaryCandidates.
        // -------------------------------------------------------------------------

        // Root that contains the candidate path.
        static readonly string CandidateRoot = OperatingSystem.IsWindows() ? @"C:\" : "/home";

        // Candidate path used across Group D tests.
        static readonly string CandidatePath = OperatingSystem.IsWindows()
            ? @"C:\Octopus\Tentacle"
            : "/home/octopus/tentacle";

        // Roots that cover the temp candidates produced by GetTemporaryCandidates.
        // On Windows, all temp paths sit under a single separate drive letter.
        // On non-Windows: $TMPDIR lives under /var/…, and /tmp is its own mount.
        static readonly string[] TempRoots = OperatingSystem.IsWindows()
            ? [@"D:\"]
            : ["/var", "/tmp"];

        // A single stable fake root used by Group A/B/C tests that don't exercise
        // path routing (any root that is an ancestor of CandidatePath works).
        static readonly string FakeRoot = CandidateRoot;

        // -------------------------------------------------------------------------
        // FakeLockService — simulates filesystem lock semantics without relying on
        // call order. Tracks currently-held locks and enforces four compatibility
        // rules that together describe any filesystem's locking behaviour.
        //
        // Usage: construct with the desired rules, pass .Acquire as the delegate.
        // -------------------------------------------------------------------------

        /// <summary>
        /// Simulates the lock-acquisition behaviour of a filesystem.  Each call to
        /// <see cref="Acquire"/> checks compatibility rules against currently-held
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
        /// <see cref="FullySupported"/>, <see cref="ExclusiveOnly"/>,
        /// <see cref="ExclusiveOnlyBecauseSharedUnsupported"/>,
        /// <see cref="ExclusiveOnlyBecauseExclusiveDoesNotBlockShared"/>,
        /// <see cref="ExclusiveOnlyBecauseSharedDoesNotBlockExclusive"/>,
        /// <see cref="Unsupported"/>.
        /// </summary>
        sealed class FakeLockService
        {
            readonly bool _exclusiveBlocksExclusive;
            readonly bool _sharedAllowed;
            readonly bool _exclusiveBlocksShared;
            readonly bool _sharedBlocksExclusive;

            // Tracks counts of currently-held locks (released on handle Dispose).
            int _heldExclusive;
            int _heldShared;

            FakeLockService(
                bool exclusiveBlocksExclusive,
                bool sharedAllowed,
                bool exclusiveBlocksShared,
                bool sharedBlocksExclusive)
            {
                _exclusiveBlocksExclusive = exclusiveBlocksExclusive;
                _sharedAllowed            = sharedAllowed;
                _exclusiveBlocksShared    = exclusiveBlocksShared;
                _sharedBlocksExclusive    = sharedBlocksExclusive;
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

            // ---- Acquire delegate ----------------------------------------------

            public ILockHandle Acquire(LockOptions opts)
            {
                switch (opts.Type)
                {
                    case LockType.Exclusive:
                        if (_exclusiveBlocksExclusive && _heldExclusive > 0)
                            throw new LockRejectedException("exclusive lock is already held");
                        if (_sharedBlocksExclusive && _heldShared > 0)
                            throw new LockRejectedException("shared lock blocks exclusive acquisition");
                        if (!_exclusiveBlocksExclusive && _heldExclusive == 0)
                            // The very first exclusive open failing means the fs doesn't support it.
                            throw new IOException("exclusive locking not supported on this filesystem");
                        _heldExclusive++;
                        return new Handle(() => _heldExclusive--);

                    case LockType.Shared:
                        if (!_sharedAllowed)
                            throw new LockRejectedException("shared locking is not supported on this filesystem");
                        if (_exclusiveBlocksShared && _heldExclusive > 0)
                            throw new LockRejectedException("exclusive lock blocks shared acquisition");
                        _heldShared++;
                        return new Handle(() => _heldShared--);

                    default:
                        throw new ArgumentOutOfRangeException(nameof(opts));
                }
            }

            sealed class Handle(Action release) : ILockHandle
            {
                bool _disposed;

                public void Dispose()
                {
                    if (_disposed) return;
                    _disposed = true;
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

            var result = drive.DetectLockSupport(Path.GetTempPath(), fs.Acquire);

            result.LockSupport.Should().Be(LockCapability.Unsupported);
        }

        [Test]
        public void DetectLockSupport_ReturnsExclusiveOnly_WhenSharedLockingIsNotSupported()
        {
            var drive = UnknownDrive();
            var fs = FakeLockService.ExclusiveOnlyBecauseSharedUnsupported();

            var result = drive.DetectLockSupport(Path.GetTempPath(), fs.Acquire);

            result.LockSupport.Should().Be(LockCapability.ExclusiveOnly);
        }

        [Test]
        public void DetectLockSupport_ReturnsExclusiveOnly_WhenExclusiveLockDoesNotBlockSharedLock()
        {
            // A shared lock can be acquired even while an exclusive lock is held —
            // the filesystem does not enforce mutual exclusion between the two types.
            var drive = UnknownDrive();
            var fs = FakeLockService.ExclusiveOnlyBecauseExclusiveDoesNotBlockShared();

            var result = drive.DetectLockSupport(Path.GetTempPath(), fs.Acquire);

            result.LockSupport.Should().Be(LockCapability.ExclusiveOnly);
        }

        [Test]
        public void DetectLockSupport_ReturnsExclusiveOnly_WhenSharedLockDoesNotBlockExclusiveLock()
        {
            // An exclusive lock can be acquired even while a shared lock is held —
            // the filesystem does not enforce mutual exclusion between the two types.
            var drive = UnknownDrive();
            var fs = FakeLockService.ExclusiveOnlyBecauseSharedDoesNotBlockExclusive();

            var result = drive.DetectLockSupport(Path.GetTempPath(), fs.Acquire);

            result.LockSupport.Should().Be(LockCapability.ExclusiveOnly);
        }

        [Test]
        public void DetectLockSupport_ReturnsSupported_WhenFullMutualExclusionIsEnforced()
        {
            // The filesystem correctly blocks all conflicting lock combinations.
            var drive = UnknownDrive();
            var fs = FakeLockService.FullySupported();

            var result = drive.DetectLockSupport(Path.GetTempPath(), fs.Acquire);

            result.LockSupport.Should().Be(LockCapability.Supported);
        }

        // -------------------------------------------------------------------------
        // Group D: GetLockDirectory path-selection with injected MountedDrives
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

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives);

            result.DirectoryInfo.FullName.Should().Be(CandidatePath);
            result.LockSupport.Should().Be(LockCapability.Supported);
        }

        [Test]
        public void GetLockDirectory_ReturnsTempPath_WhenCandidateIsUnknownAndTempDriveIsSupported()
        {
            // Candidate root is Unknown; temp roots are pre-detected as Supported.
            // GetLockDirectory should return the first temp path it finds, not the candidate.
            var drives = new MountedDrives([
                DriveWithCapability(CandidateRoot, LockCapability.Unknown),
                ..TempRoots.Select(r => DriveWithCapability(r, LockCapability.Supported))
            ]);

            // Temp drives are already-detected as Supported so DetectLockSupport is a no-op;
            // no acquire delegate is needed.
            var result = LockDirectory.GetLockDirectory(CandidatePath, drives);

            result.LockSupport.Should().Be(LockCapability.Supported);
            result.DirectoryInfo.FullName.Should().NotStartWith(CandidateRoot,
                because: "a temp path on the supported drive should be preferred");
        }

        [Test]
        public void GetLockDirectory_ReturnsCandidatePath_WhenAllTempsAreExclusiveOnlyAndCandidateDetectsSupported()
        {
            // Temp roots are pre-detected as ExclusiveOnly; candidate root is Unknown.
            // Detection on the candidate drive returns Supported, so the candidate path
            // should be returned rather than any of the ExclusiveOnly temp paths.
            var drives = new MountedDrives([
                DriveWithCapability(CandidateRoot, LockCapability.Unknown),
                ..TempRoots.Select(r => DriveWithCapability(r, LockCapability.ExclusiveOnly))
            ]);
            var fs = FakeLockService.FullySupported();

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives, fs.Acquire, _ => { });

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
                ..TempRoots.Select(r => DriveWithCapability(r, LockCapability.ExclusiveOnly))
            ]);

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives);

            result.LockSupport.Should().Be(LockCapability.ExclusiveOnly);
            result.DirectoryInfo.FullName.Should().Be(CandidatePath,
                because: "the candidate path should be used when temp offers no better support");
        }

        [Test]
        public void GetLockDirectory_ReturnsTempPath_WhenTempIsExclusiveOnlyAndCandidateIsUnsupported()
        {
            // Candidate root is pre-detected as Unsupported; temp roots are pre-detected as
            // ExclusiveOnly. The temp path genuinely offers better support, so it should be
            // preferred over the candidate.
            var drives = new MountedDrives([
                DriveWithCapability(CandidateRoot, LockCapability.Unsupported),
                ..TempRoots.Select(r => DriveWithCapability(r, LockCapability.ExclusiveOnly))
            ]);

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives);

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
            var fs = FakeLockService.Unsupported();

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives, fs.Acquire, _ => { });

            result.LockSupport.Should().Be(LockCapability.Unsupported);
            result.DirectoryInfo.FullName.Should().Be(CandidatePath);
        }

        [Test]
        public void GetLockDirectory_ReturnsUnsupported_WhenMountedDrivesIsEmpty()
        {
            // No drives at all — GetAssociatedDrive throws DirectoryNotFoundException.
            // GetLockDirectory should catch it and return Unsupported rather than propagating.
            var drives = new MountedDrives([]);

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives);

            result.LockSupport.Should().Be(LockCapability.Unsupported);
            result.DirectoryInfo.FullName.Should().Be(CandidatePath);
        }
    }
}
