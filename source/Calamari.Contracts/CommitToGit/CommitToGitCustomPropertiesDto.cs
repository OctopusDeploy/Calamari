namespace Octopus.Calamari.Contracts.CommitToGit;

public record CommitToGitCustomPropertiesDto(NamedGitCredentialDto Credential);

public record NamedGitCredentialDto(string Username, string Password, string Name);
