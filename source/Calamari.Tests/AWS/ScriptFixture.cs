using System.Collections.Generic;
using Calamari.Integration.Scripting;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.AWS
{
    [TestFixture]
    public class ScriptFixture : CalamariFixture
    {
        [Test]
        public void RunScript()
        {
            var (output, _) = RunScript(
                GetScriptFileName("awsscript"),
                GetAdditionalVariables(),
                new Dictionary<string, string> {{"extensions", "Calamari.Aws"}}
            );

            output.AssertSuccess();
            output.AssertOutput("user/OctopusAPITester");
        }

        static Dictionary<string, string> GetAdditionalVariables()
        {
            return new Dictionary<string, string>
            {
                {"Octopus.Action.AwsAccount.Variable", "AwsAccount"},
                {"Octopus.Action.Aws.Region", "us-east-1"},
                {"AwsAccount.AccessKey", ExternalVariables.Get(ExternalVariable.AwsAcessKey)},
                {"AwsAccount.SecretKey", ExternalVariables.Get(ExternalVariable.AwsSecretKey)},
                {"Octopus.Action.Aws.AssumeRole", "False"},
                {"Octopus.Action.Aws.AssumedRoleArn", ""},
                {"Octopus.Action.Aws.AssumedRoleSession", ""},
            };
        }

        static string GetScriptFileName(string fileName) =>
            $"{fileName}.{(CalamariEnvironment.IsRunningOnWindows ? ScriptSyntax.PowerShell : ScriptSyntax.Bash).FileExtension()}";
    }
}