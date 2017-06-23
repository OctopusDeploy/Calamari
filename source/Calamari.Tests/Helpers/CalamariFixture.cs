using System;
using System.IO;
using Calamari.Commands;
using Calamari.Integration.Processes;
using Calamari.Integration.ServiceMessages;
using Octostache;
using System.Reflection;

namespace Calamari.Tests.Helpers
{
    public abstract class CalamariFixture
    {
        protected CommandLine Calamari()
        {
#if NETFX
            var calamariFullPath = typeof(DeployPackageCommand).GetTypeInfo().Assembly.FullLocalPath();
            return new CommandLine(calamariFullPath);

#else
            var folder = Path.GetDirectoryName(typeof(Program).GetTypeInfo().Assembly.FullLocalPath());
            var calamariFullPath = Path.Combine(folder, "Calamari.Tests.dll");

            if (!File.Exists(calamariFullPath))
                throw new Exception($"Could not find Calamari test wrapper at {calamariFullPath}");
            return new CommandLine(calamariFullPath).UseDotnet();
#endif
        }

        protected CommandLine OctoDiff()
        {
            var octoDiffExe = OctoDiffCommandLineRunner.FindOctoDiffExecutable();

            return new CommandLine(octoDiffExe);
        }

        protected CalamariResult Invoke(CommandLine command, VariableDictionary variables)
        {
            var capture = new CaptureCommandOutput();
            var runner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables), capture));
            var result = runner.Execute(command.Build());
            return new CalamariResult(result.ExitCode, capture);
        }

        protected CalamariResult Invoke(CommandLine command)
        {
            return Invoke(command, new VariableDictionary());
        }

        protected string GetFixtureResouce(params string[] paths)
        {
            var type = GetType();
            return GetFixtureResouce(type, paths);
        }

        public static string GetFixtureResouce(Type type, params string[] paths)
        {
            var path = type.Namespace.Replace("Calamari.Tests.", String.Empty);
            path = path.Replace('.', Path.DirectorySeparatorChar);
            return Path.Combine(TestEnvironment.CurrentWorkingDirectory, path, Path.Combine(paths));
        }
    }
}