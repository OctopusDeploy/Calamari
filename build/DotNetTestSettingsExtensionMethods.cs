using System;
using System.Collections.Generic;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;

namespace Calamari.Build
{
    public static class DotNetTestSettingsExtensionMethods
    {
        public static DotNetTestSettings EnableTeamCityTestLogger(this DotNetTestSettings settings, AbsolutePath outputDirectory)
        {
            settings = settings.AddLoggers("teamcity");

            // opt in to the teamcity test reporting via file approach as suggested in the support ticket
            // https://youtrack.jetbrains.com/issue/TW-80096/Inconsistent-test-counts-when-using-dotnet-test-and-NUnit-adapter#focus=Comments-27-8728443.0-0
            var testReportsDirectory = outputDirectory / "TestReports" / Guid.NewGuid().ToString();
            testReportsDirectory.CreateOrCleanDirectory();
            settings = settings.SetProcessEnvironmentVariable("TEAMCITY_TEST_REPORT_FILES_PATH", testReportsDirectory);
            Console.WriteLine($"##teamcity[importData type='streamToBuildLog' filePattern='{testReportsDirectory}/*.msg' wrapFileContentInBlock='false' quiet='false']");
            return settings;
        }
    }
}