#r "System.Security, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public static class Octopus
{
    public static readonly OctopusParametersDictionary Parameters = new OctopusParametersDictionary();

	public class OctopusParametersDictionary : Dictionary<string, string>
	{
		public OctopusParametersDictionary() : base(StringComparer.OrdinalIgnoreCase) {}

		public new string this[string key] 
		{
			get { return base.ContainsKey(key) ? base[key] : ""; }
			set { base[key] = value; }
		}
	}

	static Octopus() 
	{
		var variablesFile = System.Environment.GetEnvironmentVariable("OctopusVariablesFile");
		LoadVariables(variablesFile);
	}

    public static void LoadVariables(string variablesFilePath)
    {
        using (var targetStream = new FileStream(variablesFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var reader = new StreamReader(targetStream))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
			{
				if (String.IsNullOrEmpty(line)) 
				{
					continue;
				}


			    var parts = line.Split(',');
			    var name = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
			    var value = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
			    Parameters[name] = value;
			}
        }
    }

	static string EncodeServiceMessageValue(string value)
	{
		var valueBytes = System.Text.Encoding.UTF8.GetBytes(value);
		return Convert.ToBase64String(valueBytes);
	}

	public static void SetVariable(string name, string value) 
	{ 	
		name = EncodeServiceMessageValue(name);
		value = EncodeServiceMessageValue(value);

		Console.WriteLine("##octopus[setVariable name='{0}' value='{1}']", name, value);
	}

	public static void CreateArtifact(string path) 
	{
		var originalFilename = System.IO.Path.GetFileName(path); 
		originalFilename = EncodeServiceMessageValue(originalFilename);	

		path = System.IO.Path.GetFullPath(path);
		path = EncodeServiceMessageValue(path);

		Console.WriteLine("##octopus[createArtifact path='{0}' originalFilename='{1}']", path, originalFilename);
	}

	public static void EnableOutputBuffer(string operation) 
	{
		operation = EncodeServiceMessageValue(operation);

		Console.WriteLine("##octopus[enableBuffer operation='{0}']", operation);
	}

	public static void DisableOutputBuffer() 
	{
		Console.WriteLine("##octopus[disableBuffer]");
	}
}
