#r "System.Security, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Octostache;

public static class Octopus
{
    public static readonly VariableDictionary Parameters;

	static Octopus() 
	{
		var variablesFile = System.Environment.GetEnvironmentVariable("OctopusVariablesFile");
		Parameters = new VariableDictionary(variablesFile);
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
