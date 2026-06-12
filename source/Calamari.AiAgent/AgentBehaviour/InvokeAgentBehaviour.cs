using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OpenAI.Chat;
//using Anthropic;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Calamari.AiAgent.AgentBehaviour
{
    public class InvokeAgentBehaviour : IDeployBehaviour
    {
        readonly ILog log;

        public InvokeAgentBehaviour(ILog log)
        {
            this.log = log;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return false;
            //var provider = context.Variables.Get(SpecialVariables.Action.AiAgent.Provider);
            //return provider == "Anthropic" || provider == "OpenAI";
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
                model = "gpt-4o";

            log.Info($"Invoking AI agent with model '{model}'...");

            var provider = "Anthropic";//variables.Get(SpecialVariables.Action.AiAgent.Provider);
            IChatClient chatClient;
            if (provider == "Anthropic")
            {
                chatClient = new Anthropic.AnthropicClient { ApiKey = apiToken }
                             .AsIChatClient(model)
                             .AsBuilder()
                             .UseFunctionInvocation()
                             .Build();
                
            }
            else if (provider == "OpenAI")
            {
               chatClient = new ChatClient(model, apiToken)
                            .AsIChatClient()
                            .AsBuilder()
                            .UseFunctionInvocation()
                            .Build();
            }
            else
            {
                throw new Exception($"Provider {provider} not supported");
            }


            var tools = new List<AITool>();
            McpClient? mcpClient = null;

            var githubToken = variables.Get("Octopus.Action.Claude.GitHubToken");
            if (!string.IsNullOrWhiteSpace(githubToken))
            {
                log.Info("Connecting to GitHub MCP server...");
                mcpClient = await McpClient.CreateAsync(
                    new StdioClientTransport(new StdioClientTransportOptions
                    {
                        Command = "npx",
                        Arguments = ["-y", "@modelcontextprotocol/server-github"],
                        Name = "GitHub",
                        EnvironmentVariables = new Dictionary<string, string?>
                        {
                            ["GITHUB_PERSONAL_ACCESS_TOKEN"] = githubToken,
                            ["PATH"] = Environment.GetEnvironmentVariable("PATH"),
                        },
                    }));
                var mcpTools = await mcpClient.ListToolsAsync();
                tools.AddRange(mcpTools);
                log.Info($"GitHub MCP server connected. {tools.Count} tools available.");
            }

            var octopusToken = variables.Get(SpecialVariables.Action.AiAgent.OctopusToken);
            if (!string.IsNullOrWhiteSpace(octopusToken))
            {
                var mcpClient2 = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions()
                {
                    Command = "npx",
                    Arguments = ["-y", "@octopusdeploy/mcp-server", ],
                    Name = "Octopus",
                    EnvironmentVariables = new Dictionary<string, string?>
                    {
                        ["OCTOPUS_SERVER_URL"] ="http://localhost:8065",
                        ["OCTOPUS_API_KEY"] = octopusToken,
                        ["PATH"] = Environment.GetEnvironmentVariable("PATH"),
                    }
                }));
                var mcpTools2 = await mcpClient2.ListToolsAsync();
                tools.AddRange(mcpTools2);
                log.Info($"Octopus MCP server connected. {tools.Count} tools available.");
                
            }

            tools.Add(AIFunctionFactory.Create(() =>
                {
                    var sensitiveKeywords = new[] { "password", "secret", "token", "apikey", "api_key", "api-key", "private" };
                    var filtered = variables
                        .Where(kvp => !sensitiveKeywords.Any(k => kvp.Key.Contains(k, StringComparison.OrdinalIgnoreCase)))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    return JsonSerializer.Serialize(filtered, new JsonSerializerOptions { WriteIndented = true });
                },
                "get_deployment_variables",
                "Returns all Octopus deployment variables as JSON (sensitive values are excluded). "
                + "Call this when you need to inspect the current deployment context such as environment, project, tenant, release version, or any custom variables."));

            try
            {
                var responseBuilder = new StringBuilder();
                var lineBuffer = new LineBuffer(line => log.Info(line));
                List<ChatMessage> chatHistory = [];

                var systemPrompt = string.Empty; //variables.Get(SpecialVariables.Action.AiAgent.SystemSkill);
                if (!string.IsNullOrWhiteSpace(systemPrompt))
                {
                    chatHistory.Add(new ChatMessage(ChatRole.System, systemPrompt));
                }

                var inputCostPerMillion = 3;
                var outputCostPerMillion = 15;

                var msg = new ChatMessage(ChatRole.User, prompt);
                chatHistory.Add(msg);

                var maxTokens = 10000;
                var chatOptions = new ChatOptions()
                {
                    MaxOutputTokens = maxTokens, Tools = [.. tools]
                };

                await foreach (var update in chatClient.GetStreamingResponseAsync(chatHistory, chatOptions))
                {
                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        responseBuilder.Append(update.Text);
                        lineBuffer.Append(update.Text);
                    }

                    var usage = update.Contents.OfType<UsageContent>().FirstOrDefault();
                    if (usage is not null)
                    {
#pragma warning disable MEAI001
                        var inputCost = Math.Round((double)(usage.Details.InputTokenCount.HasValue ? (usage.Details.InputTokenCount! / 1000000.0 * inputCostPerMillion) : 0), 4);
                        var outputCost = Math.Round((double)(usage.Details.OutputTokenCount.HasValue ? (usage.Details.OutputTokenCount / 1000000.0 * outputCostPerMillion) : 0), 4);
                        log.VerboseFormat($"Input cost: ${inputCost}, Output cost: ${outputCost}, Total cost: ${inputCost + outputCost}");
#pragma warning restore MEAI001
                    }

                    chatHistory.AddMessages(update);
                }

                lineBuffer.Flush();

                var fullResponse = responseBuilder.ToString();
                Log.SetOutputVariable(SpecialVariables.Action.AiAgent.Response, fullResponse, variables);
                log.Info("AI agent invocation complete.");
            }
            finally
            {
                if (mcpClient is not null)
                {
                    await mcpClient.DisposeAsync();
                }
            }
        }
    }
}
