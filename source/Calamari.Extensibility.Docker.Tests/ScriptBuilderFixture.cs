using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Utilities;
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

            Assert.AreEqual("docker run -d", result);
        }

        [Test]
        public void MultipleVariables()
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
