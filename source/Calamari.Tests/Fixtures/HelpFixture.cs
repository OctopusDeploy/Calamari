using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures
{
    [TestFixture]
    [Category(TestEnvironment.CompatableOS.All)]
    public class HelpFixture : CalamariFixture
    {
        [Test]
        public void NoArgumentsShouldPrintHelp()
        {
            var output = Invoke(Calamari());
            output.AssertNonZero();
            output.AssertOutput("Usage: Calamari");
        }

        [Test]
        public void UnknownArgumentShouldPrintHelp()
        {
            var output = Invoke(Calamari().Action("whatever"));
            output.AssertNonZero();
            output.AssertOutput("Command 'whatever' is not supported");
            output.AssertOutput("Usage: Calamari");
        }

        [Test]
        public void HelpShouldPrintHelp()
        {
            var output = Invoke(Calamari().Action("help"));
            output.AssertZero();
            output.AssertOutput("Usage: Calamari");
        }

        [Test]
        public void HelpOnCommandShouldPrintHelp()
        {
            var output = Invoke(Calamari().Action("help").Argument("run-script"));
            output.AssertZero();
            output.AssertOutput("Usage: Calamari run-script");
        }

        [Test]
        public void QuestionMarkShouldPrintHelp()
        {
            var output = Invoke(Calamari().Action("-?"));
            output.AssertZero();
            output.AssertOutput("Usage: Calamari");
        }
    }
}
