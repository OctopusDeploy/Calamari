#r "nuget: NuGet.Commands, 6.10.0"

using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

var version = typeof(INuGetResourceProvider).Assembly.GetName().Version;
Console.WriteLine("NuGet.Commands version: " + version);
Console.WriteLine("Parameters " + Env.ScriptArgs[0] + Env.ScriptArgs[1]);