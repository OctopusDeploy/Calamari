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
    //TODO: Should this be configurable?
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


    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Higher up checks enforce the correct OS")]
    async Task<Process> StartMacOrLinuxProcess(string workingDir,
                                               ProcessCredentials? runAs,
                                               ClaudeCommandArgsBuilder argsBuilder,
                                               Dictionary<string, string> environmentVariables,
                                               CancellationToken ct)
    {

        var username = runAs?.Username!;
        if (runAs == null || string.IsNullOrEmpty(username))
        {
            var startInfo1 = StartSimpleProcess(workingDir, argsBuilder, environmentVariables);
            var process1 = Process.Start(startInfo1)!;
            return process1;
        }

        var filePath = Path.Combine(workingDir, "my-command.sh");
        await File.WriteAllTextAsync(Path.Combine(workingDir, "my-command.sh"), $@"#!/bin/bash
cd {workingDir}
{ClaudeCodePath} {argsBuilder.Build()}
", ct);
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

        var argumentList = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
            new[] { "-q", "/dev/null", "su", "-m", username, "-c", filePath } :
            new[] { "-qec", "su", "-", username, "-c", filePath, "/dev/null" };
        startInfo.ArgumentList.AddRange(argumentList);
            
        foreach (var kvp in environmentVariables)
            startInfo.Environment[kvp.Key] = kvp.Value;
        //SetPermissionsRecursively(workingDir);
        var o = Process.Start("chmod", ["-R", "777", workingDir]);
        await o.WaitForExitAsync(ct);
         if(o.ExitCode != 0)
            throw new Exception($"Failed to set permissions on working directory: {workingDir}");
        
        
        var process = Process.Start(startInfo)!;
        
        // TODO: Should just wait as long as it takes to read "Password:" below
        await Task.Delay(1000, ct).WaitAsync(ct);
        
        // Parse password prompt so consuming code can ignore this initial password check.
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
    
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    static void SetPermissionsRecursively(string path)
    {
        var dirMode = UnixFileMode.UserRead
                      | UnixFileMode.UserWrite
                      | UnixFileMode.UserExecute
                      | UnixFileMode.GroupWrite
                      | UnixFileMode.GroupRead
                      | UnixFileMode.GroupExecute
                      | UnixFileMode.OtherRead
                      | UnixFileMode.OtherExecute
                      | UnixFileMode.OtherWrite;
        var fileMode = UnixFileMode.UserRead
                       | UnixFileMode.UserWrite
                       | UnixFileMode.UserExecute
                       | UnixFileMode.GroupRead
                       | UnixFileMode.GroupWrite
                       | UnixFileMode.GroupExecute
                       | UnixFileMode.OtherExecute
                       | UnixFileMode.OtherRead;                                                                                                                                            
                                                                                                                                                                                                               
      new DirectoryInfo(path).UnixFileMode = dirMode;                                                                                                                                                            
      foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))                                                                                                                     
          File.SetUnixFileMode(file, fileMode);                                             
      foreach (var dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))                                                                                                                
          new DirectoryInfo(dir).UnixFileMode = dirMode;                                                                                                                                                         
  }     
}