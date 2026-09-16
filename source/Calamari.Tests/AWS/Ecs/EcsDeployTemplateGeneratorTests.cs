using System.IO;
using System.Reflection;
using Calamari.Aws.Deployment;
using Calamari.Aws.Inputs.Ecs;
using Calamari.Aws.Integration.Ecs;
using Calamari.Aws.Integration.Ecs.Deploy;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using Octopus.Calamari.Contracts.Aws.Ecs;

namespace Calamari.Tests.AWS.Ecs;

[TestFixture]
public class EcsDeployTemplateGeneratorTests
{
    readonly ILog fakeLog = Substitute.For<ILog>();
    readonly IEcsStackNameGenerator fakeStackNameGenerator = Substitute.For<IEcsStackNameGenerator>();
    readonly IEcsImageNameResolver fakeEcsImageResolver = Substitute.For<IEcsImageNameResolver>();


    [Test]
    public void WithSimpleVariableSetup_MatchesExpectedSpfOutput()
    {
        var expectedJson = ReadFromFile("simpleSpfOutputTemplate.json");
        fakeEcsImageResolver.ResolveImageName(Arg.Is<ContainerImageReference>(v => v.ReferenceId == "732002f0-4555-4dbf-8dc3-64255eee5f26"), Arg.Any<IVariables>()).Returns("docker.io/nginx:1.29");
        var variables = new CalamariVariables
        {
            { AwsSpecialVariables.Ecs.Deploy.StackName, "ecs-spf-#{Octopus.Deployment.Id}" },
            { AwsSpecialVariables.Ecs.ClusterName, "TestCluster" },
            { DeploymentEnvironment.Id, "Environment-1" },
            { AwsSpecialVariables.Ecs.Deploy.ServiceTaskName, "test-octopus-spfdeployed-task" },
            { AwsSpecialVariables.Ecs.Deploy.Cpu, "256" },
            { AwsSpecialVariables.Ecs.Deploy.Memory, "512" },
            { AwsSpecialVariables.Ecs.Deploy.RuntimeArchitecturePlatform, "X86_64" },
            { AwsSpecialVariables.Ecs.Deploy.DesiredCount, "1" },
            { AwsSpecialVariables.Ecs.Deploy.MinimumHealthPercent, "100" },
            { AwsSpecialVariables.Ecs.Deploy.MaximumHealthPercent, "200" },
            { AwsSpecialVariables.Ecs.Deploy.AutoAssignPublicIp, "True" },
            { AwsSpecialVariables.Ecs.Deploy.EnableEcsManagedTags, "False" },
            { AwsSpecialVariables.Ecs.WaitOption, """{ "type": "waitWithTimeout", "timeout": 30 }""" },
            {
                AwsSpecialVariables.Ecs.Deploy.SecurityGroupIds, """
                                                                 ["sg-0d5e06a4bde84d1d"]
                                                                 """
            },
            {
                AwsSpecialVariables.Ecs.Deploy.SubnetIds, """
                                                          ["subnet-0650cd8a2119e829c", "subnet-0067a165dd462cb39"]
                                                          """
            },
            { AwsSpecialVariables.Ecs.Deploy.TaskExecutionRole, "arn:aws:iam::720766170633:role/ecsTaskExecutionRole" },
            { AwsSpecialVariables.Ecs.Deploy.TaskRole, "arn:aws:iam::720766170633:role/ecsTaskExecutionRole" },
            
            {AwsSpecialVariables.Ecs.Tags, """[{"key":"owner","value":"spfdeployment"},{"key":"createdBy", "value":"#{Octopus.Project.Slug}"}]"""},
            {AwsSpecialVariables.Ecs.Deploy.Containers, """
                                                        [{"containerName":"web-server-spf","containerImageReference":{"referenceId":"732002f0-4555-4dbf-8dc3-64255eee5f26","imageName":"nginx","feedId":"Feeds-1061"},"repositoryAuthentication":{"type":"Default"},"containerPortMappings":[{"containerPort":"80","protocol":"Tcp"}],"essential":"True","environmentFiles":[],"environmentVariables":[{"type":"Plain","key":"env","value":"#{Octopus.Environment.Name}"}],"networkSettings":{"disableNetworking":"False","dnsServers":[],"dnsSearchDomains":[],"extraHosts":[]},"containerStorage":{"readOnlyRootFileSystem":"False","mountPoints":[],"volumeFrom":[]},"containerLogging":{"type":"Manual","logDriver":"None","logOptions":[]},"firelensConfiguration":{"type":"Disabled","enableEcsLogMetadata":""},"dockerLabels":[],"healthCheck":{"command":[]},"dependencies":[],"ulimits":[]}]
                                                        """},

            
            {AwsSpecialVariables.Ecs.Deploy.LoadBalancerMappings, "[]"},
            {AwsSpecialVariables.Ecs.Deploy.Volumes, "[]"},


            {"Octopus.Project.Slug", "test-project"},
            {"Octopus.Deployment.Id", "17"},
            {"Octopus.Environment.Name", "TestEnvironment"}
            
            
        };

        var resultJson = GenerateTemplateFromVariables(variables);
        
        JToken.DeepEquals(resultJson, expectedJson).Should().BeTrue();
    }

    [Test]
    public void WithMultipleContainers_MatchesExpectedSpfOutput()
    {
        var expectedJson = ReadFromFile("multiContainerSpfOutputTemplate.json");
        
        fakeEcsImageResolver.ResolveImageName(Arg.Is<ContainerImageReference>(v => v.ReferenceId == "939a08a0-7dd9-471d-ac31-ac8e29eb04ff"), Arg.Any<IVariables>()).Returns("docker.io/nginx:1.31.1");
        fakeEcsImageResolver.ResolveImageName(Arg.Is<ContainerImageReference>(v => v.ReferenceId == "07c8e308-048f-4289-b25a-86fb901e2824"), Arg.Any<IVariables>()).Returns("docker.io/bitnami/redis:sha256-fd997c4c52c0a0af686e5af2b671f4e3d538d26f28abd3b83a01ce57eea43752.sig");
        
        var variables = new CalamariVariables
        {
           { AwsSpecialVariables.Ecs.Deploy.StackName, "test-stack" },
            { AwsSpecialVariables.Ecs.ClusterName, "TestCluster" },
            { DeploymentEnvironment.Id, "Environment-1" },
            { AwsSpecialVariables.Ecs.Deploy.ServiceTaskName, "test-multi-container-template" },
            { AwsSpecialVariables.Ecs.Deploy.Cpu, "256" },
            { AwsSpecialVariables.Ecs.Deploy.Memory, "512" },
            { AwsSpecialVariables.Ecs.Deploy.RuntimeArchitecturePlatform, "X86_64" },
            { AwsSpecialVariables.Ecs.Deploy.DesiredCount, "2" },
            { AwsSpecialVariables.Ecs.Deploy.MinimumHealthPercent, "100" },
            { AwsSpecialVariables.Ecs.Deploy.MaximumHealthPercent, "200" },
            { AwsSpecialVariables.Ecs.Deploy.AutoAssignPublicIp, "True" },
            { AwsSpecialVariables.Ecs.Deploy.EnableEcsManagedTags, "True" },
            { AwsSpecialVariables.Ecs.WaitOption, """{"type":"DontWait","timeoutMinutes":"30"}""" },
            {
                AwsSpecialVariables.Ecs.Deploy.SecurityGroupIds, """
                                                                 ["sg-0d5e06a4bde84daaa"]
                                                                 """
            },
            {
                AwsSpecialVariables.Ecs.Deploy.SubnetIds, """
                                                          ["subnet-0650cd8a2119e8aaa"]
                                                          """
            },
            { AwsSpecialVariables.Ecs.Deploy.TaskExecutionRole, "arn:aws:iam::120766170633:role/ecsTaskExecutionRole" },
            { AwsSpecialVariables.Ecs.Deploy.TaskRole, "arn:aws:iam::120766170633:role/ecsTaskExecutionRole" },
            
            {AwsSpecialVariables.Ecs.Tags, """[{"key":"my-tag","value":"a great test value"}]"""},
            {AwsSpecialVariables.Ecs.Deploy.Containers, """
                                                        [{"containerName":"web-server","containerImageReference":{"referenceId":"939a08a0-7dd9-471d-ac31-ac8e29eb04ff","imageName":"nginx}","feedId":"Feeds-1001"},"repositoryAuthentication":{"type":"Default"},"memoryLimitSoft":"47","memoryLimitHard":"200","containerPortMappings":[{"containerPort":"80","protocol":"Tcp"},{"containerPort":"443","protocol":"Tcp"}],"cpus":"2","essential":"True","entryPoint":"sh, -c","command":"echo 'Deployment successful","workingDirectory":"/tmp","environmentFiles":["jttestc668db76/test/keyarm-packagev1.0.3.zip"],"environmentVariables":[{"type":"Plain","key":"containerenv","value":"some-otherovalue"}],"networkSettings":{"disableNetworking":"False","dnsServers":[],"dnsSearchDomains":[],"extraHosts":[]},"containerStorage":{"readOnlyRootFileSystem":"True","mountPoints":[{"sourceVolume":"efs-volume","containerPath":"/etc","readonly":"False"}],"volumeFrom":[{"sourceContainer":"efs-volume","readonly":"True"}]},"containerLogging":{"type":"Auto","logOptions":[]},"firelensConfiguration":{"type":"Enabled","firelensType":"Fluentd","enableEcsLogMetadata":"True","customConfigSource":{"type":"File","filePath":"/home/config"}},"dockerLabels":[{"key":"some-label","value":"label-value"}],"user":"test-user","healthCheck":{"command":["curl -f http://localhost/ || exit 1"],"interval":"240","retries":"7","startPeriod":"179","timeout":"54"},"dependencies":[],"startTimeout":"40","stopTimeout":"60","ulimits":[{"limitName":"core","hardLimit":"12","softLimit":"10"}]},{"containerName":"cache","containerImageReference":{"referenceId":"07c8e308-048f-4289-b25a-86fb901e2824","imageName":"redis","feedId":"Feeds-1001"},"repositoryAuthentication":{"type":"Default"},"containerPortMappings":[],"essential":"True","environmentFiles":[],"environmentVariables":[],"networkSettings":{"disableNetworking":"False","dnsServers":[],"dnsSearchDomains":[],"extraHosts":[]},"containerStorage":{"readOnlyRootFileSystem":"False","mountPoints":[],"volumeFrom":[]},"containerLogging":{"type":"Auto","logOptions":[]},"firelensConfiguration":{"type":"Disabled","enableEcsLogMetadata":""},"dockerLabels":[],"healthCheck":{"command":[" [ \"CMD-SHELL\", \"curl -f http://localhost/ || exit 1\" ]."]},"dependencies":[],"ulimits":[]}]
                                                        """},

            
            {AwsSpecialVariables.Ecs.Deploy.LoadBalancerMappings, "[]"},
            {AwsSpecialVariables.Ecs.Deploy.Volumes, """
                                                     [{"type":"Efs","name":"efs-volume","fileSystemId":"efs-fs-id","accessPointId":"/data","rootDirectory":"/root","encryptionInTransit":"True","efsIamAuthorization":"True"}] 
                                                     """},
            
            {"Octopus.Action[Deploy Amazon ECS Service].Package[nginx].Image", "docker.io/nginx:1.31.1"},
            {"Octopus.Action[Deploy Amazon ECS Service].Package[redis].Image", "docker.io/bitnami/redis:sha256-fd997c4c52c0a0af686e5af2b671f4e3d538d26f28abd3b83a01ce57eea43752.sig"},
            {"Octopus.Project.Slug", "ecs-from-md-instance"},
            {"Octopus.Deployment.Id", "Deployments-622"},
            {"Octopus.Environment.Name", "Dev"}
        };
        
        var resultJson = GenerateTemplateFromVariables(variables);
        JToken.DeepEquals(resultJson, expectedJson).Should().BeTrue();
    }

    [Test]
    public void WithComplexStep_MatchesExpectedSpfOutput()
    {
        var expectedJson = ReadFromFile("complexSpfOutputTemplate.json");
        fakeEcsImageResolver.ResolveImageName(Arg.Any<ContainerImageReference>(), Arg.Any<IVariables>()).Returns("index.docker.io/nginx:latest");
        var variables = new CalamariVariables
        {
            {"Octopus.Action.Aws.Ecs.Deploy.CFStackName", "test-stack"},
            {"Octopus.Action.Aws.Ecs.Deploy.DesiredCount","2"},
            {"Octopus.Action.Aws.Ecs.Deploy.MinimumHealthPercent","150"},
            {"Octopus.Action.Aws.Ecs.Deploy.MaximumHealthPercent","300"},
            {"Octopus.Action.Aws.Ecs.Deploy.Cpu","256"},
            {"Octopus.Action.Aws.Ecs.Deploy.Memory","512"},
            {"Octopus.Action.Aws.Ecs.Deploy.RuntimeArchitecturePlatform","X86_64"},
            {"Octopus.Action.Aws.Ecs.Deploy.AutoAssignPublicIp","True"},
            {"Octopus.Action.Aws.Ecs.Deploy.EnableEcsManagedTags","True"},
            {"Octopus.Action.Aws.Ecs.Deploy.ServiceTaskName","test-big-cf-template"},
            {"Octopus.Action.Aws.Ecs.Deploy.TaskRole","arn:aws:iam::120766170633:role/ecsTaskExecutionRole"},
            {"Octopus.Action.Aws.Ecs.Deploy.TaskExecutionRole","arn:aws:iam::120766170633:role/ecsTaskExecutionRole"},
            {"Octopus.Action.Aws.Ecs.Deploy.SubnetIds","[\"subnet-0650cd8a2119e8xxx\"]"},
            {"Octopus.Action.Aws.Ecs.Deploy.SecurityGroupIds","[\"sg-0d5e06a4bde84dxxx\"]"},
            {"Octopus.Action.Aws.Ecs.Deploy.LoadBalancerMappings","[]"},
            {"Octopus.Action.Aws.Ecs.Deploy.Volumes","[{\"type\":\"Efs\",\"name\":\"efs-volume\",\"fileSystemId\":\"efs-fs-id\",\"accessPointId\":\"/data\",\"rootDirectory\":\"/root\",\"encryptionInTransit\":\"True\",\"efsIamAuthorization\":\"True\"}]"},
            {"Octopus.Action.Aws.Ecs.Tags","[{\"key\":\"my-tag\",\"value\":\"a great test value\"}]"},
            {"Octopus.Action.Aws.Ecs.WaitOption", "{\"type\":\"DontWait\",\"timeoutMinutes\":\"30\"}"},
            {"Octopus.Action.Aws.Ecs.Deploy.Containers", """[{"containerName":"web-server","containerImageReference":{"referenceId":"939a08a0-7dd9-471d-ac31-ac8e29eb04ff","imageName":"#{Octopus.Action[Deploy Amazon ECS Service - clone (1)].Package[nginx].Image}","feedId":"Feeds-1061"},"repositoryAuthentication":{"type":"Default"},"memoryLimitSoft":"47","memoryLimitHard":"200","containerPortMappings":[{"containerPort":"80","protocol":"Tcp"},{"containerPort":"443","protocol":"Tcp"}],"cpus":"2","essential":"True","entryPoint":"sh, -c","command":"echo 'Deployment successful","workingDirectory":"/tmp","environmentFiles":["jttestc668db76/test/keyarm-packagev1.0.3.zip"],"environmentVariables":[{"type":"Plain","key":"containerenv","value":"some-otherovalue"}],"networkSettings":{"disableNetworking":"False","dnsServers":[],"dnsSearchDomains":[],"extraHosts":[]},"containerStorage":{"readOnlyRootFileSystem":"True","mountPoints":[{"sourceVolume":"efs-volume","containerPath":"/etc","readonly":"False"}],"volumeFrom":[{"sourceContainer":"efs-volume","readonly":"True"}]},"containerLogging":{"type":"Auto","logOptions":[]},"firelensConfiguration":{"type":"Enabled","firelensType":"Fluentd","enableEcsLogMetadata":"True","customConfigSource":{"type":"File","filePath":"/home/config"}},"dockerLabels":[{"key":"some-label","value":"label-value"}],"user":"test-user","healthCheck":{"command":["curl -f http://localhost/ || exit 1"],"interval":"240","retries":"7","startPeriod":"179","timeout":"54"},"dependencies":[],"startTimeout":"40","stopTimeout":"60","ulimits":[{"limitName":"core","hardLimit":"12","softLimit":"10"}]}] """},
        };
        
        var resultJson = GenerateTemplateFromVariables(variables);
        JToken.DeepEquals(resultJson, expectedJson).Should().BeTrue();
    }
    JObject GenerateTemplateFromVariables(CalamariVariables variables)
    {
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeEcsImageResolver, fakeLog);
        var template = new EcsDeployTemplateGenerator(inputs).Generate();
        var resultJson =  JObject.Parse(template.Body);

        return resultJson;
    }
    

    JObject ReadFromFile(string fileName)
    {
      var filePath = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().FullLocalPath()) ?? "",
        Path.Combine("AWS", "Ecs", "SpfOutputs", fileName));
      
      var fullPath = Path.GetFullPath(filePath);
      if (!File.Exists(fullPath))
      {
        throw new FileNotFoundException($"Test data file not found: {fullPath}");
      }

      var json = File.ReadAllText(fullPath);
      
      return JObject.Parse(json);
    }
}