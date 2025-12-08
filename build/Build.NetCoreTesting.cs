using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.CI.TeamCity;

namespace Calamari.Build;

partial class Build
{
    [PublicAPI]
    Target PlatformAgnosticTesting =>
        target => target
            .Executes(() =>
                      {
                          const string testFilter = "TestCategory = PlatformAgnostic";

                          var runningInTeamCity = TeamCity.Instance is not null;
                          var outputDirectory = RootDirectory / "outputs";

                          var settingsFilePath = TryBuildExcludedTestsSettingsFile(testFilter);

                          DotNetTasks.DotNetTest(settings =>
                                                 {
                                                     settings = settings.SetProjectFile("Binaries/Calamari.Tests.dll");

                                                     settings = settingsFilePath is not null ? settings.SetSettingsFile(settingsFilePath) : settings.SetFilter(testFilter);

                                                     return settings
                                                            .SetProcessExitHandler(process => process.ExitCode switch
                                                                                              {
                                                                                                  0 => null, //successful
                                                                                                  1 => null, //some tests failed
                                                                                                  _ => throw new ProcessException(process)
                                                                                              })
                                                            .SetTestAdapterPath(outputDirectory)
                                                            .AddLoggers("console;verbosity=normal")
                                                            .When(runningInTeamCity, x => x.EnableTeamCityTestLogger(outputDirectory));
                                                 });
                      });


    [PublicAPI]
    Target LinuxSpecificTesting =>
        target => target
            .Executes(() =>
                      {
                          const string testFilter = "TestCategory != Windows & TestCategory != PlatformAgnostic & TestCategory != RunOnceOnWindowsAndLinux";

                          var runningInTeamCity = TeamCity.Instance is not null;
                          var outputDirectory = RootDirectory / "outputs";
                          
                          var settingsFilePath = TryBuildExcludedTestsSettingsFile(testFilter);

                          DotNetTasks.DotNetTest(settings =>
                                                 {
                                                     settings = settings.SetProjectFile("Binaries/Calamari.Tests.dll");

                                                     settings = settingsFilePath is not null ? settings.SetSettingsFile(settingsFilePath) : settings.SetFilter(testFilter);

                                                     return settings
                                                            .SetProcessExitHandler(process => process.ExitCode switch
                                                                                              {
                                                                                                  0 => null, //successful
                                                                                                  1 => null, //some tests failed
                                                                                                  _ => throw new ProcessException(process)
                                                                                              })
                                                            .SetTestAdapterPath(outputDirectory)
                                                            .AddLoggers("console;verbosity=normal")
                                                            .When(runningInTeamCity, x => x.EnableTeamCityTestLogger(outputDirectory));
                                                 });
                      });

    [PublicAPI]
    Target OncePerWindowsOrLinuxTesting =>
        target => target
            .Executes(() =>
                      {
                          const string testFilter = "(TestCategory != Windows & TestCategory != PlatformAgnostic) | TestCategory = RunOnceOnWindowsAndLinux";

                          var runningInTeamCity = TeamCity.Instance is not null;
                          var outputDirectory = RootDirectory / "outputs";
                          
                          var settingsFilePath = TryBuildExcludedTestsSettingsFile(testFilter);

                          DotNetTasks.DotNetTest(settings =>
                                                 {
                                                     settings = settings.SetProjectFile("Binaries/Calamari.Tests.dll");

                                                     settings = settingsFilePath is not null ? settings.SetSettingsFile(settingsFilePath) : settings.SetFilter(testFilter);

                                                     return settings
                                                            .SetProcessExitHandler(process => process.ExitCode switch
                                                                                              {
                                                                                                  0 => null, //successful
                                                                                                  1 => null, //some tests failed
                                                                                                  _ => throw new ProcessException(process)
                                                                                              })
                                                            .SetTestAdapterPath(outputDirectory)
                                                            .AddLoggers("console;verbosity=normal")
                                                            .When(runningInTeamCity, x => x.EnableTeamCityTestLogger(outputDirectory));
                                                 });
                      });

    [PublicAPI]
    Target OncePerWindowsTesting =>
        target => target
            .Executes(() =>
                      {
                          const string testFilter = "TestCategory != macOs & TestCategory != Nix & TestCategory != PlatformAgnostic & TestCategory != nixMacOS & TestCategory != RunOnceOnWindowsAndLinux & TestCategory != ModifiesSystemProxy";

                          var runningInTeamCity = TeamCity.Instance is not null;
                          var outputDirectory = RootDirectory / "outputs";
                          
                          var settingsFilePath = TryBuildExcludedTestsSettingsFile(testFilter);

                          DotNetTasks.DotNetTest(settings =>
                                                 {
                                                     settings = settings.SetProjectFile("Binaries/Calamari.Tests.dll");

                                                     settings = settingsFilePath is not null ? settings.SetSettingsFile(settingsFilePath) : settings.SetFilter(testFilter);

                                                     return settings
                                                            .SetProcessExitHandler(process => process.ExitCode switch
                                                                                              {
                                                                                                  0 => null, //successful
                                                                                                  1 => null, //some tests failed
                                                                                                  _ => throw new ProcessException(process)
                                                                                              })
                                                            .SetTestAdapterPath(outputDirectory)
                                                            .AddLoggers("console;verbosity=normal")
                                                            .When(runningInTeamCity, x => x.EnableTeamCityTestLogger(outputDirectory));
                                                 });
                      });

    [PublicAPI]
    Target WindowsSystemProxyTesting =>
        target => target
            .Executes(() =>
                      {
                          const string testFilter = "TestCategory = Windows & TestCategory = ModifiesSystemProxy";

                          var runningInTeamCity = TeamCity.Instance is not null;
                          var outputDirectory = RootDirectory / "outputs";
                          
                          var settingsFilePath = TryBuildExcludedTestsSettingsFile(testFilter);

                          DotNetTasks.DotNetTest(settings =>
                                                 {
                                                     settings = settings.SetProjectFile("Binaries/Calamari.Tests.dll");

                                                     settings = settingsFilePath is not null ? settings.SetSettingsFile(settingsFilePath) : settings.SetFilter(testFilter);

                                                     return settings
                                                            .SetProcessExitHandler(process => process.ExitCode switch
                                                                                              {
                                                                                                  0 => null, //successful
                                                                                                  1 => null, //some tests failed
                                                                                                  _ => throw new ProcessException(process)
                                                                                              })
                                                            .SetTestAdapterPath(outputDirectory)
                                                            .AddLoggers("console;verbosity=normal")
                                                            .When(runningInTeamCity, x => x.EnableTeamCityTestLogger(outputDirectory));
                                                 });
                      });
    
    
    static string? TryBuildExcludedTestsSettingsFile(string baseFilter)
    {
        var excludedTestsFile = Environment.GetEnvironmentVariable("TeamCityTestExclusionFilePath");
        if (!string.IsNullOrEmpty(excludedTestsFile))
        {
            if (File.Exists(excludedTestsFile))
            {
                var testSet = new HashSet<string>();

                using var filestream = File.OpenRead(excludedTestsFile);
                using var streamReader = new StreamReader(filestream);
                while (streamReader.ReadLine() is { } line)
                    if (!line.StartsWith('#'))
                    {
                        testSet.Add(line);
                    }

                var exclusionWhere = string.Join(" and ",
                                                 testSet.Select(test => $"test != \"{test}\""));

                //normalize to 'cat' for category https://docs.nunit.org/articles/nunit/running-tests/Test-Selection-Language.html
                var normalizedBaseFilter = baseFilter.Replace("TestCategory", "cat");
                var runSettingsFile = $"""
                                       <RunSettings>
                                           <NUnit>
                                               <Where>({normalizedBaseFilter}) && {exclusionWhere}</Where>
                                           </NUnit>
                                       </RunSettings> 
                                       """;

                var filePath = RootDirectory / "excluded.runSettings";
                File.WriteAllText(filePath, runSettingsFile);
                return filePath;
            }
        }

        return null;
    }
}