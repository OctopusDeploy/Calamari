#if NETCORE
using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Aws.Integration;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using NUnit.Framework;
using CalamariResult = Calamari.Tests.Helpers.CalamariResult;
using CaptureCommandInvocationOutputSink = Calamari.Tests.Helpers.CaptureCommandInvocationOutputSink;

namespace Calamari.Tests.KubernetesFixtures
{
    public abstract class KubernetesContextScriptWrapperLiveFixtureBase
    {
        protected const string testNamespace = "calamari-testing";

        InMemoryLog log;
        protected IVariables variables;
        protected string testFolder;

        [OneTimeSetUp]
        public void SetupTests()
        {
            testFolder = Path.GetDirectoryName(GetType().Assembly.FullLocalPath());
        }

        [SetUp]
        public void Setup()
        {
            variables = new CalamariVariables();

            log = new DoNotDoubleLog();

            SetTestClusterVariables();
        }

        protected KubernetesContextScriptWrapper CreateWrapper()
        {
            return new KubernetesContextScriptWrapper(variables, log, new AssemblyEmbeddedResources(), new TestCalamariPhysicalFileSystem());
        }

        void SetTestClusterVariables()
        {

            variables.Set(SpecialVariables.Namespace, testNamespace);
            variables.Set(ScriptVariables.Syntax, CalamariEnvironment.IsRunningOnWindows ? ScriptSyntax.PowerShell.ToString() : ScriptSyntax.Bash.ToString());
        }

        CalamariResult ExecuteScript(IScriptWrapper wrapper, string scriptName)
        {
            var calamariResult = ExecuteScriptInternal(new CommandLineRunner(log, variables), wrapper, scriptName);

            foreach (var message in log.Messages)
            {
                Console.WriteLine($"[{message.Level}] {message.FormattedMessage}");
            }

            return calamariResult;
        }

        CalamariResult ExecuteScriptInternal(ICommandLineRunner runner, IScriptWrapper wrapper, string scriptName)
        {
            var wrappers = new List<IScriptWrapper>(new[] { wrapper });
            if (variables.Get(Deployment.SpecialVariables.Account.AccountType) == "AmazonWebServicesAccount")
            {
                wrappers.Add(new AwsScriptWrapper(log, variables));
            }

            var engine = new ScriptEngine(wrappers);
            var result = engine.Execute(new Script(scriptName), variables, runner, GetEnvironments());

            return new CalamariResult(result.ExitCode, new CaptureCommandInvocationOutputSink());
        }

        protected virtual Dictionary<string, string> GetEnvironments()
        {
            return new Dictionary<string, string>();
        }

        protected void TestScript(IScriptWrapper wrapper, string scriptName)
        {
            using (var dir = TemporaryDirectory.Create())
            using (var temp = new TemporaryFile(Path.Combine(dir.DirectoryPath, $"{scriptName}.{(variables.Get(ScriptVariables.Syntax) == ScriptSyntax.Bash.ToString() ? "sh" : "ps1")}")))
            {
                File.WriteAllText(temp.FilePath, "kubectl cluster-info");

                var output = ExecuteScript(wrapper, temp.FilePath);
                output.AssertSuccess();
            }
        }
    }
}
#endif