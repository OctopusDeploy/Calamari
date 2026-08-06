using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Calamari.AiAgent.ClaudeCodeBehaviour.InjectionCheck;

public class AnthropicInjectionCheckClient
{
    const string AnthropicVersion = "2023-06-01";
    const string DefaultBaseUrl = "https://api.anthropic.com";

    static readonly JsonSerializerOptions ResponseJsonOptions = new() { PropertyNameCaseInsensitive = true };

    readonly InjectionCheckOptions options;

    public AnthropicInjectionCheckClient(InjectionCheckOptions options)
    {
        this.options = options;
    }

    public async Task<InjectionCheckResult> AnalyzeAsync(string systemPrompt, string context, string apiToken, CancellationToken cancellationToken)
    {
        var requestBody = JsonSerializer.Serialize(new
        {
            model = options.Model,
            max_tokens = options.MaxTokens,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = context } },
            output_config = new { format = new { type = "json_schema", schema = VerdictSchema } },
        });

        using var handler = new HttpClientHandler();
        // follow whatever proxy ProxyInitializer configured on the static default at startup
        var proxy = WebRequest.DefaultWebProxy;
        if (proxy != null)
        {
            handler.Proxy = proxy;
            handler.UseProxy = true;
        }

        using var client = new HttpClient(handler) { Timeout = options.RequestTimeout };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl()}/v1/messages")
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-api-key", apiToken);
        request.Headers.Add("anthropic-version", AnthropicVersion);

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InjectionCheckException($"Anthropic API returned {(int)response.StatusCode} {response.StatusCode}: {responseBody}");

        var message = JsonSerializer.Deserialize<MessagesResponse>(responseBody, ResponseJsonOptions)
                      ?? throw new InjectionCheckException("Anthropic API returned an empty response.");

        if (string.Equals(message.StopReason, "refusal", StringComparison.OrdinalIgnoreCase))
            throw new InjectionCheckException("The injection-check model refused the request.");

        var verdictJson = message.Content?.FirstOrDefault(b => b.Type == "text")?.Text;
        if (string.IsNullOrWhiteSpace(verdictJson))
            throw new InjectionCheckException("The injection-check model returned no verdict content.");

        var verdict = JsonSerializer.Deserialize<InjectionVerdict>(verdictJson, ResponseJsonOptions)
                      ?? throw new InjectionCheckException("Could not parse the injection-check verdict.");

        return new InjectionCheckResult
        {
            Verdict = verdict,
            Model = message.Model ?? options.Model,
            InputTokens = message.Usage?.InputTokens,
            OutputTokens = message.Usage?.OutputTokens,
        };
    }

    static string BaseUrl()
    {
        var baseUrl = Environment.GetEnvironmentVariable("ANTHROPIC_BASE_URL");
        return string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');
    }

    static readonly object VerdictSchema = new
    {
        type = "object",
        properties = new
        {
            injectionDetected = new { type = "boolean" },
            findings = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        source = new { type = "string" },
                        severity = new { type = "string" },
                        description = new { type = "string" },
                    },
                    required = new[] { "source", "severity", "description" },
                    additionalProperties = false,
                },
            },
        },
        required = new[] { "injectionDetected", "findings" },
        additionalProperties = false,
    };
}
