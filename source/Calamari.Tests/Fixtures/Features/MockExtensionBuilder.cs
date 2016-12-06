using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Extensibility;
using Calamari.Extensibility.Features;
using Calamari.Extensibility.FileSystem;
using Calamari.Features;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using NUnit.Framework;
using Calamari.Util;
#if !NET40
using System.Reflection;
#endif


namespace Calamari.Tests.Fixtures.Features
{
    public static class MockExtensionBuilder
    {

        static string WriteToDisk(string assemblyPath, CSharpCompilation compilation)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath));
            using (var dllStream = File.OpenWrite(assemblyPath))
            using (var pdbStream = File.OpenWrite("C:\\temp\\Test.pdb"))
            using (var win32resStream = compilation.CreateDefaultWin32Resources(
                                                                        versionResource: true, // Important!
                                                                        noManifest: false,
                                                                        manifestContents: null,
                                                                        iconInIcoFormat: null))
            {
                var result = compilation.Emit(
                                             peStream: dllStream,
                                            pdbStream: pdbStream,
                                            win32Resources: win32resStream);

                if (!result.Success)
                {
                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (Diagnostic diagnostic in failures)
                    {
                        
                        Console.WriteLine("{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
                    }
                    throw new Exception("Failed to create test assembly");
                }
                return assemblyPath;
            }

        }

        static SyntaxTree GetFeature(AssemblyQualifiedClassName classDetails)
        {
            var classParts = classDetails.ClassName.Split('.');
            var namespacePart = string.Join(".", classParts.Take(classParts.Length - 1));
            var className = classParts.Last();

            return CSharpSyntaxTree.ParseText(@"
               using Calamari.Extensibility.Features;

namespace " + namespacePart + @"
{
  
    public class " + className + @" : IFeature
        {
            private readonly ILog logger;
            public " + className + @"(ILog logger)
            {
                this.logger = logger;
            }

            public void Install(IVariableDictionary variables)
            {
                logger.Info(""Hello World!"");
            }
        }
    }");
        }

        static SyntaxTree GetAssemblyFile(AssemblyQualifiedClassName classDetails)
        {
            StringBuilder asmInfo = new StringBuilder();
            var versionString = classDetails.Version?.ToString() ?? "1.0.0.0";

            asmInfo.AppendLine("using System.Reflection;");
            asmInfo.AppendLine($"[assembly: AssemblyTitle(\"{classDetails.AssemblyName}\")]");
            asmInfo.AppendLine($"[assembly: AssemblyVersion(\"{versionString}\")]");
            asmInfo.AppendLine($"[assembly: AssemblyFileVersion(\"{versionString}\")]");
            // Product Info
            asmInfo.AppendLine($"[assembly: AssemblyProduct(\"{classDetails.AssemblyName}\")]");
            asmInfo.AppendLine($"[assembly: AssemblyInformationalVersion(\"{versionString}\")]");

            return CSharpSyntaxTree.ParseText(asmInfo.ToString(), encoding: Encoding.UTF8);
        }

        public static string Build(string path, AssemblyQualifiedClassName classDetails)
        {
            MetadataReference[] references = {
                MetadataReference.CreateFromFile(typeof(IFeature).GetTypeInfo().Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Attribute).GetTypeInfo().Assembly.Location)
            };
           
            var compilation = CSharpCompilation.Create(
                classDetails.AssemblyName,
                syntaxTrees: new[] { GetFeature(classDetails), GetAssemblyFile(classDetails)},
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var assemblyPath = Path.Combine(path, $"{classDetails.AssemblyName}.dll");

            return WriteToDisk(assemblyPath, compilation);
        }


    }
}