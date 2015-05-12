#r "System.Security, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public static class Octopus
{
    public static readonly OctopusParametersDictionary Parameters = new OctopusParametersDictionary();


	public class OctopusParametersDictionary : System.Collections.Generic.Dictionary<string,string>
	{
		public OctopusParametersDictionary() : base(System.StringComparer.OrdinalIgnoreCase)
		{
			{{VariableDeclarations}}
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
}
