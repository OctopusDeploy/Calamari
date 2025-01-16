using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyDescription("Octopus Deploy is a user-friendly release automation solution for professional .NET developers.")]
[assembly: AssemblyCompany("Octopus Deploy Pty. Ltd.")]
[assembly: AssemblyProduct("Octopus Deploy")]
[assembly: AssemblyCopyright("Copyright © Octopus Deploy Pty. Ltd. 2022")]
[assembly: AssemblyCulture("")]
#if DEBUG

[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: ComVisible(false)]
