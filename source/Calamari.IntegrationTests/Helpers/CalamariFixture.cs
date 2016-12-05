using System;
using System.IO;
using System.Net;
using System.Reflection;
using Calamari.Commands;
using Calamari.Extensibility;
using Calamari.Extensibility.FileSystem;
using Calamari.Features;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.ServiceMessages;
using NUnit.Framework;

#if APPROVAL_TESTS
using ApprovalTests.Namers;
using ApprovalTests.Reporters;
#endif

namespace Calamari.IntegrationTests.Helpers
{
    public abstract class CalamariFixture
    {
        private static ICalamariFileSystem filesystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

        [SetUp]
        public void SetUpExtensionsPath()
        {
            var testExtensionsDirectoryPath = Path.Combine(TestEnvironment.CurrentWorkingDirectory, "Fixtures", "Extensions");

            TentacleHome = filesystem.CreateTemporaryDirectory();
            Environment.SetEnvironmentVariable("TentacleHome", TentacleHome);
            Console.WriteLine("TentacleHome is set to: " + TentacleHome);

            filesystem.CopyDirectory(testExtensionsDirectoryPath, Path.Combine(TentacleHome, "Extensions"));
            Directory.CreateDirectory(DownloadPath);
        }

        [TearDown]
        public void TestFixtureTearDown()
        {
            filesystem.DeleteDirectory(TentacleHome, FailureOptions.IgnoreFailure);
            TentacleHome = null;
            Environment.SetEnvironmentVariable("TentacleHome", TentacleHome);
        }

        protected string TentacleHome { get; private set; }
        protected string DownloadPath => TestEnvironment.GetTestPath(TentacleHome, "Files");

        protected CommandLine Calamari()
        {
           
            string calamariFullPath;
#if NET40
            calamariFullPath = typeof(DeployPackageCommand).GetTypeInfo().Assembly.FullLocalPath();
#else
            var folder = Path.GetDirectoryName(typeof(Program).GetTypeInfo().Assembly.FullLocalPath());
            if(Util.CrossPlatform.IsWindows())
            {
                calamariFullPath = Path.Combine(folder, "Calamari.Tests.exe");
            }
            else
            {
                calamariFullPath = Path.Combine(folder, "Calamari.Tests.dll");
            }
#endif
            return CommandLine.Execute(calamariFullPath).DotNet();
        }

        protected CommandLine OctoDiff()
        {
            var octoDiffExe = OctoDiffCommandLineRunner.FindOctoDiffExecutable();
            return CommandLine.Execute(octoDiffExe);
        }

        protected CalamariResult Invoke(CommandLine command, IVariableDictionary variables = null)
        {
            var capture = new CaptureCommandOutput();
            var runner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables ?? new CalamariVariableDictionary()), capture));
            var result = runner.Execute(command.Build());
            return new CalamariResult(result.ExitCode, capture);
        }

        protected CommandLine InProcessCalamari()
        {
            var program = new Calamari.Program("Calamari", typeof(Calamari.Program).GetTypeInfo().Assembly.GetInformationalVersion());
            return new CommandLine(args => program.Execute(args));
        }

        protected CalamariResult InProcessInvoke(CommandLine command, IVariableDictionary variables = null) { 

            var directInvocation = command.BuildLibraryCall();
            using (var log = new ProxyLog())
            {
                var capturedCommand = new CaptureCommandOutput();
                int exitCode = 1;
                try
                {
                    exitCode = directInvocation.Executable(directInvocation.Arguments);
                }
                catch (Exception)
                {
                    //return new CalamariResult(1, ls.WriteOutput());
                }
                log.WriteOutput(new SplitCommandOutput(new ServiceMessageCommandOutput(variables ?? new CalamariVariableDictionary()), capturedCommand));
                return new CalamariResult(exitCode, capturedCommand);
            }
        }
        
        protected string GetFixtureResouce(params string[] paths)
        {
            var type = GetType();
            return GetFixtureResouce(type, paths);
        }

        public static string GetFixtureResouce(Type type, params string[] paths)
        {
            var path = type.Namespace.Replace("Calamari.IntegrationTests.", String.Empty);
            path = path.Replace('.', Path.DirectorySeparatorChar);
            return Path.Combine(TestEnvironment.CurrentWorkingDirectory, path, Path.Combine(paths));
        }
    }
}