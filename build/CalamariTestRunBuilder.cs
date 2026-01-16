using System;
using System.Collections.Generic;
using Nuke.Common.Tooling;

namespace Calamari.Build;

public class CalamariTestRunBuilder(string projectFile, AbsolutePath outputDirectory)
{
    readonly string ProjectFile = projectFile;
    readonly AbsolutePath OutputDirectory = outputDirectory;
    AbsolutePath DotNetPath = DotNetTasks.DotNetPath;
    string? Filter;

    public CalamariTestRunBuilder WithDotNetPath(AbsolutePath value)
    {
        DotNetPath = value;
        return this;
    }

    public CalamariTestRunBuilder WithFilter(string? value)
    {
        Filter = value;
        return this;
    }

    DotNetTestSettings BuildTestSettings()
    {
        var runningInTeamCity = TeamCity.Instance is not null;

        var settings = new DotNetTestSettings()
                       .SetProjectFile(ProjectFile)
                       .SetProcessToolPath(DotNetPath)
                       .SetTestAdapterPath(OutputDirectory)
                       // This is so we can mute tests that fail
                       .SetProcessExitHandler(process => process.ExitCode switch
                                                         {
                                                             0 => null, //successful
                                                             1 => null, //some tests failed
                                                             _ => throw new ProcessException(process)
                                                         })
                       .AddLoggers("console;verbose=normal")
                       .When(runningInTeamCity, x => x.EnableTeamCityTestLogger(OutputDirectory));

        var runSettingsFilePath = TryBuildExcludedTestsSettingsFile(Filter);
        if (runSettingsFilePath is not null)
        {
            settings = settings.SetSettingsFile(runSettingsFilePath);
        }
        else if (Filter is not null)
        {
            settings = settings.SetFilter(Filter);
        }

        return settings;
    }

    public void Execute()
    {
        DotNetTasks.DotNetTest(BuildTestSettings());
    }

    static string? TryBuildExcludedTestsSettingsFile(string? baseFilter)
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
                                                 testSet.Select(test => $"test != '{test}'"));

                //normalize to 'cat' for category https://docs.nunit.org/articles/nunit/running-tests/Test-Selection-Language.html
                //replace & and | with words as it's being written into XML
                var normalizedBaseFilter = baseFilter?.Replace("TestCategory", "cat").Replace("&", "and").Replace("|", "or");

                var whereClause = normalizedBaseFilter is not null
                    ? $"({normalizedBaseFilter}) and {exclusionWhere}"
                    : exclusionWhere;

                var runSettingsFile = $"""
                                       <RunSettings>
                                           <NUnit>
                                               <Where>{whereClause}</Where>
                                           </NUnit>
                                       </RunSettings> 
                                       """;

                var filePath = KnownPaths.RootDirectory / "excluded.runSettings";
                File.WriteAllText(filePath, runSettingsFile);

                TeamCity.Instance.PublishArtifacts(filePath);

                return filePath;
            }
        }

        return null;
    }
}