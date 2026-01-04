using System.Threading.Tasks;

namespace Calamari.Build;

partial class Build
{

    CalamariTestRunBuilder CreateTestRun(string projectFileOrDll)
    {
        var outputDir = RootDirectory / "outputs";
        return new CalamariTestRunBuilder(projectFileOrDll, outputDir);
    }
}