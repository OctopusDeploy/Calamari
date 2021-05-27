using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Calamari.GoogleCloudScripting;
using FluentAssertions;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using NUnit.Framework;
using NUnit.Framework.Internal;
using Sashimi.GCPScripting;
using Sashimi.Server.Contracts;
using Sashimi.Tests.Shared;
using Sashimi.Tests.Shared.Server;
using SharpCompress.Common;
using SharpCompress.Readers;
using Object = Google.Apis.Storage.v1.Data.Object;

namespace Sashimi.GoogleCloud.Scripting.Tests
{
    [TestFixture]
    [TestFixtureSource(typeof(DownloadCLI))]
    class GoogleCloudActionHandlerFixture
    {
        private readonly string cliPath;

        class DownloadCLI: IEnumerable
        {
            async IAsyncEnumerable<Object> RetrieveFilesFromGoogleCloudStorage(StorageClient client)
            {
                var results = client.ListObjectsAsync("cloud-sdk-release", "google-cloud-sdk-");

                var listOfFilesSortedByCreatedDate =
                    new SortedList<DateTime, Object>(Comparer<DateTime>.Create((a, b) => b.CompareTo(a)));

                await foreach (var result in results)
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

                foreach (var (_, value) in listOfFilesSortedByCreatedDate.Take(30))
                {
                    if (value.Name.EndsWith(postfix))
                    {
                        yield return value;
                        break;
                    }
                }

                var threeMonthsAgo = DateTime.UtcNow.AddMonths(-6);
                foreach (var (_, value) in listOfFilesSortedByCreatedDate.SkipWhile(pair => pair.Key > threeMonthsAgo))
                {
                    if (value.Name.EndsWith(postfix))
                    {
                        yield return value;
                        break;
                    }
                }
            }

            async IAsyncEnumerable<string> DownloadFiles()
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

                using StorageClient client = await StorageClient.CreateAsync(credential);

                var fileToDownload = RetrieveFilesFromGoogleCloudStorage(client);

                var rootPath = TestEnvironment.GetTestPath("gcloudCLIPath");
                Directory.CreateDirectory(rootPath);

                await foreach (var file in fileToDownload)
                {
                    var zipFile = Path.Combine(rootPath, file.Name);

                    if (!File.Exists(zipFile))
                    {
                        await using var fileStream =
                            new FileStream(zipFile, FileMode.Create, FileAccess.Write, FileShare.None);
                        await client.DownloadObjectAsync(file, fileStream);
                    }

                    var destinationDirectory = Path.Combine(rootPath, Path.GetFileNameWithoutExtension(file.Name));
                    var gcloudExe = Path.Combine(destinationDirectory, "google-cloud-sdk", "bin", $"gcloud{(OperatingSystem.IsWindows() ? ".cmd" : String.Empty)}");

                    if (!File.Exists(gcloudExe))
                    {
                        Directory.CreateDirectory(destinationDirectory);
                        await using Stream stream = File.OpenRead(zipFile);
                        using var reader = ReaderFactory.Open(stream);
                        reader.WriteAllToDirectory(destinationDirectory,
                            new ExtractionOptions {ExtractFullPath = true});
                    }
                    ExecutableHelper.AddExecutePermission(gcloudExe);
                    yield return gcloudExe;
                }
            }

            async Task<List<string>> DownloadAllFiles()
            {
                var results = new List<string>();
                await foreach (var downloadFile in DownloadFiles())
                {
                    results.Add(downloadFile);
                }

                return results;
            }

            public IEnumerator GetEnumerator()
            {
                var results = DownloadAllFiles().GetAwaiter().GetResult();
                foreach (var result in results)
                {
                    var startIndex = result.IndexOf("google-cloud-sdk-", StringComparison.Ordinal);
                    var length = result.IndexOf(Path.DirectorySeparatorChar, startIndex + 1) - startIndex;
                    yield return new TestFixtureParameters(new TestFixtureData(result) { TestName = result.Substring(startIndex, length)});
                }
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

            ActionHandlerTestBuilder.CreateAsync<GoogleCloudActionHandler, Program>()
                .WithArrange(context =>
                {
                    AddDefaults(context);
                    context.Variables.Add(KnownVariables.Action.Script.ScriptSource, KnownVariableValues.Action.Script.ScriptSource.Inline);
                    context.Variables.Add(KnownVariables.Action.Script.Syntax, OperatingSystem.IsWindows() ? ScriptSyntax.PowerShell.ToString() : ScriptSyntax.Bash.ToString());
                    context.Variables.Add(KnownVariables.Action.Script.ScriptBody, psScript);
                })
                .Execute();
        }

        [Test]
        public void TryToExecuteAnInlineScriptWithInvalidCredentials()
        {
            var psScript = $"{cliPath} info";

            ActionHandlerTestBuilder.CreateAsync<GoogleCloudActionHandler, Program>()
                .WithArrange(context =>
                {
                    AddDefaults(context, "{ \"name\": \"hello\" }");
                    context.Variables.Add(KnownVariables.Action.Script.ScriptSource, KnownVariableValues.Action.Script.ScriptSource.Inline);
                    context.Variables.Add(KnownVariables.Action.Script.Syntax, OperatingSystem.IsWindows() ? ScriptSyntax.PowerShell.ToString() : ScriptSyntax.Bash.ToString());
                    context.Variables.Add(KnownVariables.Action.Script.ScriptBody, psScript);
                })
                .WithAssert(result => result.WasSuccessful.Should().BeFalse())
                .Execute(false);
        }

        void AddDefaults(TestActionHandlerContext<Program> context, string? keyFile = null)
        {
            context.Variables.Add("Octopus.Action.GoogleCloudAccount.Variable", "MyVariable");
            context.Variables.Add("MyVariable.JsonKey", Convert.ToBase64String(Encoding.UTF8.GetBytes(keyFile ?? EnvironmentJsonKey!)));
            context.Variables.Add("Octopus.Action.GoogleCloud.CustomExecutable", cliPath);
        }
    }
}