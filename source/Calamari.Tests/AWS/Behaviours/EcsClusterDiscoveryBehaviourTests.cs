using System.Collections.Generic;
using System.Linq;
using Amazon.ECS.Model;
using Amazon.Runtime;
using Calamari.Aws.Behaviours;
using Calamari.Aws.Discovery;
using Calamari.Aws.Integration.Ecs;
using Calamari.Common.Commands;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using NSubstitute;
using NUnit.Framework;
using Octopus.Calamari.Contracts.TargetDiscovery;
using Task = System.Threading.Tasks.Task;

namespace Calamari.Tests.AWS.Behaviours;

[TestFixture]
public class EcsClusterDiscoveryBehaviourTests
{
    readonly ILog fakeLog = Substitute.For<ILog>();
    readonly IEcsDiscoverer fakeEcsDiscoverer = Substitute.For<IEcsDiscoverer>();
    readonly IAwsTargetDiscoveryContextResolver fakeContextResolver = Substitute.For<IAwsTargetDiscoveryContextResolver>();
    readonly IEcsClusterDiscoveryWriter fakeWriter = Substitute.For<IEcsClusterDiscoveryWriter>();

    [SetUp]
    public void Setup()
    {
      fakeWriter.ClearReceivedCalls();
      fakeLog.ClearReceivedCalls();
    }

    [Test]
    public async Task Execute_WithoutTargetDiscoveryVariables_ExitsLogsWarning()
    {
        var sut = new EcsClusterDiscoveryBehaviour(fakeEcsDiscoverer, fakeContextResolver, fakeWriter, fakeLog);
        var deployment = new RunningDeployment(new CalamariVariables(), new Dictionary<string, string>());

        await sut.Execute(deployment);

        fakeLog.Received().Warn(Arg.Is<string>(s => s.StartsWith("Could not find target discovery context in variable")));
        fakeLog.Received().Warn("Aborting target discovery.");
    }

    [Test]
    public async Task Execute_WithValidContext_ExitsAndLogsWarning()
    {
        const string missingAuthContext = """
                                          {
                                            "scope": {
                                              "spaceName": "Default",
                                              "environmentName": "Dev",
                                              "projectName": "Full-ECS",
                                              "roles": [
                                                "spf-deprecation"
                                              ]
                                            }
                                          }
                                          """;

        var variables = new CalamariVariables()
        {
            { "Octopus.TargetDiscovery.Context", missingAuthContext }
        };

        // Integrate with actual resolver 
        var sut = new EcsClusterDiscoveryBehaviour(fakeEcsDiscoverer, new AwsTargetDiscoveryContextResolver(), fakeWriter, fakeLog);
        var deployment = new RunningDeployment(variables, new Dictionary<string, string>());

        await sut.Execute(deployment);

        fakeLog.Received().Warn("Aborting target discovery. Could not resolve context.");
    }

    [Test]
    public async Task Execute_WithInvalidCredentials_ExitsAndLogsWarning()
    {
        const string invalidCredentialsInContext = """
                                                   {
                                                     "scope": {
                                                       "spaceName": "Default",
                                                       "environmentName": "Dev",
                                                       "projectName": "Deprecated-SPF",
                                                       "roles": [
                                                         "spf-deprecation"
                                                       ]
                                                     },
                                                     "authentication": {
                                                       "type": "Aws",
                                                       "authenticationMethod": "account",
                                                       "credentials": {
                                                         "type": "account",
                                                         "accountId": "Accounts-1",
                                                         "account": {
                                                           "accessKey": "ABCD0EFGH1IJ2KLMNOP3",
                                                           "seKretKey": "THISISNOTAREALSECRETBUTAPPARENTLYTHATNEEDSTOBESPELLEDOUTTOBRANCHPROTECTION",
                                                           "region": "ap-southeast-2"
                                                         }
                                                       },
                                                       "role": {
                                                         "type": "noAssumedRole"
                                                       },
                                                       "regions": [
                                                         "ap-bahamas-2"
                                                       ]
                                                     }
                                                   }
                                                   """;

        var variables = new CalamariVariables()
        {
            { "Octopus.TargetDiscovery.Context", invalidCredentialsInContext }
        };

        // Integrate with actual resolver 
        var sut = new EcsClusterDiscoveryBehaviour(fakeEcsDiscoverer, new AwsTargetDiscoveryContextResolver(), fakeWriter, fakeLog);
        var deployment = new RunningDeployment(variables, new Dictionary<string, string>());

        await sut.Execute(deployment);

        fakeLog.Received().Warn("Aborting target discovery. Invalid credentials.");
    }

    [Test]
    public async Task Execute_WithValidContext_AndNoMatchingClusters_WritesNoCreateMessages()
    {
        // Arrange
        const string contextJson = """
                                   {
                                     "scope": {
                                       "spaceName": "Default",
                                       "environmentName": "Dev",
                                       "projectName": "Deprecated-SPF",
                                       "roles": [
                                         "spf-deprecation"
                                       ]
                                     },
                                     "authentication": {
                                       "type": "Aws",
                                       "authenticationMethod": "account",
                                       "credentials": {
                                         "type": "account",
                                         "accountId": "Accounts-1",
                                         "account": {
                                           "accessKey": "ABCD0EFGH1IJ2KLMNOP3",
                                           "secretKey": "THISISNOTAREALSECRETBUTAPPARENTLYTHATNEEDSTOBESPELLEDOUTTOBRANCHPROTECTION",
                                           "region": "ap-southeast-2"
                                         }
                                       },
                                       "role": {
                                         "type": "noAssumedRole"
                                       },
                                       "regions": [
                                         "ap-southeast-2"
                                       ]
                                     }
                                   }
                                   """;

        var variables = new CalamariVariables()
        {
            { "Octopus.TargetDiscovery.Context", contextJson }
        };
        var deployment = new RunningDeployment(variables, new Dictionary<string, string>());
        fakeEcsDiscoverer.DiscoverClustersInRegion(Arg.Any<AWSCredentials>(), Arg.Is<string>(s => s.Equals("ap-southeast-2")))
                         .Returns(Task.FromResult<IReadOnlyList<Cluster>>(new List<Cluster>()));

        var sut = new EcsClusterDiscoveryBehaviour(fakeEcsDiscoverer, new AwsTargetDiscoveryContextResolver(), fakeWriter, fakeLog);

        // Act
        await sut.Execute(deployment);

        // Assert
        fakeWriter.DidNotReceive()
                  .WriteTargetCreationServiceMessage(
                      Arg.Any<string>(),
                      Arg.Any<Cluster>(),
                      Arg.Any<IAwsAuthenticationDetails>(),
                      Arg.Any<TargetDiscoveryScope>(),
                      Arg.Any<TargetMatchResult>());
    }

    [Test]
    public async Task Execute_WithValidContext_AndMatchingClusters_WritesNoCreateMessages()
    {
        // Arrange
        const string contextJson = """
                                   {
                                     "scope": {
                                       "spaceName": "Default",
                                       "environmentName": "Dev",
                                       "projectName": "Deprecated-SPF",
                                       "roles": [
                                         "spf-deprecation"
                                       ]
                                     },
                                     "authentication": {
                                       "type": "Aws",
                                       "authenticationMethod": "account",
                                       "credentials": {
                                         "type": "account",
                                         "accountId": "Accounts-1",
                                         "account": {
                                           "accessKey": "ABCD0EFGH1IJ2KLMNOP3",
                                           "secretKey": "THISISNOTAREALSECRETBUTAPPARENTLYTHATNEEDSTOBESPELLEDOUTTOBRANCHPROTECTION",
                                           "region": "ap-southeast-2"
                                         }
                                       },
                                       "role": {
                                         "type": "noAssumedRole"
                                       },
                                       "regions": [
                                         "ap-southeast-2"
                                       ]
                                     }
                                   }
                                   """;

        var matchingClusters = new List<Cluster>
        {
            new()
            {
                ClusterName = "MatchingCluster1",
                Tags =
                [
                    new Tag
                    {
                        Key = TargetTags.RoleTagName,
                        Value = "spf-deprecation"
                    },
                    new Tag
                    {
                        Key = TargetTags.EnvironmentTagName,
                        Value = "Dev"
                    },
                    new Tag
                    {
                      Key = TargetTags.SpaceTagName,
                      Value = "Default"
                    }
                ]
            },
            new()
            {
                ClusterName = "MatchingCluster2",
                Tags =
                [
                  new Tag
                  {
                    Key = TargetTags.RoleTagName,
                    Value = "spf-deprecation"
                  },
                  new Tag
                  {
                    Key = TargetTags.EnvironmentTagName,
                    Value = "Dev"
                  },
                  new Tag
                  {
                    Key = TargetTags.SpaceTagName,
                    Value = "Default"
                  }
                ]
            }
        };

        var nonMatchingClusters = new List<Cluster>
        {
            new()
            {
                ClusterName = "NonMatchingCluster1"
            },
            new()
            {
              ClusterName = "NonMatchingCluster1",
              Tags =
              [
                new Tag
                {
                  Key = TargetTags.RoleTagName,
                  Value = "spf-deprecation"
                },
                new Tag
                {
                  Key = TargetTags.EnvironmentTagName,
                  Value = "Dev"
                },
                new Tag
                {
                  Key = TargetTags.SpaceTagName,
                  Value = "ADifferentSpace"
                }
              ]
            }
        };

        var allClusters = matchingClusters.Concat(nonMatchingClusters).ToList();

        var variables = new CalamariVariables()
        {
            { "Octopus.TargetDiscovery.Context", contextJson }
        };
        var deployment = new RunningDeployment(variables, new Dictionary<string, string>());
        fakeEcsDiscoverer.DiscoverClustersInRegion(Arg.Any<AWSCredentials>(), Arg.Is<string>(s => s.Equals("ap-southeast-2")))
                         .Returns(Task.FromResult<IReadOnlyList<Cluster>>(allClusters));

        var sut = new EcsClusterDiscoveryBehaviour(fakeEcsDiscoverer, new AwsTargetDiscoveryContextResolver(), fakeWriter, fakeLog);

        // Act
        await sut.Execute(deployment);

        // Assert
        fakeWriter.ReceivedWithAnyArgs(2).WriteTargetCreationServiceMessage(null, null, null, null, null);
        
        fakeWriter.Received()
                  .WriteTargetCreationServiceMessage(
                      "ap-southeast-2",
                      Arg.Is<Cluster>(c => c.ClusterName.Equals("MatchingCluster1")),
                      Arg.Is<IAwsAuthenticationDetails>(auth => auth.AccountId == "Accounts-1"),
                      Arg.Is<TargetDiscoveryScope>(s => s.ProjectName.Equals("Deprecated-SPF")),
                      Arg.Is<TargetMatchResult>(m => m.Role.Equals("spf-deprecation") && m.IsSuccess));
    }
}
