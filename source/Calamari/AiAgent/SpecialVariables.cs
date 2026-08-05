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
                public const string ApiKey = "Octopus.Action.Claude.ApiKey";
                public const string Model = "Octopus.Action.Claude.Model";
                public const string Response = "Octopus.Action.Claude.Response";
                public const string McpServers = "Octopus.Action.Claude.McpServers";
                public const string MaxTurns = "Octopus.Action.Claude.MaxTurns";
                public const string MaxBudgetUsd = "Octopus.Action.Claude.MaxBudgetUsd";
                public const string MaxArtifactSizeInMegaBytes = "Octopus.Action.Claude.MaxArtifactSizeInMegaBytes";
                public const string OctopusToken = "Octopus.Action.Claude.OctopusToken";
                public const string OctopusMcpApiKey = "Octopus.Action.Claude.OctopusMcpApiKey";
                public const string OctopusMcpTools = "Octopus.Action.Claude.OctopusMcpTools";
                public const string Permissions = "Octopus.Action.Claude.Permissions";
                public const string PermissionMode = "Octopus.Action.Claude.PermissionMode";
                public const string Effort = "Octopus.Action.Claude.Effort";
                public const string PassEnvironmentVariables = "Octopus.Action.Claude.PassEnvironmentVariables";

                public const string SandboxMode = "Octopus.Action.Claude.SandboxMode";
                public const string SandboxSettings = "Octopus.Action.Claude.SandboxSettings";

                public const string Skills = "Octopus.Action.Claude.Skills";
                public const string SkillName = "Name";
                public const string SkillContent = "Content";

                public const string InjectionCheckEnabled = "Octopus.Action.Claude.InjectionCheckEnabled";
                public const string InjectionCheckModel = "Octopus.Action.Claude.InjectionCheckModel";
                public const string InjectionCheckMaxTokens = "Octopus.Action.Claude.InjectionCheckMaxTokens";
                public const string InjectionCheckMaxInputCharacters = "Octopus.Action.Claude.InjectionCheckMaxInputCharacters";
                public const string InjectionCheckOnDetection = "Octopus.Action.Claude.InjectionCheckOnDetection";
                public const string InjectionCheckFailOpenOnError = "Octopus.Action.Claude.InjectionCheckFailOpenOnError";
            }
        }
    }
}
