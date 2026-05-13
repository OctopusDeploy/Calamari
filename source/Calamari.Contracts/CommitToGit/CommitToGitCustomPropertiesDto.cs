namespace Octopus.Calamari.Contracts.CommitToGit;

public record CommitToGitCustomPropertiesDto(GitCredentialDto GitCredential);

public record GitCredentialDto(
    string CredentialName,
    string RepositoryUrl,
    string Username,
    string Password
);