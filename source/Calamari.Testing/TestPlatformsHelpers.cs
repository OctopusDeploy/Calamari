using System;
using System.Collections.Generic;
using System.Linq;

namespace Calamari.Testing;

//Directly taken from the OctopusDeploy repo
public static class TestPlatformsHelpers
{
    static readonly TestPlatforms[] SpecificPlatforms = [TestPlatforms.Windows, TestPlatforms.Linux, TestPlatforms.MacOs];

    /// <summary>Enumerate the specific platforms set in the given <see cref="TestPlatforms" /> bit flags.</summary>
    public static IEnumerable<TestPlatforms> GetSpecificPlatforms(TestPlatforms flags) =>
        SpecificPlatforms.Where(platform => (platform & flags) != 0);

    public static TestPlatforms GetCurrentPlatform() =>
        OperatingSystem.IsLinux() ? TestPlatforms.Linux :
        OperatingSystem.IsWindows() ? TestPlatforms.Windows :
        OperatingSystem.IsMacOS() ? TestPlatforms.MacOs :
        throw new InvalidOperationException("Unknown test platform.");
}
