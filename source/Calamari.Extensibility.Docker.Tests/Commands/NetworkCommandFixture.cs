using Calamari.Extensibility.Docker.Commands;
using NUnit.Framework;

namespace Calamari.Extensibility.Docker.Tests.Commands
{
    [TestFixture]
    public class NetworkCommandFixture
    {
        private static string DefaultName = "Mario";

        [Test]
        public void Network_AppendsSubnets()
        {
            var command = new NetworkCommand(DefaultName);
            command.Subnets.Add("192.170.0.0/16");
            command.Subnets.Add("192.168.1.0/24");

            AssertCommandWithDefaults("--subnet=192.170.0.0/16 --subnet=192.168.1.0/24", command.ToString());
        }

        [Test]
        public void Network_AppendsIPRanges()
        {
            var command = new NetworkCommand(DefaultName);
            command.IpRanges.Add("192.170.0.0/16");
            command.IpRanges.Add("192.168.1.0/24");

            AssertCommandWithDefaults("--ip-range=192.170.0.0/16 --ip-range=192.168.1.0/24", command.ToString());
        }

        [Test]
        public void Network_AppendsGateways()
        {
            var command = new NetworkCommand(DefaultName);
            command.Gateways.Add("192.170.0.0/16");
            command.Gateways.Add("192.168.1.0/24");

            AssertCommandWithDefaults("--gateway=192.170.0.0/16 --gateway=192.168.1.0/24", command.ToString());
        }

        void AssertCommandWithDefaults(string innerArgs, string actual)
        {
            Assert.AreEqual($"docker network create {innerArgs} {DefaultName}", actual);
        }
    }
}
