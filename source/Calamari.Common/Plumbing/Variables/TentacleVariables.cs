using System;

namespace Calamari.Common.Plumbing.Variables
{
    public static class TentacleVariables
    {
        public static class CurrentDeployment
        {
            public static readonly string PackageFilePath = "Octopus.Tentacle.CurrentDeployment.PackageFilePath";
        }

        public static class Agent
        {
            public static readonly string InstanceName = "Octopus.Tentacle.Agent.InstanceName";
            public static readonly string ApplicationDirectoryPath = "Octopus.Tentacle.Agent.ApplicationDirectoryPath";
            public static readonly string JournalPath = "env:TentacleJournal";
            public static readonly string TentacleHome = "env:TentacleHome";
        }

        public static class PreviousInstallation
        {
            public static readonly string PackageVersion = "Octopus.Tentacle.PreviousInstallation.PackageVersion";
            public static readonly string PackageFilePath = "Octopus.Tentacle.PreviousInstallation.PackageFilePath";
            public static readonly string OriginalInstalledPath = "Octopus.Tentacle.PreviousInstallation.OriginalInstalledPath";
            public static readonly string CustomInstallationDirectory = "Octopus.Tentacle.PreviousInstallation.CustomInstallationDirectory";
        }

        public static class PreviousSuccessfulInstallation
        {
            public static readonly string PackageVersion = "Octopus.Tentacle.PreviousSuccessfulInstallation.PackageVersion";
            public static readonly string PackageFilePath = "Octopus.Tentacle.PreviousSuccessfulInstallation.PackageFilePath";
            public static readonly string OriginalInstalledPath = "Octopus.Tentacle.PreviousSuccessfulInstallation.OriginalInstalledPath";
            public static readonly string CustomInstallationDirectory = "Octopus.Tentacle.PreviousSuccessfulInstallation.CustomInstallationDirectory";
        }
    }
}