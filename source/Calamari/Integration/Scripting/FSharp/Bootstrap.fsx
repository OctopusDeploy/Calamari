#r "System.Security, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Net
open System.Security.Cryptography

(*public static class Octopus
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
		private byte[] Key { get;set; }

		public OctopusParametersDictionary(string key) : base(System.StringComparer.OrdinalIgnoreCase)
		{
			Key = Convert.FromBase64String(key);
{{VariableDeclarations}}
		}
        
     	public string DecryptString(string encrypted, string iv)
        {
            using (var algorithm = new AesCryptoServiceProvider() {
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7,
                KeySize = 128,
                BlockSize = 128 })
			{
				algorithm.Key = Key;
				algorithm.IV =  Convert.FromBase64String(iv);
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

	public static void SetVariable(string name, string value) 
	{ 	
		name = EncodeServiceMessageValue(name);
		value = EncodeServiceMessageValue(value);

		Parameters[name] = value;

		Console.WriteLine("##octopus[setVariable name='{0}' value='{1}']", name, value);
	}

	public static void CreateArtifact(string path, string fileName = null) 
	{
		if(fileName == null){
			fileName = System.IO.Path.GetFileName(path); 
		} 

		fileName = EncodeServiceMessageValue(fileName);	

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

Octopus.Initialize(Env.ScriptArgs[Env.ScriptArgs.Count - 1]);
*)