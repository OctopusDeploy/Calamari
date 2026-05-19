namespace Octopus.Calamari.Contracts.Git;

public interface IGitCredentialDto
{
    string Type { get; }
    string Url { get; }
    string Name { get; }
}

public record UsernamePasswordGitCredentialDto(string Name, string Url, string Username, string Password) : IGitCredentialDto
{
    public const string DiscriminatorValue = "UsernamePassword";
    public string Type => DiscriminatorValue;
}

public record SshKeyGitCredentialDto(string Name, string Url, string Username, string PrivateKey) : IGitCredentialDto
{
    public const string DiscriminatorValue = "SshKey";
    public string Type => DiscriminatorValue;
}