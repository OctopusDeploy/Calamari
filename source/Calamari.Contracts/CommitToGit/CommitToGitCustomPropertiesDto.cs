
using Octopus.Calamari.Contracts.Git;

namespace Octopus.Calamari.Contracts.CommitToGit;

public record CommitToGitCustomPropertiesDto(IGitCredentialDto GitCredential);
