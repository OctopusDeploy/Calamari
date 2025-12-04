using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.CI.TeamCity;
using Serilog;

namespace Calamari.Build;

partial class Build
{
    [Parameter(Name = "CalamariFlavour")] readonly string? CalamariFlavourToTest;
    [Parameter(Name = "VSTest_TestCaseFilter")] readonly string? CalamariFlavourTestCaseFilter;

    [PublicAPI]
    Target TestCalamariFlavourProject =>
        target => target
            .Executes(() =>
                      {
                          var testProject = $"Calamari.{CalamariFlavourToTest}.Tests";

                          Log.Verbose("{TestProject} tests will be executed", testProject);

                          var runningInTeamCity = TeamCity.Instance is not null;
                          var outputDirectory = RootDirectory / "outputs";

                          DotNetTasks.DotNetTest(settings => settings
                                                             .SetProjectFile($"CalamariTests/{testProject}.dll")
                                                             .SetFilter(CalamariFlavourTestCaseFilter)
                                                             .SetProcessExitHandler(process => process.ExitCode switch
                                                                                               {
                                                                                                   0 => null, //successful
                                                                                                   1 => null, //some tests failed
                                                                                                   _ => throw new ProcessException(process)
                                                                                               })
                                                             .SetTestAdapterPath(outputDirectory)
                                                             .AddLoggers("console;verbosity=normal")
                                                             .When(runningInTeamCity, x => x.EnableTeamCityTestLogger(outputDirectory)));
                      });
}