using System;
using System.Collections.Generic;
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

            var mcpServers = BuildMcpServers(variables);
            var runAs = BuildRunAs(variables);

            var runner = new ClaudeCodeCliRunner(log);
            var response = await runner.RunAsync(new ClaudeCodeOptions
            {
                Prompt = prompt,
                ApiToken = apiToken,
                Model = model,
                SystemPrompt = variables.Get(SpecialVariables.Action.AiAgent.SystemSkill),
                MaxTurns = variables.GetInt32(SpecialVariables.Action.AiAgent.MaxTokens),
                McpServers = mcpServers,
                RunAs = runAs,
            });

            Log.SetOutputVariable(SpecialVariables.Action.AiAgent.Response, response, variables);
            log.Info("Claude Code invocation complete.");
        }

        static Dictionary<string, McpServerConfig> BuildMcpServers(IVariables variables)
        {
            var servers = new Dictionary<string, McpServerConfig>();

            var githubToken = variables.Get(SpecialVariables.Action.AiAgent.GitHubToken);
            if (!string.IsNullOrWhiteSpace(githubToken))
            {
                servers["github"] = new McpServerConfig
                {
                    Command = "npx",
                    Args = new[] { "-y", "@modelcontextprotocol/server-github" },
                    Env = new Dictionary<string, string>
                    {
                        ["GITHUB_PERSONAL_ACCESS_TOKEN"] = githubToken,
                        ["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? "",
                    },
                };
            }

            var octopusToken = variables.Get(SpecialVariables.Action.AiAgent.OctopusToken);
            if (!string.IsNullOrWhiteSpace(octopusToken))
            {
                servers["octopus"] = new McpServerConfig
                {
                    Command = "npx",
                    Args = new[] { "-y", "@octopusdeploy/mcp-server" },
                    Env = new Dictionary<string, string>
                    {
                        ["OCTOPUS_SERVER_URL"] = "http://localhost:8065",
                        ["OCTOPUS_API_KEY"] = octopusToken,
                        ["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? "",
                    },
                };
            }

            return servers;
        }

        static ProcessCredentials? BuildRunAs(IVariables variables)
        {
            var username = variables.Get(SpecialVariables.Action.AiAgent.RunAsUsername);
            if (string.IsNullOrWhiteSpace(username))
                return null;

            return new ProcessCredentials
            {
                Username = username,
                Password = variables.Get(SpecialVariables.Action.AiAgent.RunAsPassword),
            };
        }
    }
}
