using Calamari.Aws.Discovery;
using Calamari.Common.Plumbing.Logging;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.AWS.Discovery;

[TestFixture]
public class AwsTargetDiscoveryContextResolverTests
{
    readonly AwsTargetDiscoveryContextResolver sut =  new();
    
    readonly ILog fakeLog = Substitute.For<ILog>();
    

    [TestCase("")]
    [TestCase("Nothing to see here")]
    public void TryResolve_WithInvalidJsonInput_ReturnsFalse(string inputJson)
    {
       var result = sut.TryResolve(inputJson, fakeLog, out _);
       
       result.Should().BeFalse();
    }
    
    [Test]
    public void TryResolve_WithMissingAuthentication_ReturnsFalse()
    {

        const string inputJson = """
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
        
        var result = sut.TryResolve(inputJson, fakeLog, out _);
        
        result.Should().BeFalse();
    }
    
    [Test]
    public void TryResolve_WithMissingScope_ReturnsFalse()
    {
        const string inputJson = """
                        {
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
        
        var result = sut.TryResolve(inputJson, fakeLog, out _);
        
        result.Should().BeFalse();
    }

    [Test]
    public void TryResolve_WithValidContext_ReturnsTrue_AND_PopulatesObject()
    {
        var inputJson = """
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

        var result = sut.TryResolve(inputJson, fakeLog, out var context);
        
        result.Should().BeTrue();
        context.Should().NotBeNull();
        
        context.Scope.Should().NotBeNull();
        context.Scope.SpaceName.Should().Be("Default");
        context.Scope.EnvironmentName.Should().Be("Dev");
        context.Scope.ProjectName.Should().Be("Deprecated-SPF");
        
        context.Authentication.Should().NotBeNull();
        context.Authentication.AccountId.Should().Be("Accounts-1");
        context.Authentication.AuthenticationMethod.Should().Be("account");
        context.Authentication.Role.Type.Should().Be("noAssumedRole");
        context.Authentication.Regions.Should().Contain("ap-southeast-2");
    }
}
