using Calamari.NamingIsHard;
using NUnit.Framework;
using Sashimi.Tests.Shared.Server;

namespace Sashimi.NamingIsHard.Tests
{
    [TestFixture]
    public class MyActionHandlerFixture
    {
        [Test]
        public void Test1()
        {
            ActionHandlerTestBuilder.CreateAsync<MyActionHandler, Program>()
                .WithArrange(context =>
                {

                })
                .Execute();
        }
    }
}