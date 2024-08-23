using Calamari.FullFrameworkTools.Command;
using NUnit.Framework;

namespace Calamari.FullFrameworkTools.Tests
{
    [TestFixture]
    public class SerializedLogTest
    {

        [Test]
        public void Foo()
        {
            var log = new SerializedLog();
            
            log.Error("CAKE");
        }
    }
}