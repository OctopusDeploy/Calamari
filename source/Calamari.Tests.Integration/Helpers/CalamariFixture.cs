using System;
using System.IO;
using System.Reflection;
using Calamari.Commands;
using Calamari.Extensibility;
using Calamari.Features;
using Calamari.Integration.Processes;
using Calamari.Integration.ServiceMessages;
using Octostache;

#if APPROVAL_TESTS
using ApprovalTests.Namers;
using ApprovalTests.Reporters;
#endif

namespace Calamari.Tests.Integration.Helpers
{
#if APPROVAL_TESTS
    [UseReporter(typeof(DiffReporter))]
    [UseApprovalSubdirectory("Approved")]
#endif
    public abstract class CalamariFixture
    {
        protected CommandLine Calamari()
        {
            string calamariFullPath;
#if NET40
            calamariFullPath = typeof(DeployPackageCommand).GetTypeInfo().Assembly.FullLocalPath();
#else
            var folder = Path.GetDirectoryName(typeof(Program).GetTypeInfo().Assembly.FullLocalPath());
            if(Util.CrossPlatform.IsWindows())
            {
                calamariFullPath = Path.Combine(folder, "Calamari.Tests.Integration.exe");
            }
            else
            {
                calamariFullPath = Path.Combine(folder, "Calamari.Tests.Integration.dll");
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
            var path = GetType().Namespace.Replace("Calamari.Tests.Integration.", String.Empty);
            path = path.Replace('.', Path.DirectorySeparatorChar);
            return Path.Combine(TestEnvironment.CurrentWorkingDirectory, path, Path.Combine(paths));
        }
    }

   

}