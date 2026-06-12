using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public class ClaudeCodeProcessStartInfo
{
    const string ClaudeCodePath = "claude";
    internal static string ShellQuote(string value)
    {
        return "'" + value.Replace("'", @"'\''") + "'";
    }

    
    async Task<Process> StartWindowsProcess(string workingDir,
                                            ProcessCredentials? runAs,
                                            ClaudeCommandArgsBuilder argsBuilder,
                                            Dictionary<string, string> environmentVariables)
    {
        var startInfo = StartSimpleProcess(workingDir, argsBuilder, environmentVariables);
            
        if (runAs != null)
        {
            startInfo.UserName = runAs.Username;
#pragma warning disable CA1416
            if (runAs.Password != null)
                startInfo.PasswordInClearText = runAs.Password;

            if (!string.IsNullOrEmpty(runAs.Domain))
                startInfo.Domain = runAs.Domain;
#pragma warning restore CA1416
        }

        await Task.CompletedTask;

        return Process.Start(startInfo)!;
    }

    static ProcessStartInfo StartSimpleProcess(string workingDir, ClaudeCommandArgsBuilder argsBuilder, Dictionary<string, string> environmentVariables)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ClaudeCodePath,
            Arguments = argsBuilder.Build(),
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var kvp in environmentVariables)
            startInfo.Environment[kvp.Key] = kvp.Value;
        return startInfo;
    }

    public async Task<Process> StartClaudeProcess(string workingDir,
                                                  ProcessCredentials? runAs,
                                                  ClaudeCommandArgsBuilder argsBuilder,
                                                  Dictionary<string, string> environmentVariables,
                                                  CancellationToken ct)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await StartWindowsProcess(workingDir, runAs, argsBuilder, environmentVariables);
        }

        return await StartMacOrLinuxProcess(workingDir,
            runAs,
            argsBuilder,
            environmentVariables,
            ct);
    }


    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    async Task<Process> StartMacOrLinuxProcess(string workingDir,
                                               ProcessCredentials? runAs,
                                               ClaudeCommandArgsBuilder argsBuilder,
                                               Dictionary<string, string> environmentVariables, CancellationToken ct)
    {

        var username = runAs?.Username!;
        if (runAs == null || string.IsNullOrEmpty(username))
        {
            var startInfo1 = StartSimpleProcess(workingDir, argsBuilder, environmentVariables);
            var process1 = Process.Start(startInfo1)!;
            return process1;
        }
        
        //string cmd = $"ANTHROPIC_API_KEY=XXX claude -p \\\"What OS user am I?\\\"";
        string cmd = "ANTHROPIC_API_KEY='XXX' claude -p 'what time is is?' --verbose --output-format stream-json";
        //cmd = $"ANTHROPIC_API_KEY='{environmentVariables["ANTHROPIC_API_KEY"]}' claude   --model claude-sonnet-4-20250514 --bare --strict-mcp-config --output-format stream-json --verbose --permission-mode dontAsk --no-session-persistence --debug-file /var/folders/qr/m8j6qgqj0h93xlr5yw5dsgqh0000gn/T/claude-agent-debug-4bf79ef90e5b4e1a83d0494f8eea23b5.log --mcp-config /var/folders/qr/m8j6qgqj0h93xlr5yw5dsgqh0000gn/T/Test_9d362db4f38445339dafc520eff2b45c/mcp-config.json --system-prompt-file /var/folders/qr/m8j6qgqj0h93xlr5yw5dsgqh0000gn/T/Test_9d362db4f38445339dafc520eff2b45c/system-prompt.md --allowedTools Bash,Read,Write,Edit,Glob,Grep,WebSearch,WebFetch,mcp__octopus__*,mcp__github__* --max-turns 10 -p 'Who Am I? Print out the name of the OS user account you are running under.'";

        var filePath = Path.Combine(workingDir, "my-command.sh");
        File.WriteAllText(Path.Combine(workingDir, "my-command.sh"), $@"#!/bin/bash
        cd {workingDir}
        claude  --model claude-sonnet-4-20250514 --bare --strict-mcp-config --output-format stream-json --verbose  -p 'Who Am I? Print out the name of the OS user account you are running under.'
");
        File.SetUnixFileMode(filePath,
            UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead
            | UnixFileMode.GroupWrite
            | UnixFileMode.GroupExecute
            | UnixFileMode.OtherExecute
            | UnixFileMode.OtherRead);
        File.SetUnixFileMode(workingDir,
            UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute
            | UnixFileMode.GroupWrite
            | UnixFileMode.GroupRead
            | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead
            | UnixFileMode.OtherExecute
            | UnixFileMode.OtherWrite);
        
        
        //cmd = $"ANTHROPIC_API_KEY='{environmentVariables["ANTHROPIC_API_KEY"]}' ./{filePath}";
        cmd = $"{filePath}";
        var startInfo = new ProcessStartInfo
        {
            FileName = "script",
            WorkingDirectory = workingDir,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        //TODO: Perhaps this to script toa void encoding
        var argumentList = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
            new[] { "-q", "/dev/null", "su", "-m", username, "-c", cmd } :
            new[] { "-qec", "su", "-", username, "-c", cmd, "/dev/null" };
        startInfo.ArgumentList.AddRange(argumentList);
            
        foreach (var kvp in environmentVariables)
            startInfo.Environment[kvp.Key] = kvp.Value;

        var process = Process.Start(startInfo)!;
            Task.Delay(1000, ct).Wait(ct);
        // Parse password prompt
        var passwordReq = "Password:".Length;
        var buff = new char[passwordReq];
        await process.StandardOutput.ReadAsync(buff, 0, passwordReq);
        var message = new string(buff);
        if(message != "Password:"){
            throw new Exception($"Unexpected startup message: {message}");
        }
        await process.StandardInput.WriteLineAsync(runAs!.Password);
        if(process.StandardOutput.Read() != '\r' || process.StandardOutput.Read() != '\n'){
            throw new Exception("Expecting new line");
        }

        return process;
    }
}