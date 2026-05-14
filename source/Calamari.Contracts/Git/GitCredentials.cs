namespace Octopus.Calamari.Contracts.Git;

public interface IGitCredentialDto
{
    string Type { get; }
    string Url { get; }
    string Name { get; }
}

// UsernamePasswordGitCredentialDto - could rename, but not worth altering the API
public record GitUsernameAndPasswordCredentialDto(string Name, string Url, string Username, string Password) : IGitCredentialDto
{
    public const string DiscriminatorValue = "UsernamePassword";
    public string Type => DiscriminatorValue;
}

public record GitSshKeyAndKnownHostsDto(string Name, string Url)
{
    public const string DiscriminatorValue = "SSHKeyAndKnownHosts";
    public string Type => DiscriminatorValue;
}