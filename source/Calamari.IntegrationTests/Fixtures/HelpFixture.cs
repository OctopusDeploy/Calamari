using Calamari.IntegrationTests.Helpers;
using NUnit.Framework;

namespace Calamari.IntegrationTests.Fixtures
{
    [TestFixture]
    public class HelpFixture : CalamariFixture
    {
        [Test]
        public void NoArgumentsShouldPrintHelp()
        {
            var output = Invoke(Calamari());
            output.AssertFailure();
            output.AssertOutput("Usage: Calamari");
        }

        [Test]
        public void UnknownArgumentShouldPrintHelp()
        {
            var output = Invoke(Calamari().Action("whatever"));
            output.AssertFailure();
            output.AssertOutput("Command 'whatever' is not supported");
            output.AssertOutput("Usage: Calamari");
        }

        [Test]
        public void HelpShouldPrintHelp()
        {
            var output = Invoke(Calamari().Action("help"));
            output.AssertSuccess();
            output.AssertOutput("Usage: Calamari");
        }

        [Test]
        public void HelpOnCommandShouldPrintHelp()
        {
            var output = Invoke(Calamari().Action("help").Argument("run-script"));
            output.AssertSuccess();
            output.AssertOutput("Usage: Calamari run-script");
        }

        [Test]
        public void QuestionMarkShouldPrintHelp()
        {
            var output = Invoke(Calamari().Action("-?"));
            output.AssertSuccess();
            output.AssertOutput("Usage: Calamari");
        }
    }
}
