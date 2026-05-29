namespace Calamari.AiAgent
{
    public static class SpecialVariables
    {
        public static class Action
        {
            public static class AiAgent
            {
                public const string Prompt = "Octopus.Action.Claude.Prompt";
                public const string ApiToken = "Octopus.Action.Claude.ApiToken";
                public const string Model = "Octopus.Action.Claude.Model";
                public const string Response = "Octopus.Action.Claude.Response";
                public const string GitHubToken = "Octopus.Action.Claude.GitHubToken";
                public const string SystemSkill = "Octopus.Action.Claude.SystemSkill";
                public const string MaxTokens = "Octopus.Action.Claude.MaxTokens";
                public const string OctopusToken = "Octopus.Action.Claude.OctopusToken";
                public const string RunAsUsername = "Octopus.Action.Claude.RunAsUsername";
                public const string RunAsPassword = "Octopus.Action.Claude.RunAsPassword";
            }
        }
    }

    public static class AiAgentServiceMessageNames
    {
        public const string Name = "ai-agent-usage";

        public const string CostUsdAttribute = "costUsd";
        public const string TotalCostUsdAttribute = "totalCostUsd";
        public const string DurationMsAttribute = "durationMs";
        public const string DurationApiMsAttribute = "durationApiMs";
        public const string NumTurnsAttribute = "numTurns";
        public const string InputTokensAttribute = "inputTokens";
        public const string OutputTokensAttribute = "outputTokens";
        public const string CacheReadInputTokensAttribute = "cacheReadInputTokens";
        public const string CacheCreationInputTokensAttribute = "cacheCreationInputTokens";
        public const string ModelAttribute = "model";
        public const string ProviderAttribute = "provider";
    }
}
