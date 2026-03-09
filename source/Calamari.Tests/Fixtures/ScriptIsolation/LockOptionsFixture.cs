#nullable enable
using System;
using System.IO;
using Calamari.Common.Features.Processes.ScriptIsolation;
using Calamari.Common.Plumbing.Commands;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ScriptIsolation
{
    [TestFixture]
    public class LockOptionsFixture
    {
        static string DefaultTentacleHome => OperatingSystem.IsWindows()
            ? @"C:\Octopus\Tentacle"
            : "/home/octopus/tentacle";

        static CommonOptions.ScriptIsolationOptions MakeOptions(
            string? level = "FullIsolation",
            string? mutexName = "TestMutex",
            string? timeout = "00:01:00",
            string? tentacleHome = null)
            => new()
            {
                Level = level,
                MutexName = mutexName,
                Timeout = timeout,
                TentacleHome = tentacleHome ?? DefaultTentacleHome
            };

        static CommonOptions.ScriptIsolationOptions MakeOptionsNoTentacleHome(
            string? level = "FullIsolation",
            string? mutexName = "TestMutex",
            string? timeout = "00:01:00")
            => new()
            {
                Level = level,
                MutexName = mutexName,
                Timeout = timeout,
                TentacleHome = null
            };

        [Test]
        public void FromScriptIsolationOptionsOrNull_ReturnsNull_WhenLevelIsNull()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(level: null));
            result.Should().BeNull();
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_ReturnsNull_WhenLevelIsWhiteSpace()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(level: "   "));
            result.Should().BeNull();
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_ReturnsNull_WhenMutexNameIsNull()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(mutexName: null));
            result.Should().BeNull();
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_ReturnsNull_WhenMutexNameIsWhiteSpace()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(mutexName: "   "));
            result.Should().BeNull();
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_ReturnsNull_WhenTentacleHomeIsMissing()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptionsNoTentacleHome());
            result.Should().BeNull();
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_ReturnsNull_WhenTentacleHomeIsWhiteSpace()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(tentacleHome: "   "));
            result.Should().BeNull();
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_MapsFullIsolationToExclusive()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(level: "FullIsolation"));
            result.Should().NotBeNull();
            result.Type.Should().Be(LockType.Exclusive);
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_MapsNoIsolationToShared()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(level: "NoIsolation"));
            result.Should().NotBeNull();
            result.Type.Should().Be(LockType.Shared);
        }

        [TestCase("fullisolation")]
        [TestCase("FULLISOLATION")]
        [TestCase("FullIsolation")]
        [TestCase("noisolation")]
        [TestCase("NOISOLATION")]
        [TestCase("NoIsolation")]
        public void FromScriptIsolationOptionsOrNull_IsCaseInsensitive(string isolationLevelValue)
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(level: isolationLevelValue));
            result.Should().NotBeNull();
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_ReturnsNull_ForUnknownIsolationLevel()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(level: "SomeUnknownLevel"));
            result.Should().BeNull();
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_ParsesTimeout()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(timeout: "00:05:00"));
            result.Should().NotBeNull();
            result.Timeout.Should().Be(TimeSpan.FromMinutes(5));
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_DefaultsToMaxValue_WhenTimeoutIsInvalid()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(timeout: "not-a-timespan"));
            result.Should().NotBeNull();
            result.Timeout.Should().Be(TimeSpan.MaxValue);
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_BuildsCorrectLockFilePath()
        {
            var scriptIsolationOptions = MakeOptions(mutexName: "MyMutex");
            var result = LockOptions.FromScriptIsolationOptionsOrNull(scriptIsolationOptions);
            result.Should().NotBeNull();
            var expectedLockFile = Path.Join(scriptIsolationOptions.TentacleHome, "ScriptIsolation.MyMutex.lock");

            result.LockFile.FullName.Should().Be(expectedLockFile);
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_Throws_WhenMutexNameContainsInvalidFileNameChar()
        {
            var invalidChar = Path.GetInvalidFileNameChars()[0];
            var badName = $"My{invalidChar}Mutex";

            Action act = () => LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(mutexName: badName));

            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_PreservesName()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(mutexName: "MyMutex"));
            result.Should().NotBeNull();
            result.Name.Should().Be("MyMutex");
        }
    }
}
