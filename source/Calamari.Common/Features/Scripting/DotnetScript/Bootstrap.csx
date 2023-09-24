#r "System.Security, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Net;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Security.Principal;
using System.Linq;

public static class Octopus
{
    public static OctopusParametersDictionary Parameters { get; private set; }

    static Octopus()
    {
        InitializeDefaultProxy();
    }

    public static void Initialize(string password)
    {
        if (Parameters != null)
        {
            throw new Exception("Octopus can only be initialized once.");
        }
        Parameters = new OctopusParametersDictionary(password);
        LogEnvironmentInformation();
    }

    static void LogEnvironmentInformation()
    {
        if (Parameters.ContainsKey("Octopus.Action.Script.SuppressEnvironmentLogging") && Parameters["Octopus.Action.Script.SuppressEnvironmentLogging"] == "True")
            return;

        var environmentInformationStamp = $"Dotnet-Script Environment Information:{Environment.NewLine}" +
            $"  {string.Join($"{Environment.NewLine}  ", SafelyGetEnvironmentInformation())}";

        Console.WriteLine("##octopus[stdout-verbose]");
        Console.WriteLine(environmentInformationStamp);
        Console.WriteLine("##octopus[stdout-default]");
    }

    #region Logging Helpers

    static string[] SafelyGetEnvironmentInformation()
    {
        var envVars = GetEnvironmentVars()
            .Concat(GetPathVars())
            .Concat(GetProcessVars());
        return envVars.ToArray();
    }

    private static string SafelyGet(Func<string> thingToGet)
    {
        try
        {
            return thingToGet.Invoke();
        }
        catch (Exception)
        {
            return "Unable to retrieve environment information.";
        }
    }

    static IEnumerable<string> GetEnvironmentVars()
    {
        yield return SafelyGet(() => $"OperatingSystem: {Environment.OSVersion}");
        yield return SafelyGet(() => $"OsBitVersion: {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}");
        yield return SafelyGet(() => $"Is64BitProcess: {Environment.Is64BitProcess}");
        yield return SafelyGet(() => $"CurrentUser: {WindowsIdentity.GetCurrent().Name}");
        yield return SafelyGet(() => $"MachineName: {Environment.MachineName}");
        yield return SafelyGet(() => $"ProcessorCount: {Environment.ProcessorCount}");
    }

    static IEnumerable<string> GetPathVars()
    {
        yield return SafelyGet(() => $"CurrentDirectory: {Directory.GetCurrentDirectory()}");
        yield return SafelyGet(() => $"TempDirectory: {Path.GetTempPath()}");
    }

    static IEnumerable<string> GetProcessVars()
    {
        yield return SafelyGet(() => {
            var process = Process.GetCurrentProcess();
            return $"HostProcess: {process.ProcessName} ({process.Id})";
        });
    }

    #endregion

    public class OctopusParametersDictionary : System.Collections.Generic.Dictionary<string, string>
    {
        private byte[] Key { get; set; }

        public OctopusParametersDictionary(string key) : base(System.StringComparer.OrdinalIgnoreCase)
        {
            Key = Convert.FromBase64String(key);
            /*{{VariableDeclarations}}*/
        }

        public string DecryptString(string encrypted, string iv)
        {
            using (var algorithm = Aes.Create())
            {
                algorithm.Mode = CipherMode.CBC;
                algorithm.Padding = PaddingMode.PKCS7;
                algorithm.KeySize = 128;
                algorithm.BlockSize = 128;
                algorithm.Key = Key;
                algorithm.IV = Convert.FromBase64String(iv);
                using (var dec = algorithm.CreateDecryptor())
                using (var ms = new MemoryStream(Convert.FromBase64String(encrypted)))
                using (var cs = new CryptoStream(ms, dec, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs, Encoding.UTF8))
                {
                    return sr.ReadToEnd();
                }
            }
        }
    }

    private static string EncodeServiceMessageValue(string value)
    {
        var valueBytes = System.Text.Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(valueBytes);
    }

    public static void FailStep(string message = null)
    {
        if (message != null)
        {
            message = EncodeServiceMessageValue(message);
            Console.WriteLine("##octopus[resultMessage message='{0}']", message);
        }
        Environment.Exit(-1);
    }

    public static void SetVariable(string name, string value, bool sensitive = false)
    {
        name = EncodeServiceMessageValue(name);
        value = EncodeServiceMessageValue(value);

        Parameters[name] = value;

        if (sensitive) {
            Console.WriteLine("##octopus[setVariable name='{0}' value='{1}' sensitive='{2}']", name, value, EncodeServiceMessageValue("True"));
        } else {
        Console.WriteLine("##octopus[setVariable name='{0}' value='{1}']", name, value);
    }
    }

    public static void CreateArtifact(string path, string fileName = null)
    {
        if (fileName == null)
        {
            fileName = System.IO.Path.GetFileName(path);
        }

        var serviceFileName = EncodeServiceMessageValue(fileName);

        var length = System.IO.File.Exists(path) ? new System.IO.FileInfo(path).Length.ToString() : "0";
        length = EncodeServiceMessageValue(length);

        path = System.IO.Path.GetFullPath(path);
        var servicepath = EncodeServiceMessageValue(path);

        Console.WriteLine("##octopus[stdout-verbose]");
        Console.WriteLine("Artifact {0} will be collected from {1} after this step completes", fileName, path);
        Console.WriteLine("##octopus[stdout-default]");
        Console.WriteLine("##octopus[createArtifact path='{0}' name='{1}' length='{2}']", servicepath, serviceFileName, length);
    }
    
    public static void UpdateProgress(int percentage, string message = "")
    {
        Console.WriteLine("##octopus[progress percentage='{0}' message='{1}']", EncodeServiceMessageValue(percentage.ToString()), EncodeServiceMessageValue(message));
    }
    
    public static void WriteVerbose(string message)
    {
        Console.WriteLine("##octopus[stdout-verbose]");
        Console.WriteLine(message);
        Console.WriteLine("##octopus[stdout-default]");
    }
    
    public static void WriteHighlight(string message)
    {
        Console.WriteLine("##octopus[stdout-highlight]");
        Console.WriteLine(message);
        Console.WriteLine("##octopus[stdout-default]");
    }
    
    public static void WriteWait(string message)
    {
        Console.WriteLine("##octopus[stdout-wait]");
        Console.WriteLine(message);
        Console.WriteLine("##octopus[stdout-default]");
    }
    
    public static void WriteWarning(string message)
    {
        Console.WriteLine("##octopus[stdout-warning]");
        Console.WriteLine(message);
        Console.WriteLine("##octopus[stdout-default]");
    }

    public static void InitializeDefaultProxy()
    {
        var proxyUsername = Environment.GetEnvironmentVariable("TentacleProxyUsername");
        var proxyPassword = Environment.GetEnvironmentVariable("TentacleProxyPassword");
        var proxyHost = Environment.GetEnvironmentVariable("TentacleProxyHost");
        var proxyPortText = Environment.GetEnvironmentVariable("TentacleProxyPort");
        int proxyPort;
        int.TryParse(proxyPortText, out proxyPort);

		var useDefaultProxyText = Environment.GetEnvironmentVariable("TentacleUseDefaultProxy");
		bool useDefaultProxy;
		bool.TryParse(useDefaultProxyText, out useDefaultProxy);

        var useCustomProxy = !string.IsNullOrWhiteSpace(proxyHost);
        var bypassProxy = !useCustomProxy && !useDefaultProxy;
        
        var proxy = useCustomProxy
            ? new WebProxy(new UriBuilder("http", proxyHost, proxyPort).Uri)
            : useDefaultProxy ? WebRequest.GetSystemWebProxy() : new WebProxy();

        var useDefaultCredentials = string.IsNullOrWhiteSpace(proxyUsername);

        if (!bypassProxy)
            proxy.Credentials = useDefaultCredentials
                ? useCustomProxy
                    ? new NetworkCredential()
                    : CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(proxyUsername, proxyPassword);

        WebRequest.DefaultWebProxy = proxy;
    }
}

public class ScriptArgsEnv {

    public ScriptArgsEnv(IList<string> args)
    {
        ScriptArgs = args;
    }

    public IList<string> ScriptArgs { get; set; }
}

var Env = new ScriptArgsEnv(Args);

Octopus.Initialize(Args[Args.Count - 1]);