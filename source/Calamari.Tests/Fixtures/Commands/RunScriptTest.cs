using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Commands
{
    [TestFixture]
    public class RunScriptTest
    {
        [Test]
        public void RunScript()
        {
            var retCode = Program.Main(new[] {"run-script"});
            // Expected because we don't pass the required variables
            Assert.AreEqual(1, retCode);
        }
    }
}
