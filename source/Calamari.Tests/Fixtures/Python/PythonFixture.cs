using System.Collections.Generic;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Python
{
    public class PythonFixture : CalamariFixture
    {
        [Test]
        public void ShouldCallHello()
        {
            var (output, _) = RunScript("hello.py");

            output.AssertSuccess();
            output.AssertOutput("Hello");
        }

        [Test]
        public void ShouldPrintVariables()
        {
            var (output, _) = RunScript("printvariables.py", new Dictionary<string, string>
            {
                ["Variable1"] = "ABC",
                ["Variable2"] = "DEF",
                ["Variable3"] = "GHI",
                ["Foo_bar"] = "Hello",
                ["Host"] = "Never",
            });

            output.AssertSuccess();
            output.AssertOutput("V1= ABC");
            output.AssertOutput("V2= DEF");
            output.AssertOutput("V3= GHI");
            output.AssertOutput("Foo_bar= Hello");
        }

    }
}