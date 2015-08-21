#r "System.Security, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Net;
using System.Linq;
using System.Security.Cryptography;

public static class Octopus
{
    public static OctopusParametersDictionary Parameters { get; private set; }
	
	static Octopus() 
	{
		InitializeDefaultProxy();
	}

	public static void Initialize(string password) {
		if(Parameters != null) {
			throw new Exception("Octopus can only be initialized once.");
		}
		Parameters = new OctopusParametersDictionary(password);
	}

	public class OctopusParametersDictionary : System.Collections.Generic.Dictionary<string,string>
	{
		private string Password { get;set; }

		public OctopusParametersDictionary(string password) : base(System.StringComparer.OrdinalIgnoreCase)
		{
			Password = password;
{{VariableDeclarations}}
		}

		private string DecryptString(string encryptedText)
		{
			var salt = Encoding.UTF8.GetBytes("SaltCrypto");
			var vector = Encoding.UTF8.GetBytes("IV_Password");
			var pass = Encoding.UTF8.GetBytes(Password);
        
			var InitializationVector = (new SHA1Managed()).ComputeHash(vector).Take(16).ToArray();
			var Key = new PasswordDeriveBytes(pass, salt, "SHA1", 5).GetBytes(32);

			var textBytes = Convert.FromBase64String(encryptedText);
			using (var r = new RijndaelManaged() {Key = Key, IV = InitializationVector})
			using (var dec = r.CreateDecryptor())
			using (var ms = new MemoryStream(textBytes))
			using (var cs = new CryptoStream(ms, dec, CryptoStreamMode.Read))
			using (var sw = new StreamReader(cs))
			{
				return sw.ReadToEnd();
			}
		}
	}

	private static string EncodeServiceMessageValue(string value)
	{
		var valueBytes = System.Text.Encoding.UTF8.GetBytes(value);
		return Convert.ToBase64String(valueBytes);
	}

	public static void SetVariable(string name, string value) 
	{ 	
		name = EncodeServiceMessageValue(name);
		value = EncodeServiceMessageValue(value);

		Parameters[name] = value;

		Console.WriteLine("##octopus[setVariable name='{0}' value='{1}']", name, value);
	}

	public static void CreateArtifact(string path) 
	{
		var fileName = System.IO.Path.GetFileName(path); 
		fileName = EncodeServiceMessageValue(fileName);	

		if(!System.IO.File.Exists(path)){
		}

		var length = System.IO.File.Exists(path) ? new System.IO.FileInfo(path).Length.ToString() : "0";
		length = EncodeServiceMessageValue(length);

		path = System.IO.Path.GetFullPath(path);
		path = EncodeServiceMessageValue(path);


		Console.WriteLine("##octopus[createArtifact path='{0}' name='{1}' length='{2}']", path, fileName, length);
	}

	public static void InitializeDefaultProxy() 
	{
		var proxyUsername = Environment.GetEnvironmentVariable("TentacleProxyUsername");
        var proxyPassword = Environment.GetEnvironmentVariable("TentacleProxyPassword");

        WebRequest.DefaultWebProxy.Credentials = string.IsNullOrWhiteSpace(proxyUsername) 
            ? CredentialCache.DefaultCredentials 
            : new NetworkCredential(proxyUsername, proxyPassword);
	}
}

Octopus.Initialize(Env.ScriptArgs[0]);