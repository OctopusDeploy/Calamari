using Calamari.Extensibility.Docker.Commands;
using NUnit.Framework;

namespace Calamari.Extensibility.Docker.Tests.Commands
{
    [TestFixture]
    public class DockerRunCommandFixture
    {
        [Test]
        public void DockerRun_ReturnNothingIfNoVariables()
        {
            var command = new DockerRunCommand("busybox");

            Assert.AreEqual("docker run --detach busybox", command.ToString());
        }


        [Test]
        public void DockerRun_AppendsNetworkAliasWhenNetworkTypeNotNetwork()
        {

            var command = new DockerRunCommand(DefaultImage);
            command.NetworkAliases.Add("Smith");

            AssertCommandWithDefaults("--network-alias=\"Smith\"", command.ToString());
        }

        [Test]
        public void DockerRun_UsesNetworkNameWhenNetworkTypeNetwork()
        {
            var command = new DockerRunCommand(DefaultImage)
            {
                NetworkType = "network",
                NetworkName = "Smith",
                NetworkContainer = "Will"
            };

            AssertCommandWithDefaults("--network=\"Smith\"", command.ToString());
        }

        [Test]
        public void DockerRun_UsesContainerNameWhenNetworkTypeContainer()
        {
            var command = new DockerRunCommand(DefaultImage)
            {
                NetworkType = "container",
                NetworkName = "Smith",
                NetworkContainer = "Will"
            };

            AssertCommandWithDefaults("--network=\"container:Will\"", command.ToString());
        }

        [Test]
        public void DockerRun_SetsNetworkType()
        {
            var command = new DockerRunCommand(DefaultImage)
            {
                NetworkType = "none",
                NetworkName = "Smith",
                NetworkContainer = "Will"
            };

            AssertCommandWithDefaults("--network=none", command.ToString());            
        }

        [Test]
        public void DockerRun_GetsRestartPolicyWithCountWhenOnFailure()
        {
            var command = new DockerRunCommand(DefaultImage)
            {
                RestartPolicy = "on-failure",
                RestartPolicyMax = 2
            };

            AssertCommandWithDefaults("--restart on-failure:2", command.ToString());
        }

        [Test]
        public void DockerRun_IgnoreRetryPolicyCountIfNoneProvided()
        {
            var command = new DockerRunCommand(DefaultImage)
            {
                RestartPolicy = "on-failure",
            };

            AssertCommandWithDefaults("--restart on-failure", command.ToString());
        }

        [Test]
        public void DockerRun_IgnoreRetryPolicyCountIfWrongType()
        {
            var command = new DockerRunCommand(DefaultImage)
            {
                RestartPolicy = "never",
                RestartPolicyMax = 2
            };

            AssertCommandWithDefaults("--restart never", command.ToString());
        }

        [Test]
        public void DockerRun_PublishedAllPortsWhenAutoMapped()
        {
            var command = new DockerRunCommand(DefaultImage)
            {
                PortAutoMap = true
            };

            AssertCommandWithDefaults("--publish-all", command.ToString());
        }

        [Test]
        public void DockerRun_PortMapping_AssumeHostIncludesPortWhenNonEndingSemicolon()
        {
            var command = new DockerRunCommand(DefaultImage);
            command.PortMappings.Add("80", "192.168.0.1:45");

            AssertCommandWithDefaults("--publish 192.168.0.1:45:80", command.ToString());
        }


        [Test]
        public void DockerRun_PortMapping_AssumeHostIPWhenNotNumber()
        {
            var command = new DockerRunCommand(DefaultImage);
            command.PortMappings.Add("80", "192.168.0.1");

            AssertCommandWithDefaults("--publish 192.168.0.1::80", command.ToString());
        }

        [Test]
        public void DockerRun_PortMapping_AssumeHostIsPortWhenIsANumber()
        {
            var command = new DockerRunCommand(DefaultImage);
            command.PortMappings.Add("80", "81");

            AssertCommandWithDefaults("--publish 81:80", command.ToString());
        }


        [Test]
        public void DockerRun_PortMapping_ParseWhenNoHostProvided()
        {
            var command = new DockerRunCommand(DefaultImage);
            command.PortMappings.Add("80", "");

            AssertCommandWithDefaults("--publish 80", command.ToString());
        }

        [Test]
        public void DockerRun_VolumesFrom()
        {
            var command = new DockerRunCommand(DefaultImage);
            command.VolumesFrom.Add("A");

            AssertCommandWithDefaults("--volumes-from=\"A\"", command.ToString());
        }


        [Test]
        public void DockerRun_VolumeBinding_MissingHostSupported()
        {
            var command = new DockerRunCommand(DefaultImage);
            command.VolumeBindings.Add("/home", new DockerRunCommand.VolumeBinding());

            AssertCommandWithDefaults("--volume \"/home\"", command.ToString());
        }

        [Test]
        public void DockerRun_VolumeBinding_HostPrepended()
        {
            var command = new DockerRunCommand(DefaultImage);
            command.VolumeBindings.Add("/home", new DockerRunCommand.VolumeBinding()
            {
                Host = "/temp"
            });

            AssertCommandWithDefaults("--volume \"/temp:/home\"", command.ToString());
        }


        [Test]
        public void DockerRun_VolumeBinding_ReadOnlyAppended()
        {
            var command = new DockerRunCommand(DefaultImage);
            command.VolumeBindings.Add("/home", new DockerRunCommand.VolumeBinding()
            {
                Host = "/temp",
                ReadOnly = "True"
            });

            AssertCommandWithDefaults("--volume \"/temp:/home:ro\"", command.ToString());
        }

        [Test]
        public void DockerRun_VolumeBinding_NoCopyAppended()
        {
            var command = new DockerRunCommand(DefaultImage);
            command.VolumeBindings.Add("/home", new DockerRunCommand.VolumeBinding()
            {
                Host = "/temp",
                NoCopy = "True"
            });

            AssertCommandWithDefaults("--volume \"/temp:/home:nocopy\"", command.ToString());
        }

        [Test]
        public void DockerRun_EnvironmentVariableObjectParsed()
        {
            var command = new DockerRunCommand(DefaultImage);
            command.EnvironmentVariables.Add("Variable1", "Value");
            command.EnvironmentVariables.Add("Variable2", "2");

            StringAssert.Contains(" --env \"Variable1=Value\"", command.ToString());
            StringAssert.Contains(" --env \"Variable2=2\"", command.ToString());
        }

        [Test]
        public void DockerRun_xUsesNetworkNameWhenNetworkTypeNetwork()
        {
            var command = new DockerRunCommand(DefaultImage);
            command.AddedHosts.Add("docker","10.1.1.2");

            AssertCommandWithDefaults("--add-host=docker:10.1.1.2", command.ToString());
        }

        [Test]
        public void DontRun_UsesCreateCommand()
        {
            var command = new DockerRunCommand("busybox");
            command.DontRun = true;

            Assert.AreEqual("docker create busybox", command.ToString());
        }


        [Test]
        public void EntryCommand_PlacedAfterImageName()
        {
            var command = new DockerRunCommand("busybox");
            command.EntryCommand = "blah";

            Assert.AreEqual("docker run --detach busybox blah", command.ToString());
        }

        private string DefaultImage = "busybox";

        void AssertCommandWithDefaults(string innerArgs, string actual)
        {
            Assert.AreEqual($"docker run --detach {innerArgs} {DefaultImage}", actual);
        }



    }
}
