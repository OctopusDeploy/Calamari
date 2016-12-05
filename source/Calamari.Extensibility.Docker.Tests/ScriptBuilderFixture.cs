using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Utilities;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Extensibility.Docker.Tests
{
    [TestFixture]
    public class ScriptBuilderFixture
    {
        [Test]
        public void EmptyVariableDictionary()
        {
            var result = ScriptBuilder.Run(new VariableDictionary());

            Assert.AreEqual("docker run --detach", result);
        }

        [Test]
        public void NetworkCommand()
        {
            var variables = new VariableDictionary
            {
                [SpecialVariables.Action.Docker.NetworkIPRange] = "192.171.0.0/16,192.169.1.0/24",
                [SpecialVariables.Action.Docker.NetworkSubnet] = "192.170.0.0/16,192.168.1.0/24",
                [SpecialVariables.Action.Docker.NetworkGateway] = "192.169.0.0/16,192.167.1.0/24",
                [SpecialVariables.Action.Docker.NetworkType] = "other",
                [SpecialVariables.Action.Docker.NetworkCustomDriver] = "bananas",
                [SpecialVariables.Action.Docker.Args] = "--somethingelse"
            };

            var generator = Substitute.For<RandomStringGenerator>();
            generator.Generate(Arg.Any<int>()).Returns("ABC");

            var result = ScriptBuilder.Network(generator, variables);

            var expected = "docker network create " +
                           "--driver=\"bananas\" " +
                           "--subnet=192.170.0.0/16 --subnet=192.168.1.0/24 " +
                           "--ip-range=192.171.0.0/16 --ip-range=192.169.1.0/24 " +
                           "--gateway=192.169.0.0/16 --gateway=192.167.1.0/24 " +
                           "--somethingelse " +
                           "network_abc";

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void RunCommand()
        {
            var variables = new VariableDictionary
            {
                [SpecialVariables.Tentacle.CurrentDeployment.PackageFilePath] = "MyImage",
                [SpecialVariables.Action.Docker.NetworkContainer] = "mynetwork",
                [SpecialVariables.Action.Docker.NetworkType] = "container",
                [SpecialVariables.Action.Docker.RestartPolicy] = "on-failure",
                [SpecialVariables.Action.Docker.RestartPolicyMax] = "3",
                [SpecialVariables.Action.Docker.EnvVariable] = "{\"Variable1\":\"cake\", \"Variable2\":4}",
                [SpecialVariables.Action.Docker.PortMapping] = "{\"80\": \"192.168.0.1:45\", \"85\": \"\"}",
                [SpecialVariables.Action.Docker.VolumeBindings] = "{\"/home\": {\"host\": \"/temp\", \"noCopy\": \"True\" }, \"/users\": null}",
                [SpecialVariables.Action.Docker.VolumesFrom] = "A,B, C",
                [SpecialVariables.Action.Docker.Args] = "--somethingelse",
                [SpecialVariables.Action.Docker.Command] = "\"echo something\""
            };

            var result = ScriptBuilder.Run(variables);

            var expected = "docker run --detach " +
                           "--volume \"/temp:/home:nocopy\" " +
                           "--volume \"/users\" " +
                           "--restart on-failure:3 " +
                           "--volumes-from=\"A\" " +
                           "--volumes-from=\"B\" " +
                           "--volumes-from=\"C\" " +
                           "--publish 192.168.0.1:45:80 " +
                           "--publish 85 " +
                           "--network=\"container:mynetwork\" " +
                           "--env \"Variable1=cake\" " +
                           "--env \"Variable2=4\" " +
                           "--somethingelse " +
                           "MyImage \"echo something\"";

            Assert.AreEqual(expected, result);
        }

    }
}
