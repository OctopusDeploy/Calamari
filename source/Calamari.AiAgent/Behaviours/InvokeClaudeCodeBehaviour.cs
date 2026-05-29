using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AiAgent.Behaviours
{
    public class InvokeClaudeCodeBehaviour : IDeployBehaviour
    {
        readonly ILog log;

        public InvokeClaudeCodeBehaviour(ILog log)
        {
            this.log = log;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
            //var provider = context.Variables.Get(SpecialVariables.Action.AiAgent.Provider);
            //return provider == "ClaudeCode";
        }

        public async Task Execute(RunningDeployment context)
        {
            var variables = context.Variables;

            var prompt = variables.Get(SpecialVariables.Action.AiAgent.Prompt);
            if (string.IsNullOrWhiteSpace(prompt))
                throw new CommandException($"Variable '{SpecialVariables.Action.AiAgent.Prompt}' is required but was not provided.");

            var apiToken = variables.Get(SpecialVariables.Action.AiAgent.ApiToken);
            if (string.IsNullOrWhiteSpace(apiToken))
                throw new CommandException($"Variable '{SpecialVariables.Action.AiAgent.ApiToken}' is required but was not provided.");

            var model = variables.Get(SpecialVariables.Action.AiAgent.Model);
            if (string.IsNullOrWhiteSpace(model))
                model = "claude-sonnet-4-20250514";

            log.Info($"Invoking Claude Code CLI with model '{model}'...");

            var runner = new ClaudeCodeCliRunner(log);
            var response = await runner.RunAsync(new ClaudeCodeOptions
            {
                Prompt = prompt,
                ApiToken = apiToken,
                Model = model,
                SystemPrompt = variables.Get(SpecialVariables.Action.AiAgent.SystemSkill),
                MaxTurns = variables.GetInt32(SpecialVariables.Action.AiAgent.MaxTokens),
            });

            Log.SetOutputVariable(SpecialVariables.Action.AiAgent.Response, response, variables);
            log.Info("Claude Code invocation complete.");
        }
    }
}
