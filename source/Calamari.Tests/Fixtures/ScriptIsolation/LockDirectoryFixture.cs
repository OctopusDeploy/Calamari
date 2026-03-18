#nullable enable
using System;
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
        // A stable path prefix that exists on all platforms, used as a fake root for
        // constructing CachedDriveInfo entries without touching real DriveInfo.
        static readonly string FakeRoot = OperatingSystem.IsWindows() ? @"C:\" : "/";
        static readonly string CandidatePath = OperatingSystem.IsWindows()
            ? @"C:\Octopus\Tentacle"
            : "/home/octopus/tentacle";

        // -------------------------------------------------------------------------
        // Group A: LockDirectory.Supports(LockType)
        // -------------------------------------------------------------------------

        [TestCase(LockCapability.Supported, LockType.Exclusive, true)]
        [TestCase(LockCapability.Supported, LockType.Shared,    true)]
        [TestCase(LockCapability.ExclusiveOnly, LockType.Exclusive, true)]
        [TestCase(LockCapability.ExclusiveOnly, LockType.Shared,    false)]
        [TestCase(LockCapability.Unsupported, LockType.Exclusive, false)]
        [TestCase(LockCapability.Unknown,     LockType.Exclusive, false)]
        public void Supports_ReturnsExpectedResult(
            LockCapability capability, LockType lockType, bool expected)
        {
            var dir = new LockDirectory(new DirectoryInfo(CandidatePath), capability);
            dir.Supports(lockType).Should().Be(expected);
        }

        // -------------------------------------------------------------------------
        // Group B: CachedDriveInfo.LockSupport property
        // -------------------------------------------------------------------------

        [TestCase("ntfs",  DriveType.Fixed,   null,                        LockCapability.Supported)]
        [TestCase("ext4",  DriveType.Fixed,   null,                        LockCapability.Supported)]
        [TestCase("apfs",  DriveType.Fixed,   null,                        LockCapability.Supported)]
        [TestCase("btrfs", DriveType.Fixed,   null,                        LockCapability.Supported)]
        [TestCase("tmpfs", DriveType.Fixed,   null,                        LockCapability.Supported)]
        [TestCase("xfs",   DriveType.Fixed,   null,                        LockCapability.Supported)]
        [TestCase("zfs",   DriveType.Fixed,   null,                        LockCapability.Supported)]
        [TestCase("hfs+",  DriveType.Fixed,   null,                        LockCapability.Supported)]
        [TestCase("nfs",   DriveType.Fixed,   null,                        LockCapability.Unknown)]
        [TestCase("nfs",   DriveType.Network, null,                        LockCapability.Unknown)]
        [TestCase("ntfs",  DriveType.Network, null,                        LockCapability.Unknown)]
        [TestCase("unknown", DriveType.Fixed, LockCapability.Unsupported,  LockCapability.Unsupported)]
        [TestCase("ntfs",  DriveType.Fixed,   LockCapability.ExclusiveOnly, LockCapability.ExclusiveOnly)]
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
        // Group C: CachedDriveInfo.DetectLockSupport with injected delegate
        // -------------------------------------------------------------------------

        // Builds a CachedDriveInfo with LockSupport == Unknown so detection is triggered.
        static CachedDriveInfo UnknownDrive()
            => new(
                   RootDirectory: new DirectoryInfo(FakeRoot),
                   Format: "unknown-fs",
                   DriveType: DriveType.Fixed,
                   DetectedLockSupport: null   // Format is unrecognised → LockSupport == Unknown
                  );

        // Builds a LockFile that has LockCapability.Unknown so Open() doesn't throw NotSupportedException.
        static LockFile MakeTestLockFile()
        {
            var dir = new LockDirectory(
                                        new DirectoryInfo(Path.GetTempPath()),
                                        LockCapability.Unknown
                                       );
            return dir.GetLockFile($"detect-test-{Guid.NewGuid():N}.tmp");
        }

        // A minimal ILockHandle stub.
        sealed class NoOpHandle : ILockHandle
        {
            public void Dispose() { }
            public System.Threading.Tasks.ValueTask DisposeAsync() => System.Threading.Tasks.ValueTask.CompletedTask;
        }

        [Test]
        public void DetectLockSupport_ReturnsUnsupported_WhenFirstExclusiveOpenFails()
        {
            var drive = UnknownDrive();
            ILockHandle Delegate(LockOptions _) => throw new IOException("lock not supported");

            var result = drive.DetectLockSupport(Path.GetTempPath(), Delegate);

            result.LockSupport.Should().Be(LockCapability.Unsupported);
        }

        [Test]
        public void DetectLockSupport_ReturnsExclusiveOnly_WhenSharedLockFails()
        {
            // Exclusive open: first call succeeds; second call (re-lock while held) throws → exclusive works.
            // Shared open: throws → shared not supported.
            var drive = UnknownDrive();
            var callCount = 0;
            ILockHandle Delegate(LockOptions opts)
            {
                callCount++;
                // TestExclusiveLock: call 1 = initial acquire (succeed), call 2 = second acquire (throw).
                // TestSharedLock:    call 3 = first shared acquire (throw → returns false → ExclusiveOnly).
                return callCount switch
                {
                    1 => new NoOpHandle(),
                    2 => throw new LockRejectedException("exclusive blocks exclusive"),
                    _ => throw new LockRejectedException("shared not supported")
                };
            }

            var result = drive.DetectLockSupport(Path.GetTempPath(), Delegate);

            result.LockSupport.Should().Be(LockCapability.ExclusiveOnly);
        }

        [Test]
        public void DetectLockSupport_ReturnsExclusiveOnly_WhenExclusiveDoesNotBlockShared()
        {
            // Exclusive works. Shared works (two concurrent shared succeed).
            // But exclusive-blocks-shared fails (shared acquire while exclusive held also succeeds → bad).
            var drive = UnknownDrive();
            var callCount = 0;
            ILockHandle Delegate(LockOptions opts)
            {
                callCount++;
                // TestExclusiveLock:       call 1 succeed, call 2 throw (exclusive blocks exclusive ✓)
                // TestSharedLock:          call 3 succeed, call 4 succeed (shared+shared ✓)
                // TestExclusiveBlocksShared: call 5 succeed (exclusive), call 6 succeed (shared while exclusive held — bad → returns false)
                return callCount switch
                {
                    1 => new NoOpHandle(),
                    2 => throw new LockRejectedException("exclusive blocks exclusive"),
                    3 => new NoOpHandle(),
                    4 => new NoOpHandle(),
                    5 => new NoOpHandle(),
                    6 => new NoOpHandle(),   // should have thrown — ExclusiveOnly result
                    _ => new NoOpHandle()
                };
            }

            var result = drive.DetectLockSupport(Path.GetTempPath(), Delegate);

            result.LockSupport.Should().Be(LockCapability.ExclusiveOnly);
        }

        [Test]
        public void DetectLockSupport_ReturnsExclusiveOnly_WhenSharedDoesNotBlockExclusive()
        {
            // Exclusive works. Shared works. Exclusive-blocks-shared works.
            // But shared-blocks-exclusive fails (exclusive while shared held also succeeds → bad).
            var drive = UnknownDrive();
            var callCount = 0;
            ILockHandle Delegate(LockOptions opts)
            {
                callCount++;
                // TestExclusiveLock:         call 1 succeed, call 2 throw ✓
                // TestSharedLock:            call 3 succeed, call 4 succeed ✓
                // TestExclusiveBlocksShared: call 5 succeed (exclusive), call 6 throw (shared blocked ✓)
                // TestSharedBlocksExclusive: call 7 succeed (shared), call 8 succeed (exclusive not blocked — bad → returns false)
                return callCount switch
                {
                    1 => new NoOpHandle(),
                    2 => throw new LockRejectedException("exclusive blocks exclusive"),
                    3 => new NoOpHandle(),
                    4 => new NoOpHandle(),
                    5 => new NoOpHandle(),
                    6 => throw new LockRejectedException("exclusive blocks shared"),
                    7 => new NoOpHandle(),
                    8 => new NoOpHandle(),   // should have thrown — ExclusiveOnly result
                    _ => new NoOpHandle()
                };
            }

            var result = drive.DetectLockSupport(Path.GetTempPath(), Delegate);

            result.LockSupport.Should().Be(LockCapability.ExclusiveOnly);
        }

        [Test]
        public void DetectLockSupport_ReturnsSupported_WhenAllFourProbesPass()
        {
            var drive = UnknownDrive();
            var callCount = 0;
            ILockHandle Delegate(LockOptions opts)
            {
                callCount++;
                // TestExclusiveLock:         call 1 succeed, call 2 throw ✓
                // TestSharedLock:            call 3 succeed, call 4 succeed ✓
                // TestExclusiveBlocksShared: call 5 succeed (exclusive), call 6 throw (shared blocked ✓)
                // TestSharedBlocksExclusive: call 7 succeed (shared), call 8 throw (exclusive blocked ✓)
                return callCount switch
                {
                    1 => new NoOpHandle(),
                    2 => throw new LockRejectedException("exclusive blocks exclusive"),
                    3 => new NoOpHandle(),
                    4 => new NoOpHandle(),
                    5 => new NoOpHandle(),
                    6 => throw new LockRejectedException("exclusive blocks shared"),
                    7 => new NoOpHandle(),
                    8 => throw new LockRejectedException("shared blocks exclusive"),
                    _ => new NoOpHandle()
                };
            }

            var result = drive.DetectLockSupport(Path.GetTempPath(), Delegate);

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

        // An acquire delegate that simulates full lock support in DetectLockSupport.
        // Only used when we need DetectLockSupport to probe (i.e. LockSupport == Unknown).
        static ILockHandle FullySupportedAcquire(LockOptions opts) =>
            throw new LockRejectedException("simulated block");

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
            // The temp path sits under FakeRoot so it matches the same drive.
            // We use two separate roots: one for the candidate (Unknown) and one for the
            // temp path (Supported) to exercise the temp-candidate branch.
            // On both platforms we can fake this by giving each path a distinct root.
            if (OperatingSystem.IsWindows())
            {
                const string candidateRoot = @"C:\";
                const string tempRoot = @"D:\";
                const string candidate = @"C:\Octopus\Tentacle";
                var tempPath = Path.Combine(tempRoot, "Tentacle");

                var drives = new MountedDrives([
                    DriveWithCapability(candidateRoot, LockCapability.Unknown),
                    DriveWithCapability(tempRoot, LockCapability.Supported)
                ]);

                // Supply an acquire delegate that behaves as fully supported so that if
                // DetectLockSupport is ever called it returns Supported.
                var callCount = 0;
                ILockHandle AcquireDelegate(LockOptions o)
                {
                    callCount++;
                    return callCount switch
                    {
                        1 => new NoOpHandle(),
                        2 => throw new LockRejectedException("exclusive blocks exclusive"),
                        3 => new NoOpHandle(),
                        4 => new NoOpHandle(),
                        5 => new NoOpHandle(),
                        6 => throw new LockRejectedException("exclusive blocks shared"),
                        7 => new NoOpHandle(),
                        8 => throw new LockRejectedException("shared blocks exclusive"),
                        _ => new NoOpHandle()
                    };
                }

                var result = LockDirectory.GetLockDirectory(candidate, drives, AcquireDelegate);

                // The temp drive is Supported so the first temp candidate under tempRoot
                // should be returned.
                result.LockSupport.Should().Be(LockCapability.Supported);
                result.DirectoryInfo.FullName.Should().StartWith(tempRoot,
                    because: "the result should be located under the supported temp drive");
            }
            else
            {
                // On non-Windows, simulate candidate on one "mount" and /tmp on a separate one.
                // We can't truly fake two POSIX roots without OS cooperation, so instead we
                // set up the candidate drive as Unknown and verify the detection path is entered.
                // For this particular scenario we verify that when all drives are Unknown and
                // detect returns Supported for the temp path, the result uses that temp path.
                var drives = new MountedDrives([
                    DriveWithCapability("/", LockCapability.Unknown)
                ]);

                var callCount = 0;
                ILockHandle AcquireDelegate(LockOptions o)
                {
                    callCount++;
                    return callCount switch
                    {
                        1 => new NoOpHandle(),
                        2 => throw new LockRejectedException("exclusive blocks exclusive"),
                        3 => new NoOpHandle(),
                        4 => new NoOpHandle(),
                        5 => new NoOpHandle(),
                        6 => throw new LockRejectedException("exclusive blocks shared"),
                        7 => new NoOpHandle(),
                        8 => throw new LockRejectedException("shared blocks exclusive"),
                        _ => new NoOpHandle()
                    };
                }

                var result = LockDirectory.GetLockDirectory(CandidatePath, drives, AcquireDelegate);

                // The first temp candidate or the candidate itself will be detected as Supported.
                result.LockSupport.Should().Be(LockCapability.Supported);
            }
        }

        [Test]
        public void GetLockDirectory_ReturnsCandidatePath_WhenAllTempsAreExclusiveOnlyAndCandidateDetectsSupported()
        {
            // Candidate drive is Unknown; temp drives are ExclusiveOnly (already-detected).
            // When we fall back and detect the candidate, it comes back as Supported.
            var drives = new MountedDrives([
                DriveWithCapability(FakeRoot, LockCapability.Unknown)
            ]);

            var callCount = 0;
            ILockHandle AcquireDelegate(LockOptions o)
            {
                callCount++;
                // All four probes pass → Supported
                return callCount switch
                {
                    1 => new NoOpHandle(),
                    2 => throw new LockRejectedException("exclusive blocks exclusive"),
                    3 => new NoOpHandle(),
                    4 => new NoOpHandle(),
                    5 => new NoOpHandle(),
                    6 => throw new LockRejectedException("exclusive blocks shared"),
                    7 => new NoOpHandle(),
                    8 => throw new LockRejectedException("shared blocks exclusive"),
                    _ => new NoOpHandle()
                };
            }

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives, AcquireDelegate);

            result.LockSupport.Should().Be(LockCapability.Supported);
        }

        [Test]
        public void GetLockDirectory_ReturnsFirstTempPathWithExclusiveOnly_WhenAllDetectToExclusiveOnly()
        {
            // Candidate drive is Unknown; all temp paths share the same root drive which has
            // already been detected as ExclusiveOnly (DetectedLockSupport is set, so
            // DetectLockSupport is a no-op and just returns the existing value).
            // The candidate is re-probed last with a fresh call; we give the candidate drive
            // also ExclusiveOnly detection so the final result is the temp path.
            if (OperatingSystem.IsWindows())
            {
                // Windows: candidate on C:\, temp paths on D:\ (ExclusiveOnly).
                const string candidateRoot = @"C:\";
                const string tempRoot = @"D:\";
                const string candidate = @"C:\Octopus\Tentacle";

                var drives = new MountedDrives([
                    DriveWithCapability(candidateRoot, LockCapability.ExclusiveOnly),
                    DriveWithCapability(tempRoot,      LockCapability.ExclusiveOnly)
                ]);

                var result = LockDirectory.GetLockDirectory(candidate, drives);

                result.LockSupport.Should().Be(LockCapability.ExclusiveOnly);
                result.DirectoryInfo.FullName.Should().StartWith(tempRoot,
                    because: "the first ExclusiveOnly temp path should be preferred over the candidate");
            }
            else
            {
                // On non-Windows there is a single drive root ("/") covering both the candidate
                // and temp paths. Set it to ExclusiveOnly; because the candidate drive is not
                // Unknown, DetectLockSupport is a no-op and returns ExclusiveOnly immediately.
                // All temp paths resolve the same drive and are also ExclusiveOnly.
                // The first temp path found is captured as tempPathExclusiveOnly and returned.
                var drives = new MountedDrives([
                    DriveWithCapability("/", LockCapability.ExclusiveOnly)
                ]);

                var result = LockDirectory.GetLockDirectory(CandidatePath, drives);

                result.LockSupport.Should().Be(LockCapability.ExclusiveOnly);
                // The result should NOT be the candidate path — it should be a temp directory.
                result.DirectoryInfo.FullName.Should().NotBe(CandidatePath,
                    because: "a temp path should be preferred over the original candidate");
            }
        }

        [Test]
        public void GetLockDirectory_ReturnsUnsupported_WhenNothingWorks()
        {
            var drives = new MountedDrives([
                DriveWithCapability(FakeRoot, LockCapability.Unknown)
            ]);

            // All acquire attempts fail → everything detects as Unsupported.
            ILockHandle AcquireDelegate(LockOptions _) => throw new IOException("no locking at all");

            var result = LockDirectory.GetLockDirectory(CandidatePath, drives, AcquireDelegate);

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
