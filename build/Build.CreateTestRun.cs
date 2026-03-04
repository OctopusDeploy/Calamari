namespace Calamari.Build;

partial class Build
{
    static CalamariTestRunBuilder CreateTestRun(string projectFileOrDll)
    {
        return new CalamariTestRunBuilder(projectFileOrDll, KnownPaths.OutputsDirectory);
    }
}