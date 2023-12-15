using System.IO;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Serilog;

namespace Calamari.Build;

partial class Build
{
    [Parameter(Name = "CalamariFlavour")] readonly string CalamariFlavourToTest;
    [Parameter(Name = "VSTest_TestCaseFilter")] readonly string CalamariFlavourTestCaseFilter;

    [PublicAPI]
    Target TestCalamariFlavourProject => target => target.Executes(async () =>
    {
        var testProject = $"Calamari.{CalamariFlavourToTest}.Tests";

        var affectedProjectFile = RootDirectory / "affected.proj";
        bool isAffected;
        if (affectedProjectFile.FileExists())
        {
            Log.Information("Affected projects analysis found; checking to see if {TestProject} is affected",
                testProject);
            var contents = await File.ReadAllTextAsync(affectedProjectFile);

            isAffected = contents.Contains(testProject);
        }
        else
        {
            Log.Information("Affected projects analysis not found; assuming {TestProject} *is* affected", testProject);
            isAffected = true;
        }

        if (isAffected)
        {
            Log.Verbose("{TestProject} tests will be executed", testProject);

            DotNetTasks.DotNetTest(settings => settings
                .SetProjectFile($"CalamariTests/{testProject}.dll")
                .SetFilter(CalamariFlavourTestCaseFilter)
                .SetLoggers("trx")
                .SetProcessExitHandler(
                    process => process.ExitCode switch
                    {
                        0 => null, //successful
                        1 => null, //some tests failed
                        _ => throw new ProcessException(process)
                    }));
        }
        else
        {
            Log.Information("{TestProject} is not affected, so no tests will be executed", testProject);
            Log.Information(
                $"##teamcity[testStarted name='{testProject}-NoTests' captureStandardOutput='false']");
            Log.Information($"##teamcity[testFinished name='{testProject}-NoTests' duration='0']");
        }
    });
}