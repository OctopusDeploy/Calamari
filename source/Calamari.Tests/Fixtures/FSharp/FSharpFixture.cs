using System.Collections.Generic;
using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.FSharp
{
    [TestFixture]
    [Category(TestEnvironment.ScriptingSupport.FSharp)]
    public class FSharpFixture : CalamariFixture
    {
        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldPrintEncodedVariable()
        {
            var (output, _) = RunScript("OutputVariable.fsx");
            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='RG9ua2V5' value='S29uZw==']");
        }
        
        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldPrintSensitiveVariable()
        {

            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "SensitiveOutputVariable.fsx")));

            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='UGFzc3dvcmQ=' value='Y29ycmVjdCBob3JzZSBiYXR0ZXJ5IHN0YXBsZQ==' sensitive='VHJ1ZQ==']");
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldCreateArtifact()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "CreateArtifact.fsx")));

            output.AssertSuccess();
            output.AssertOutput("##octopus[createArtifact");
            output.AssertOutput("name='bXlGaWxlLnR4dA==' length='MTAw']");
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldCallHello()
        {
            var variablesFile = Path.GetTempFileName();

            var variables = new VariableDictionary();
            variables.Set("Name", "Paul");
            variables.Set("Variable2", "DEF");
            variables.Set("Variable3", "GHI");
            variables.Set("Foo_bar", "Hello");
            variables.Set("Host", "Never");
            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                    .Action("run-script")
                    .Argument("script", GetFixtureResouce("Scripts", "Hello.fsx"))
                    .Argument("variables", variablesFile));

                output.AssertSuccess();
                output.AssertOutput("Hello Paul");
            }
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldCallHelloWithSensitiveVariable()
        {
            var variablesFile = Path.GetTempFileName();

            var variables = new VariableDictionary();
            variables.Set("Name", "NameToEncrypt");
            variables.SaveEncrypted("5XETGOgqYR2bRhlfhDruEg==", variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                    .Action("run-script")
                    .Argument("script", GetFixtureResouce("Scripts", "Hello.fsx"))
                    .Argument("sensitiveVariables", variablesFile)
                    .Argument("sensitiveVariablesPassword", "5XETGOgqYR2bRhlfhDruEg=="));

                output.AssertSuccess();
                output.AssertOutput("Hello NameToEncrypt");
            }
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldCallHelloWithVariableSubstitution()
        {
            var (output, _) = RunScript("HelloVariableSubstitution.fsx", new Dictionary<string, string>()
                {["Name"] = "SubstitutedValue"});

            output.AssertSuccess();
            output.AssertOutput("Hello SubstitutedValue");
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldCallHelloDirectValue()
        {
            var variablesFile = Path.GetTempFileName();

            var variables = new VariableDictionary();
            variables.Set("Name", "direct value");
            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                    .Action("run-script")
                    .Argument("script", GetFixtureResouce("Scripts", "Hello.fsx"))
                    .Argument("variables", variablesFile));

                output.AssertSuccess();
                output.AssertOutput("Hello direct value");
            }
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldCallHelloDefaultValue()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "HelloDefaultValue.fsx")));

            output.AssertSuccess();
            output.AssertOutput("Hello default value");
        }
        
        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldCallHelloWithNullVariable()
        {
            var variablesFile = Path.GetTempFileName();

            var variables = new VariableDictionary();
            variables.Set("Name", null);
            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                    .Action("run-script")
                    .Argument("script", GetFixtureResouce("Scripts", "Hello.fsx"))
                    .Argument("variables", variablesFile));

                output.AssertSuccess();
                output.AssertOutput("Hello ");
            }
        }
        
        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldCallHelloWithNullSensitiveVariable()
        {
            var variablesFile = Path.GetTempFileName();

            var variables = new VariableDictionary();
            variables.Set("Name", null);
            variables.SaveEncrypted("5XETGOgqYR2bRhlfhDruEg==", variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                    .Action("run-script")
                    .Argument("script", GetFixtureResouce("Scripts", "Hello.fsx"))
                    .Argument("sensitiveVariables", variablesFile)
                    .Argument("sensitiveVariablesPassword", "5XETGOgqYR2bRhlfhDruEg=="));

                output.AssertSuccess();
                output.AssertOutput("Hello ");
            }
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldConsumeParametersWithQuotes()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "Parameters.fsx"))
                .Argument("scriptParameters", "\"Para meter0\" Parameter1")); ;

            output.AssertSuccess();
            output.AssertOutput("Parameters Para meter0-Parameter1");
        }
        
    }
}