#nullable enable
using Calamari.ArgoCD.Git;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.Tests.ArgoCD.Contracts;

/// <summary>
/// Deserialisation tests for <see cref="ArgoCDCustomPropertiesDto"/> with a focus on
/// the polymorphic <see cref="IGitCredentialDto"/> array. The converter discriminates on
/// the <c>Type</c> field emitted by Octopus Server (the concrete type name); a missing
/// <c>Type</c> defaults to <see cref="GitCredentialDto"/> for backwards compatibility.
/// </summary>
[TestFixture]
public class ArgoCDCustomPropertiesDtoSerializationTests
{
    static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.None,
        DateParseHandling = DateParseHandling.None,
        Converters = { new IGitCredentialDtoJsonConverter() }
    };

    static T DeserializeRaw<T>(string json)
        => JsonConvert.DeserializeObject<T>(json, Settings)!;

    [Test]
    public void TypeGitCredentialDto_DeserializesAsHttpsCredential()
    {
        const string json = """
            {
              "Gateways": [], "Applications": [],
              "Credentials": [
                {
                  "Type": "GitCredentialDto",
                  "Url": "https://github.com/org/repo.git",
                  "Username": "user",
                  "Password": "pass"
                }
              ]
            }
            """;

        var result = DeserializeRaw<ArgoCDCustomPropertiesDto>(json);

        result.Credentials.Should().HaveCount(1);
        var credential = result.Credentials[0].Should().BeOfType<GitCredentialDto>().Subject;
        credential.Url.Should().Be("https://github.com/org/repo.git");
        credential.Username.Should().Be("user");
        credential.Password.Should().Be("pass");
    }

    [Test]
    public void MissingType_DefaultsToHttpsCredential()
    {
        // Backwards compatibility: older server versions don't emit the Type field.
        const string json = """
            {
              "Gateways": [], "Applications": [],
              "Credentials": [
                {
                  "Url": "https://github.com/org/repo.git",
                  "Username": "user",
                  "Password": "pass"
                }
              ]
            }
            """;

        var result = DeserializeRaw<ArgoCDCustomPropertiesDto>(json);

        result.Credentials.Should().HaveCount(1);
        result.Credentials[0].Should().BeOfType<GitCredentialDto>();
    }

    [Test]
    public void UnknownType_Throws()
    {
        const string json = """
            {
              "Gateways": [], "Applications": [],
              "Credentials": [
                { "Type": "MysteryCredentialDto", "Url": "x" }
              ]
            }
            """;

        var act = () => DeserializeRaw<ArgoCDCustomPropertiesDto>(json);

        act.Should().Throw<JsonSerializationException>()
           .WithMessage("*MysteryCredentialDto*");
    }

    [Test]
    public void EmptyCredentialsArray_Deserializes()
    {
        const string json = """
            {
              "Gateways": [], "Applications": [],
              "Credentials": []
            }
            """;

        var result = DeserializeRaw<ArgoCDCustomPropertiesDto>(json);

        result.Credentials.Should().BeEmpty();
    }
}
