using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using Calamari.Util;

namespace Calamari.Integration.Processes
{
    public static class SilentProcessRunner
    {
        // ReSharper disable once InconsistentNaming
        private const int CP_OEMCP = 1;
        private static readonly Encoding oemEncoding;

        static SilentProcessRunner()
        {
            try
            {
                CPINFOEX info;
                if (GetCPInfoEx(CP_OEMCP, 0, out info))
                {
                    oemEncoding = Encoding.GetEncoding(info.CodePage);
                }
                else
                {
                    oemEncoding = Encoding.GetEncoding(850);
                }
            }
            catch (Exception)
            {
                Trace.WriteLine("Couldn't get default OEM encoding");
                oemEncoding = Encoding.UTF8;
            }
        }

        public static SilentProcessRunnerResult ExecuteCommand(
            string executable, 
            string arguments, 
            string workingDirectory, 
            Action<string> output, 
            Action<string> error,
            TimeSpan? timeout = null)
        {
            return ExecuteCommand(executable, arguments, workingDirectory, null, null, null, output, error, timeout);
        }
        
        public static SilentProcessRunnerResult ExecuteCommand(
            string executable, 
            string arguments, 
            string workingDirectory, 
            Dictionary<string, string> environmentVars, 
            Action<string> output, 
            Action<string> error,
            TimeSpan? timeout = null)
        {
            return ExecuteCommand(executable, arguments, workingDirectory, environmentVars, null, null, output, error, timeout);
        }

        public static SilentProcessRunnerResult ExecuteCommand(
            string executable, 
            string arguments, 
            string workingDirectory,
            Dictionary<string, string> environmentVars,
            string userName,             
            SecureString password, 
            Action<string> output, 
            Action<string> error,
            TimeSpan? timeout = null)
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = executable;
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.WorkingDirectory = workingDirectory;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.StandardOutputEncoding = oemEncoding;
                    process.StartInfo.StandardErrorEncoding = oemEncoding;


                    if (environmentVars != null)
                    {
                        foreach (string environmentVar in environmentVars.Keys)
                        {
                            process.StartInfo.EnvironmentVariables[environmentVar] = environmentVars[environmentVar];
                        }                       
                    }

                    RunProcessWithCredentials(process.StartInfo, userName, password);

                    using (var outputWaitHandle = new AutoResetEvent(false))
                    using (var errorWaitHandle = new AutoResetEvent(false))
                    {
                        var errorData = new StringBuilder();
                        process.OutputDataReceived += (sender, e) =>
                        {
                            try
                            {
                                if (e.Data == null)
                                    outputWaitHandle.Set();
                                else
                                    output(e.Data);
                            }
                            catch (Exception ex)
                            {
                                try
                                {
                                    error($"Error occured handling message: {ex.PrettyPrint()}");
                                }
                                catch
                                {
                                    // Ignore
                                }
                            }
                        };

                        process.ErrorDataReceived += (sender, e) =>
                        {
                            try
                            {
                                if (e.Data == null)
                                    errorWaitHandle.Set();
                                else
                                {
                                    errorData.AppendLine(e.Data);
                                    error(e.Data);
                                }
                            }
                            catch (Exception ex)
                            {
                                try
                                {
                                    error($"Error occured handling message: {ex.PrettyPrint()}");
                                }
                                catch
                                {
                                    // Ignore
                                }
                            }
                        };

                        process.Start();

                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        // TimeSpan.TotalMilliseconds can have a value > int.MaxValue, so we just assume wait for ever if this happens.
                        var timeoutMilliseconds = timeout == null || timeout.Value.TotalMilliseconds > int.MaxValue ? -1 : (int)(timeout.Value.TotalMilliseconds);
                        var processExited = process.WaitForExit(timeoutMilliseconds);

                        if (!processExited)
                        {
                            Log.Error($"Process with ID {process.Id} exceeded the max allowed runtime of {timeout} and will be killed.");
                            process.Kill();
                            process.WaitForExit();
                        }

                        outputWaitHandle.WaitOne();
                        errorWaitHandle.WaitOne();

                        return new SilentProcessRunnerResult(process.ExitCode, errorData.ToString(), !processExited);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error when attempting to execute {executable}: {ex.Message}", ex);
            }
        }

        static void RunProcessWithCredentials(ProcessStartInfo processStartInfo, string userName, SecureString password)
        {
            if (string.IsNullOrEmpty(userName) || password == null)
            {
                return;
            }

            var parts = userName.Split(new[] { '\\' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                var domainPart = parts[0];
                var userNamePart = parts[1];
                processStartInfo.Domain = domainPart;
                processStartInfo.UserName = userNamePart;

                WindowStationAndDesktopAccess.GrantAccessToWindowStationAndDesktop(userNamePart, domainPart);
            }
            else
            {
                processStartInfo.UserName = userName;

                WindowStationAndDesktopAccess.GrantAccessToWindowStationAndDesktop(userName);
            }

            processStartInfo.Password = password;

            // Environment variables (such as {env:TentacleHome}) are usually inherited from the parent process.
            // When running as a different user they are not inherited, so manually add them to the process.
            AddTentacleEnvironmentVariablesToProcess(processStartInfo);
        }

        static void AddTentacleEnvironmentVariablesToProcess(ProcessStartInfo processStartInfo)
        {
            foreach (DictionaryEntry environmentVariable in Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process))
            {
                var key = environmentVariable.Key.ToString();
                if (!key.StartsWith("Tentacle"))
                {
                    continue;
                }
                processStartInfo.EnvironmentVariables[key] = environmentVariable.Value.ToString();
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetCPInfoEx([MarshalAs(UnmanagedType.U4)] int CodePage, [MarshalAs(UnmanagedType.U4)] int dwFlags, out CPINFOEX lpCPInfoEx);

        private const int MAX_DEFAULTCHAR = 2;
        private const int MAX_LEADBYTES = 12;
        private const int MAX_PATH = 260;

        [StructLayout(LayoutKind.Sequential)]
        private struct CPINFOEX
        {
            [MarshalAs(UnmanagedType.U4)]
            public int MaxCharSize;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_DEFAULTCHAR)]
            public byte[] DefaultChar;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_LEADBYTES)]
            public byte[] LeadBytes;

            public char UnicodeDefaultChar;

            [MarshalAs(UnmanagedType.U4)]
            public int CodePage;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string CodePageName;
        }
    }
}