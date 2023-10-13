using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Variables;
using Calamari.Scripting;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using FluentAssertions;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using NUnit.Framework.Internal;
using NUnit.Framework;
using GoogleStorageObject = Google.Apis.Storage.v1.Data.Object;
using ZipFile = System.IO.Compression.ZipFile;

namespace Calamari.GoogleCloudScripting.Tests
{
    [TestFixture]
    [TestFixtureSource(typeof(DownloadCli))]
    class GoogleCloudActionHandlerFixture
    {
        private readonly string cliPath;

        private class DownloadCli: IEnumerable
        {
            private readonly string postfix = "";

            public DownloadCli()
            {
                if (OperatingSystem.IsLinux())
                {
                    postfix = "-linux-x86_64.tar.gz";
                }
                else if (OperatingSystem.IsWindows())
                {
                    postfix = "-windows-x86_64-bundled-python.zip";
                }
                else if (OperatingSystem.IsMacOs())
                {
                    postfix = "-darwin-x86_64-bundled-python.tar.gz";
                }
            }
            
            private GoogleStorageObject RetrieveFileFromGoogleCloudStorage(StorageClient client)
            {
                var results = client.ListObjects("cloud-sdk-release", "google-cloud-sdk-");
                var listOfFilesSortedByCreatedDate = new SortedList<DateTime, GoogleStorageObject>(Comparer<DateTime>.Create((a, b) => b.CompareTo(a)));

                // Checking date time less than to fetch gcloud versions earlier than 448.
                // 448 requires python 3.8 and up, currently 3.5 is available on Teamcity agents
                // This is intended as a temporary workaround
                // https://build.octopushq.com/test/-1383742321497021969?currentProjectId=OctopusDeploy_Calamari_CalamariGoogleCloudScriptingTests_NetcoreTesting&expandTestHistoryChartSection=true
                var dateBeforeGcloud448 = new DateTime(2023, 09, 20);
                
                foreach (var result in results)
                {
                    if (result.TimeCreated.HasValue && result.TimeCreated.Value.CompareTo(dateBeforeGcloud448) == -1)
                    {
                        listOfFilesSortedByCreatedDate.Add(result.TimeCreated.Value, result);
                    }
                }

                foreach (var file in listOfFilesSortedByCreatedDate.Where(file => file.Value.Name.EndsWith(postfix)))
                {
                    return file.Value;
                }

                throw new Exception(
                    $"Could not find a suitable executable to download from Google cloud storage for the postfix {postfix}");
            }

            private string DownloadFile()
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
            
                using var client = StorageClient.Create(credential);
            
                var fileToDownload = RetrieveFileFromGoogleCloudStorage(client);
            
                var rootPath = TestEnvironment.GetTestPath("gcloud");
                Directory.CreateDirectory(rootPath);
            
                var zipFile = Path.Combine(rootPath, fileToDownload.Name);
        
                if (!File.Exists(zipFile))
                {
                    using var fileStream = new FileStream(zipFile, FileMode.Create, FileAccess.Write, FileShare.None);
                    client.DownloadObject(fileToDownload, fileStream);
                }

                // postfix is stripped from the path to prevent file paths exceeding length limit on Windows 2012
                var shortenedName = Path.GetFileName(fileToDownload.Name).Replace(postfix, "");
                
                var destinationDirectory = Path.Combine(rootPath, shortenedName);
                var gcloudExe = Path.Combine(destinationDirectory, "google-cloud-sdk", "bin", $"gcloud{(OperatingSystem.IsWindows() ? ".cmd" : string.Empty)}");
        
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
                        throw new Exception($"{zipFile} cannot be extracted. Supported compressions are .zip and .tar.gz.");
                    }
                }

                ExecutableHelper.AddExecutePermission(gcloudExe);
                return gcloudExe;
            }

            private static bool IsZip(string fileName) =>
                string.Equals(Path.GetExtension(fileName), ".zip", StringComparison.OrdinalIgnoreCase);

            private static bool IsGZip(string fileName) =>
                string.Equals(Path.GetExtension(fileName), ".gz", StringComparison.OrdinalIgnoreCase);

            private static void ExtractGZip(string gzArchiveName, string destinationFolder)
            {
                using var inStream = File.OpenRead(gzArchiveName);
                using var gzipStream = new GZipInputStream(inStream);
                using var tarArchive = TarArchive.CreateInputTarArchive(gzipStream, Encoding.UTF8);
                tarArchive.ExtractContents(destinationFolder);
            }

            private static string PostfixWithoutExtension(string postfix) =>
                IsZip(postfix) ? postfix.Replace(".zip", "") : postfix.Replace(".tar.gz", "");
            
            public IEnumerator GetEnumerator()
            {
                var result = DownloadFile();
                var startIndex = result.IndexOf("google-cloud-sdk-", StringComparison.Ordinal);
                var length = result.IndexOf(Path.DirectorySeparatorChar, startIndex + 1) - startIndex;
                yield return new TestFixtureParameters(new TestFixtureData(result)
                    {
                        TestName = $"{result.Substring(startIndex, length)}{PostfixWithoutExtension(postfix)}"
                    });
            }
        }

        private const string JsonEnvironmentVariableKey = "GOOGLECLOUD_OCTOPUSAPITESTER_JSONKEY";

        private static readonly string? EnvironmentJsonKey = Environment.GetEnvironmentVariable(JsonEnvironmentVariableKey);

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
        [Ignore("Agents need to have their version of Python updated. Ultimately we should move this to use a docker container.  See https://octopusdeploy.slack.com/archives/C01HH8T16G3/p1695967518947159")]
        public async Task ExecuteAnInlineScript()
        {
            var psScript = $"{cliPath} info";

            await CommandTestBuilder.CreateAsync<RunScriptCommand, Program>()
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
        public async Task TryToExecuteAnInlineScriptWithInvalidCredentials()
        {
            var psScript = $"{cliPath} info";

            await CommandTestBuilder.CreateAsync<RunScriptCommand, Program>()
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

        private void AddDefaults(CommandTestBuilderContext context, string? keyFile = null)
        {
            context.Variables.Add("Octopus.Action.GoogleCloudAccount.Variable", "MyVariable");
            context.Variables.Add("MyVariable.JsonKey", Convert.ToBase64String(Encoding.UTF8.GetBytes(keyFile ?? EnvironmentJsonKey!)));
            context.Variables.Add("Octopus.Action.GoogleCloud.CustomExecutable", cliPath);
        }
    }
}