using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Variables;
using Calamari.Scripting;
using Calamari.Tests.Shared;
using Calamari.Tests.Shared.Helpers;
using FluentAssertions;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using NUnit.Framework.Internal;
using NUnit.Framework;
using Object = Google.Apis.Storage.v1.Data.Object;
using ZipFile = System.IO.Compression.ZipFile;

namespace Calamari.GoogleCloudScripting.Tests
{
    [TestFixture]
    [TestFixtureSource(typeof(DownloadCLI))]
    class GoogleCloudActionHandlerFixture
    {
        private readonly string cliPath;

        class DownloadCLI: IEnumerable
        {
            Object RetrieveFileFromGoogleCloudStorage(StorageClient client)
            {
                var results = client.ListObjects("cloud-sdk-release", "google-cloud-sdk-");
                var listOfFilesSortedByCreatedDate = new SortedList<DateTime, Object>(Comparer<DateTime>.Create((a, b) => b.CompareTo(a)));

                foreach (var result in results)
                {
                    if (result.TimeCreated.HasValue)
                    {
                        listOfFilesSortedByCreatedDate.Add(result.TimeCreated.Value, result);
                    }
                }

                string postfix = "";

                if (OperatingSystem.IsLinux())
                {
                    postfix = "-linux-x86_64.tar.gz";
                }
                else if (OperatingSystem.IsWindows())
                {
                    postfix = "-windows-x86_64-bundled-python.zip";
                }
                else if (OperatingSystem.IsMacOS())
                {
                    postfix = "-darwin-x86_64-bundled-python.tar.gz";
                }

                foreach (var file in listOfFilesSortedByCreatedDate.Where(file => file.Value.Name.EndsWith(postfix)))
                {
                    return file.Value;
                }

                throw new Exception(
                    $"Could not find a suitable executable to download from Google cloud storage for the postfix {postfix}");
            }

            string DownloadFile()
            {
                GoogleCredential? credential;
                try
                {
                    credential = GoogleCredential.FromJson(EnvironmentJsonKey);
                }
                catch (InvalidOperationException)
                {
                    throw new Exception("Error reading json key file, please ensure file is correct.");
                }
            
                using StorageClient client = StorageClient.Create(credential);
            
                var fileToDownload = RetrieveFileFromGoogleCloudStorage(client);
            
                var rootPath = TestEnvironment.GetTestPath("gcloudCLIPath");
                Directory.CreateDirectory(rootPath);
            
                var zipFile = Path.Combine(rootPath, fileToDownload.Name);
        
                if (!File.Exists(zipFile))
                {
                    using var fileStream =
                        new FileStream(zipFile, FileMode.Create, FileAccess.Write, FileShare.None);
                    client.DownloadObject(fileToDownload, fileStream);
                }
        
                var destinationDirectory = Path.Combine(rootPath, Path.GetFileNameWithoutExtension(fileToDownload.Name));
                var gcloudExe = Path.Combine(destinationDirectory, "google-cloud-sdk", "bin", $"gcloud{(OperatingSystem.IsWindows() ? ".cmd" : String.Empty)}");
        
                if (!File.Exists(gcloudExe))
                {
                    if (IsGZip(zipFile))
                    {
                        ExtractGZip(zipFile, destinationDirectory);
                    }
                    else if (IsZip(zipFile))
                    {
                        ZipFile.ExtractToDirectory(zipFile, destinationDirectory);
                    }
                    else
                    {
                        throw new Exception(
                            $"{zipFile} cannot be extracted. Supported compressions are .zip and .tar.gz.");
                    }
                }

                AddExecutePermission(gcloudExe);
                return gcloudExe;
            }

            public static void AddExecutePermission(string exePath)
            {
                if (CalamariEnvironment.IsRunningOnWindows)
                    return;

                var stdOut = new StringBuilder();
                var stdError = new StringBuilder();
                var result = SilentProcessRunner.ExecuteCommand("chmod",
                    $"+x {exePath}",
                    Path.GetDirectoryName(exePath) ?? string.Empty,
                    s => stdOut.AppendLine(s),
                    s => stdError.AppendLine(s));

                if (result.ExitCode != 0)
                    throw new Exception(stdOut.ToString() + stdError);
            }

            bool IsZip(string fileName) =>
                string.Equals(Path.GetExtension(fileName), ".zip", StringComparison.OrdinalIgnoreCase);

            bool IsGZip(string fileName) =>
                string.Equals(Path.GetExtension(fileName), ".gz", StringComparison.OrdinalIgnoreCase);

            void ExtractGZip(string gzArchiveName, string destinationFolder)
            {
                Stream inStream = File.OpenRead(gzArchiveName);
                Stream gzipStream = new GZipInputStream(inStream);

                TarArchive tarArchive = TarArchive.CreateInputTarArchive(gzipStream, Encoding.UTF8);
                tarArchive.ExtractContents(destinationFolder);
                tarArchive.Close();

                gzipStream.Close();
                inStream.Close();
            }

            public IEnumerator GetEnumerator()
            {
                var result = DownloadFile();
                var startIndex = result.IndexOf("google-cloud-sdk-", StringComparison.Ordinal);
                var length = result.IndexOf(Path.DirectorySeparatorChar, startIndex + 1) - startIndex;
                yield return new TestFixtureParameters(new TestFixtureData(result) { TestName = result.Substring(startIndex, length)});
            }
        }

        const string JsonEnvironmentVariableKey = "GOOGLECLOUD_OCTOPUSAPITESTER_JSONKEY";

        static readonly string? EnvironmentJsonKey = Environment.GetEnvironmentVariable(JsonEnvironmentVariableKey);

        public GoogleCloudActionHandlerFixture(string cliPath)
        {
            this.cliPath = cliPath;
        }

        [OneTimeSetUp]
        public void Setup()
        {
            if (EnvironmentJsonKey == null)
            {
                throw new Exception($"Environment Variable `{JsonEnvironmentVariableKey}` could not be found. The value can be found in the password store under GoogleCloud - OctopusAPITester");
            }
        }

        [Test]
        public void ExecuteAnInlineScript()
        {
            var psScript = $"{cliPath} info";

            CommandTestBuilder.CreateAsync<RunScriptCommand, Program>()
                .WithArrange(context =>
                {
                    AddDefaults(context);
                    context.Variables.Add(ScriptVariables.ScriptSource, ScriptVariables.ScriptSourceOptions.Inline);
                    context.Variables.Add(ScriptVariables.Syntax, OperatingSystem.IsWindows() ? ScriptSyntax.PowerShell.ToString() : ScriptSyntax.Bash.ToString());
                    context.Variables.Add(ScriptVariables.ScriptBody, psScript);
                })
                .Execute();
        }

        [Test]
        public void TryToExecuteAnInlineScriptWithInvalidCredentials()
        {
            var psScript = $"{cliPath} info";

            CommandTestBuilder.CreateAsync<RunScriptCommand, Program>()
                .WithArrange(context =>
                {
                    AddDefaults(context, "{ \"name\": \"hello\" }");
                    context.Variables.Add(ScriptVariables.ScriptSource, ScriptVariables.ScriptSourceOptions.Inline);
                    context.Variables.Add(ScriptVariables.Syntax, OperatingSystem.IsWindows() ? ScriptSyntax.PowerShell.ToString() : ScriptSyntax.Bash.ToString());
                    context.Variables.Add(ScriptVariables.ScriptBody, psScript);
                })
                .WithAssert(result => result.WasSuccessful.Should().BeFalse())
                .Execute(false);
        }

        void AddDefaults(CommandTestBuilderContext context, string? keyFile = null)
        {
            context.Variables.Add("Octopus.Action.GoogleCloudAccount.Variable", "MyVariable");
            context.Variables.Add("MyVariable.JsonKey", Convert.ToBase64String(Encoding.UTF8.GetBytes(keyFile ?? EnvironmentJsonKey!)));
            context.Variables.Add("Octopus.Action.GoogleCloud.CustomExecutable", cliPath);
        }
    }
}