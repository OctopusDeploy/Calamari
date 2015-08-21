#r "System.Security, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Net;
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

		 private byte[] ExtractSalt(byte[] encrypted, out byte[] salt)
        {
            salt = new byte[8];
            Buffer.BlockCopy(encrypted, 8, salt, 0, 8);
            
            int aesDataLength = encrypted.Length - 16;
            var aesData = new byte[aesDataLength];
            Buffer.BlockCopy(encrypted, 16, aesData, 0, aesDataLength);
            return aesData;
        }
		
        private byte[] GetKey(byte[] salt, out byte[] iv)
        {
			var passwordBytes = Encoding.UTF8.GetBytes(Password);

            byte[] key;
            using (var md5 = MD5.Create())
            {
                int preKeyLength = passwordBytes.Length + salt.Length;
                byte[] preKey = new byte[preKeyLength];
                Buffer.BlockCopy(passwordBytes, 0, preKey, 0, passwordBytes.Length);
                Buffer.BlockCopy(salt, 0, preKey, passwordBytes.Length, salt.Length);

                key = md5.ComputeHash(preKey);

                int preIVLength = key.Length + preKeyLength;
                byte[] preIV = new byte[preIVLength];

                Buffer.BlockCopy(key, 0, preIV, 0, key.Length);
                Buffer.BlockCopy(preKey, 0, preIV, key.Length, preKey.Length);
                iv = md5.ComputeHash(preIV);
            }
            return key;
        }

        private SymmetricAlgorithm GetAlgorithm(byte[] salt)
        {
            byte[] iv;
            var key = GetKey(salt, out iv);

            return new AesManaged
            {
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7,
                KeySize = 128,
                BlockSize = 128,
                Key = key,
                IV = iv
            };
        }

		public string DecryptString(string encryptedText)
        {
            var textBytes = Convert.FromBase64String(encryptedText);
            byte[] salt;
            var aesData = ExtractSalt(textBytes, out salt);
            using (var algorithm = GetAlgorithm(salt))
            {
                using (var dec = algorithm.CreateDecryptor())
                using (var ms = new MemoryStream(aesData))
                using (var cs = new CryptoStream(ms, dec, CryptoStreamMode.Read))
                using (var sw = new StreamReader(cs, Encoding.UTF8))
                {
                    return sw.ReadToEnd();
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