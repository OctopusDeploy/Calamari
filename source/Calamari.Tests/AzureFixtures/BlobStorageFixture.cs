#if AZURE
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.Azure.Accounts;
using Calamari.Azure.Commands;
using Calamari.Azure.Deployment;
using Calamari.Azure.Deployment.Conventions;
using Calamari.Azure.Deployment.Integration.BlobStorage;
using Calamari.Azure.Integration;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Serialization;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using FluentAssertions;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using Octopus.CoreUtilities.Extensions;
using Octostache;

namespace Calamari.Tests.AzureFixtures
{
    [TestFixture, Explicit]
    public class BlobStorageFixture
    {
        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public async Task SkipUploadIfMD5Matches()
        {
            var fileSelections = new List<FileSelectionProperties>
            {
                new FileSelectionProperties {Pattern = "Page1.html", FailIfNoMatches = true},
                new FileSelectionProperties {Pattern = "scripts/**/*"}
            };

            var containerName = $"{Guid.NewGuid():N}";
            DateTimeOffset? lastModified = null;

            await Execute("Package1",
                    new List<string> {"Page1.html", "scripts/Script1.js"}, fileSelections,
                    containerName: containerName, deleteContainer: false, callback: blob =>
                    {
                        if (blob.Name == "Page1.html")
                        {
                            lastModified = blob.Properties.LastModified;
                        }

                        return Task.FromResult(0);
                    })
                .ConfigureAwait(false);

            await Execute("Package1",
                    new List<string> {"Page1.html", "scripts/Script1.js"}, fileSelections,
                    containerName: containerName, callback: blob =>
                    {
                        if (blob.Name == "Page1.html")
                        {
                            blob.Properties.LastModified.Should().Be(lastModified);
                        }

                        return Task.FromResult(0);
                    })
                .ConfigureAwait(false);
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public async Task WhenMD5SkipMakeSureMetadataIsStillUpdated()
        {
            var fileSelections = new List<FileSelectionProperties>
            {
                new FileSelectionProperties {Pattern = "Page1.html", FailIfNoMatches = true},
                new FileSelectionProperties {Pattern = "scripts/**/*"}
            };

            var containerName = $"{Guid.NewGuid():N}";

            await Execute("Package1",
                    new List<string> {"Page1.html", "scripts/Script1.js"}, fileSelections,
                    containerName: containerName, deleteContainer: false)
                .ConfigureAwait(false);

            fileSelections[0].Metadata.Add("one", "two");

            await Execute("Package1",
                    new List<string> {"Page1.html", "scripts/Script1.js"}, fileSelections,
                    containerName: containerName, callback: blob =>
                    {
                        if (blob.Name == "Page1.html")
                        {
                            blob.Metadata.Should().ContainKey("one");
                            blob.Metadata["one"].Should().Be("two");
                        }

                        return Task.FromResult(0);
                    })
                .ConfigureAwait(false);
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public async Task UploadPackage1()
        {
            var fileSelections = new List<FileSelectionProperties>
            {
                new FileSelectionProperties {Pattern = "Page1.html", FailIfNoMatches = true},
                new FileSelectionProperties {Pattern = "scripts/**/*"}
            };

            await Execute("Package1",
                    new List<string> {"Page1.html", "scripts/Script1.js"}, fileSelections)
                .ConfigureAwait(false);
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public async Task UploadPackage2WithSubstitution()
        {
            var BOMMarkUtf8 = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble());

            var fileSelections = new List<FileSelectionProperties>
            {
                new FileSelectionProperties {Pattern = "Page1.html", FailIfNoMatches = true},
                new FileSelectionProperties {Pattern = "scripts/**/*"}
            };

            var substitutions = new List<string> {"scripts/**/*"};

            await Execute("Package2",
                new List<string> {"Page1.html", "scripts/replace.txt"},
                fileSelections,
                substitutions, variables => variables.Set("ReplaceValue", "Hello World"), async blob =>
                {
                    if (blob.Name == "scripts/replace.txt")
                    {
                        var text = await blob.DownloadTextAsync(Encoding.UTF8, null, null, null).ConfigureAwait(false);
                        text.Replace(BOMMarkUtf8, String.Empty).Should().Be("Test=Hello World");
                    }
                }).ConfigureAwait(false);
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public async Task UploadEntirePackage3()
        {
            await Execute("Package3",
                    new List<string> {"Page1.html", "scripts/JavaScript1.js"},
                    extraVariables: variables =>
                        variables.Set(AzureSpecialVariables.BlobStorage.Mode, TargetMode.EntirePackage.ToString()))
                .ConfigureAwait(false);
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public async Task UploadPackage3WithMetadata()
        {
            var fileSelections = new List<FileSelectionProperties>
            {
                new FileSelectionProperties {Pattern = "Page1.html", FailIfNoMatches = true, Metadata = {{"one", "two"}} },
                new FileSelectionProperties {Pattern = "scripts/**/*", Metadata = {{"three", "four"}}},
                new FileSelectionProperties {Pattern = "scripts/**/*", Metadata = {{"five", "six"}}}
            };

            await Execute("Package3",
                    new List<string> {"Page1.html", "scripts/JavaScript1.js"},
                    fileSelections, callback: blob =>
                    {
                        switch (blob.Name)
                        {
                            case "scripts/JavaScript1.js":
                                blob.Metadata.Should().ContainKey("three");
                                blob.Metadata.Should().ContainKey("five");
                                blob.Metadata["three"].Should().Be("four");
                                blob.Metadata["five"].Should().Be("six");
                                break;
                            case "Page1.html":
                                blob.Metadata.Should().ContainKey("one");
                                blob.Metadata["one"].Should().Be("two");
                                break;
                        }

                        return Task.FromResult(0);
                    })
                .ConfigureAwait(false);
        }


        private async Task Execute(string packageName, IEnumerable<string> filesExist,
            List<FileSelectionProperties> fileSelections = null,
            IReadOnlyCollection<string> substitutions = null, Action<VariableDictionary> extraVariables = null,
            Func<CloudBlockBlob, Task> callback = null, string containerName = null, bool deleteContainer = true)
        {
            if (containerName == null)
            {
                containerName = $"{Guid.NewGuid():N}";
            }

            var variablesFile = Path.GetTempFileName();
            var variables = new VariableDictionary();

            var enrichedSerializerSettings = GetEnrichedSerializerSettings();
            variables.Set(AzureSpecialVariables.BlobStorage.FileSelections,
                JsonConvert.SerializeObject(fileSelections, enrichedSerializerSettings));
            if (substitutions != null && substitutions.Count > 0)
            {
                variables.Set(SpecialVariables.Package.SubstituteInFilesTargets, String.Join("\n", substitutions));
                variables.Set(SpecialVariables.Package.SubstituteInFilesEnabled, Boolean.TrueString);
            }
            variables.Set(AzureSpecialVariables.BlobStorage.ResourceGroupName, "calamaritests");
            variables.Set(AzureSpecialVariables.BlobStorage.ContainerName, containerName);
            variables.Set(AzureSpecialVariables.BlobStorage.Mode, TargetMode.FileSelections.ToString());

            variables.Set(SpecialVariables.Account.AccountType, AzureAccountTypes.ServicePrincipalAccountType);
            variables.Set(SpecialVariables.Action.Azure.SubscriptionId,
                Environment.GetEnvironmentVariable("Azure_OctopusAPITester_SubscriptionId"));
            variables.Set(SpecialVariables.Action.Azure.ClientId,
                Environment.GetEnvironmentVariable("Azure_OctopusAPITester_ClientId"));
            variables.Set(SpecialVariables.Action.Azure.TenantId,
                Environment.GetEnvironmentVariable("Azure_OctopusAPITester_TenantId"));
            variables.Set(SpecialVariables.Action.Azure.Password,
                Environment.GetEnvironmentVariable("Azure_OctopusAPITester_Password"));
            variables.Set(SpecialVariables.Action.Azure.StorageAccountName, "calamaritests");
            
            extraVariables?.Invoke(variables);

            variables.Save(variablesFile);

            var account = AccountFactory.Create(variables) as AzureServicePrincipalAccount;
            var storageAccountPrimaryKey = await UploadToBlobStorage.GetStorageAccountPrimaryKey(account,
                variables.Get(SpecialVariables.Action.Azure.StorageAccountName),
                variables.Get(AzureSpecialVariables.BlobStorage.ResourceGroupName)).ConfigureAwait(false);
            var cloudStorage =
                new CloudStorageAccount(
                    new StorageCredentials(variables.Get(SpecialVariables.Action.Azure.StorageAccountName),
                        storageAccountPrimaryKey), DefaultVariables.StorageEndpointSuffix, true);

            var blobClient = cloudStorage.CreateCloudBlobClient();

            var container = blobClient.GetContainerReference(containerName);
            try
            {
                await container.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Off, new BlobRequestOptions(),
                    new OperationContext()).ConfigureAwait(false);

                var packageDirectory = TestEnvironment.GetTestPath("AzureFixtures", "BlobStorage", packageName);
                using (var package =
                    new TemporaryFile(PackageBuilder.BuildSimpleZip(packageName, "1.0.0", packageDirectory)))
                using (new TemporaryFile(variablesFile))
                {
                    var command = new UploadToBlobStorageCommand();
                    command.Execute(new[]
                    {
                        "--package", $"{package.FilePath}", "--variables", $"{variablesFile}"
                    });
                }

                foreach (var blobName in filesExist)
                {
                    var blob = container.GetBlockBlobReference(blobName);
                    var result = await blob.ExistsAsync().ConfigureAwait(false);
                    result.Should().BeTrue();

                    if (callback != null)
                    {
                        await callback(blob).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                if (deleteContainer)
                {
                    await container.DeleteIfExistsAsync().ConfigureAwait(false);
                }
            }
        }

        private static JsonSerializerSettings GetEnrichedSerializerSettings()
        {
            return JsonSerialization.GetDefaultSerializerSettings()
                .Tee(x => { x.ContractResolver = new CamelCasePropertyNamesContractResolver(); });
        }
    }
}
#endif