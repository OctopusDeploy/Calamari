namespace Calamari.AiAgent
{
    public static class SpecialVariables
    {
        public static class Action
        {
            public static class AiAgent
            {
                public const string Prompt = "Octopus.Action.AiAgent.Prompt";
                public const string ApiToken = "Octopus.Action.AiAgent.ApiToken";
                public const string Model = "Octopus.Action.AiAgent.Model";
                public const string Response = "Octopus.Action.AiAgent.Response";
                public const string GitHubToken = "Octopus.Action.AiAgent.GitHubToken";
                public const string SystemSkill = "Octopus.Action.AiAgent.SystemSkill";
                public const string Provider = "Octopus.Action.AiAgent.Provider";
                public const string MaxTokens = "Octopus.Action.AiAgent.MaxTokens";
                public const string OctopusToken = "Octopus.Action.AiAgent.OctopusToken";
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
