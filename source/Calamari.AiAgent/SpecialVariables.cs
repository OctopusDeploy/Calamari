namespace Calamari.AiAgent
{
    public static class SpecialVariables
    {
        public static class Web
        {

            public const string ServerUri = "Octopus.Web.ServerUri";
        }

        public static class Action
        {
            public static class Claude
            {
                public const string Prompt = "Octopus.Action.Claude.Prompt";
                public const string ApiToken = "Octopus.Action.Claude.ApiToken";
                public const string Model = "Octopus.Action.Claude.Model";
                public const string Response = "Octopus.Action.Claude.Response";
                public const string McpServers = "Octopus.Action.Claude.McpServers";
                public const string MaxTurns = "Octopus.Action.Claude.MaxTurns";
                public const string MaxBudgetUsd = "Octopus.Action.Claude.MaxBudgetUsd";
                public const string OctopusToken = "Octopus.Action.Claude.OctopusToken";
                public const string AllowedTools = "Octopus.Action.Claude.AllowedTools";
                public const string Effort = "Octopus.Action.Claude.Effort";
                public const string PassEnvironmentVariables = "Octopus.Action.Claude.PassEnvironmentVariables";
                public const string RunAsUsername = "Octopus.Action.Claude.RunAsUsername";
                public const string RunAsPassword = "Octopus.Action.Claude.RunAsPassword";

                public const string SandboxMode = "Octopus.Action.Claude.SandboxMode";

                public const string BashNetworkAllowedDomains = "Octopus.Action.Claude.Bash.NetworkAllowedDomains";
                public const string BashNetworkDeniedDomains = "Octopus.Action.Claude.Bash.NetworkDeniedDomains";
                public const string BashFilesystemAllowWrite = "Octopus.Action.Claude.Bash.FilesystemAllowWrite";
                public const string BashFilesystemDenyWrite = "Octopus.Action.Claude.Bash.FilesystemDenyWrite";
                public const string BashFilesystemDenyRead = "Octopus.Action.Claude.Bash.FilesystemDenyRead";
                public const string BashFilesystemAllowRead = "Octopus.Action.Claude.Bash.FilesystemAllowRead";
                public const string BashExcludedCommands = "Octopus.Action.Claude.Bash.ExcludedCommands";

                public const string SrtNetworkAllowedDomains = "Octopus.Action.Claude.Srt.NetworkAllowedDomains";
                public const string SrtNetworkDeniedDomains = "Octopus.Action.Claude.Srt.NetworkDeniedDomains";
                public const string SrtFilesystemAllowWrite = "Octopus.Action.Claude.Srt.FilesystemAllowWrite";
                public const string SrtFilesystemDenyWrite = "Octopus.Action.Claude.Srt.FilesystemDenyWrite";
                public const string SrtFilesystemDenyRead = "Octopus.Action.Claude.Srt.FilesystemDenyRead";
                public const string SrtFilesystemAllowRead = "Octopus.Action.Claude.Srt.FilesystemAllowRead";

                public const string Skills = "Octopus.Action.Claude.Skills";
                public const string SkillName = "Name";
                public const string SkillContent = "Content";
            }
        }
    }
}
