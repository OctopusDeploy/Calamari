using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Octopus.Calamari.Contracts.ClaudeCode;

namespace Calamari.AiAgent.ClaudeCodeBehaviour.InjectionCheck;

public class PromptInjectionGuard
{
    const string SystemPromptResource = "Calamari.AiAgent.ClaudeCodeBehaviour.DefaultContext.injection-check-system-prompt.md";

    readonly ILog log;
    readonly InjectionCheckOptions options;
    readonly ClaudeCodeUsageReporter usageReporter;

    public PromptInjectionGuard(ILog log, InjectionCheckOptions options, ClaudeCodeUsageReporter usageReporter)
    {
        this.log = log;
        this.options = options;
        this.usageReporter = usageReporter;
    }

    public async Task CheckAsync(string workingDir, string prompt, string apiToken, CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            log.Info($"Prompt-injection check is disabled ('{SpecialVariables.Action.Claude.InjectionCheckEnabled}' is false); skipping.");
            return;
        }

        log.Info($"Running prompt-injection check against the execution context using model '{options.Model}'.");

        var systemPrompt = ReadSystemPrompt();

        var context = new ExecutionContextCollector(options.MaxInputCharacters, log).Collect(workingDir, prompt);
        InjectionCheckResult result;
        try
        {
            result = await new AnthropicInjectionCheckClient(options).AnalyzeAsync(systemPrompt, context, apiToken, cancellationToken);
        }
        catch (Exception ex) when (options.FailOpenOnError)
        {
            log.Warn($"Prompt-injection check could not run ({ex.Message}); continuing without it.");
            return;
        }
        catch (Exception ex)
        {
            throw new CommandException($"Prompt-injection check failed to run: {ex.Message}");
        }

        ReportUsage(result);

        if (!result.Verdict.InjectionDetected)
        {
            log.Info("Prompt-injection check passed: no injection detected in the execution context.");
            return;
        }

        var summary = FormatFindings(result.Verdict);
        switch (options.OnDetection)
        {
            case InjectionCheckAction.Warn:
                log.Warn($"Prompt-injection check flagged the execution context:{Environment.NewLine}{summary}");
                break;
            default:
                throw new CommandException($"Prompt-injection check blocked this step. Suspicious content was detected in the execution context:{Environment.NewLine}{summary}");
        }
    }

    void ReportUsage(InjectionCheckResult result)
    {
        usageReporter.AddModelUsage(new[]
        {
            new ClaudeCodeModelUsage
            {
                Model = result.Model,
                InputTokens = result.InputTokens,
                OutputTokens = result.OutputTokens,
            },
        });
    }

    static string FormatFindings(InjectionVerdict verdict)
    {
        if (verdict.Findings == null || verdict.Findings.Count == 0)
            return "(no details provided)";

        return string.Join(Environment.NewLine, verdict.Findings.Select(f => $"- [{f.Severity}] {f.Source}: {f.Description}"));
    }

    static string ReadSystemPrompt()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(SystemPromptResource)
                           ?? throw new Exception("Could not find expected injection-check system prompt embedded resource.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
