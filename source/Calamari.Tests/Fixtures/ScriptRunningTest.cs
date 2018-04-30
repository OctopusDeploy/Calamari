using NUnit.Framework;
using Octopus.CoreUtilities.Extensions;
using System;
using System.IO;
using System.Reflection;
using Autofac;
using Calamari.Integration.Processes;
using Octostache;

namespace Calamari.Tests.Fixtures
{
    [TestFixture]
    public class ScriptRunningTest
    {
        private IContainer container;

        private string[] Args =>
            ScriptRunningTest.FullLocalPath(typeof(ScriptRunningTest).Assembly)
                .Map(dllPath => Path.GetDirectoryName(dllPath))
                .Map(dllDir => Path.Combine(dllDir, "Scripts"))
                .Map(scriptPath => new[]
                    {"run-test-script", "--script=" + scriptPath + "\\awsscript.ps1", "--extensions=Aws,Tests"});

        private static string FullLocalPath(Assembly assembly) =>
            Uri.UnescapeDataString(new UriBuilder(assembly.CodeBase).Path).Replace("/", "\\");

        private CalamariVariableDictionary BuildVariables(CalamariVariableDictionary variables)
        {
            variables.Set("Octopus.Action.AwsAccount.Variable", "AwsAccount");
            variables.Set("Octopus.Action.Aws.Region", "us-east-1");
            variables.Set("AwsAccount.AccessKey", "AKIAIHVPNPPZOLA4TYVQ");
            variables.Set("AwsAccount.SecretKey", "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");
            variables.Set("Octopus.Action.Aws.AssumeRole", "False");
            variables.Set("Octopus.Action.Aws.AssumedRoleArn", "");
            variables.Set("Octopus.Action.Aws.AssumedRoleSession", "");
            return variables;
        }

        [SetUp]
        public void SetUp()
        {
            container = Calamari.Program.BuildContainer(Args);
        }

        [TearDown]
        public void TearDown()
        {
            container?.Dispose();
        }

        [Test]
        public void RunScript()
        {
            BuildVariables(container.Resolve<CalamariVariableDictionary>());
            var retCode = container.Resolve<Calamari.Program>().Execute(Args);
            Assert.AreEqual(0, retCode);
        }
    }
}
