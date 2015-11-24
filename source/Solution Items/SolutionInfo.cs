using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyDescription("Calamari is a set of open source deployment agent libraries for Octopus Deploy.")]
[assembly: AssemblyCompany("Octopus Deploy Pty. Ltd.")]
[assembly: AssemblyProduct("Calamari")]
[assembly: AssemblyCopyright("Copyright © Octopus Deploy Pty. Ltd. 2011-2015")]
[assembly: AssemblyCulture("")]
#if DEBUG

[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: ComVisible(false)]