using System.Diagnostics;
using System.IO;
using Calamari.Aws.Deployment;
using Calamari.Aws.Inputs;
using Calamari.Aws.Inputs.Ecs;
using Calamari.Aws.Integration.Ecs;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.AWS.Ecs;

[TestFixture]
public class EcsDeployTemplateGeneratorTests
{
    [Test]
    public void MinimalTemplateAppearsAsExpected()
    {
        var fakeLog = Substitute.For<ILog>();
        var fakeStackNameGenerator = Substitute.For<IEcsStackNameGenerator>();
        
        var variables =  new CalamariVariables
        {
            { AwsSpecialVariables.Ecs.Deploy.StackName, "TestStack" },
            { AwsSpecialVariables.Ecs.ClusterName, "TestCluster" },
            { DeploymentEnvironment.Id, "Environment-1"},
            { AwsSpecialVariables.Ecs.Deploy.ServiceTaskName, "SampleTask"},
            { AwsSpecialVariables.Ecs.Deploy.Cpu, "256"},
            { AwsSpecialVariables.Ecs.Deploy.Memory, "512"},
            { AwsSpecialVariables.Ecs.Deploy.RuntimeArchitecturePlatform, "X86_64"},
            { AwsSpecialVariables.Ecs.Deploy.DesiredCount, "1"},
            { AwsSpecialVariables.Ecs.Deploy.MinimumHealthPercent, "100"},
            { AwsSpecialVariables.Ecs.Deploy.MaximumHealthPercent, "200"},
            { AwsSpecialVariables.Ecs.Deploy.AutoAssignPublicIp, "True"},
            { AwsSpecialVariables.Ecs.Deploy.EnableEcsManagedTags, "False"},
            { AwsSpecialVariables.Ecs.WaitOption, """{ "type": "waitWithTimeout", "timeout": 30 }"""},
            { AwsSpecialVariables.Ecs.Deploy.SecurityGroupIds, """
                                                               ["sg-0d5e06a4bde84d1d3"]
                                                               """},
            { AwsSpecialVariables.Ecs.Deploy.SubnetIds, """
                                                        ["subnet-0067a165dd462cb39"]
                                                        """},
            {
                AwsSpecialVariables.Ecs.Deploy.Containers, 
                """[{"containerName":"sample-container","containerImageReference":{"referenceId":"547c5091-b891-4bb2-a582-78489bd9b18c","imageName":"#{Octopus.Action.Package[nginx].Image}","feedId":"Feeds-1001"},"repositoryAuthentication":{"type":"default"},"containerPortMappings":[{"containerPort":80,"protocol":"tcp"}],"essential":"True","environmentFiles":[],"environmentVariables":[],"networkSettings":{"disableNetworking":false,"dnsServers":[],"dnsSearchDomains":[],"extraHosts":[]},"containerStorage":{"readOnlyRootFileSystem":"False","mountPoints":[],"volumeFrom":[]},"containerLogging":{"type":"manual","logDriver":"none","logOptions":[]},"firelensConfiguration":{"type":"disabled"},"dockerLabels":[],"healthCheck":{"command":[]},"dependencies":[],"ulimits":[]}]"""
            },
            {"Octopus.Action.Package[nginx].Image", "docker.io/nginx:1.29.1"}
        };
        
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);
        
        var template = new EcsDeployTemplateGenerator(inputs).Generate();
        
        #if DEBUG
        // Write template content over the top of content in and save "/Users/jt/Developer/Octopus/temp/DeployECSOutputs/Calamari.json"
        File.WriteAllText("/Users/jt/Developer/Octopus/temp/DeployECSOutputs/Calamari.json", template.Body);
        #endif
        
    }
}