using System.IO;
using Calamari.Integration.Processes;
using Calamari.Tests.Helpers;

namespace Calamari.Azure.Tests
{
    public static class AzureTestEnvironment
    {
        public static readonly string AssemblyLocalPath = typeof(TestEnvironment).Assembly.FullLocalPath();
        public static string TestWorkingDirectory = Path.GetDirectoryName(AssemblyLocalPath);
    }
}